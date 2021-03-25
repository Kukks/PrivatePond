using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.BIP78.Receiver;
using BTCPayServer.BIP78.Sender;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NBitcoin;
using NBitcoin.Crypto;
using NBXplorer;
using NBXplorer.Models;
using PrivatePond.Data;
using PrivatePond.Data.EF;
using PrivatePond.Services.NBXplorer;

namespace PrivatePond.Controllers
{
    public class PayjoinReceiverWallet : PayjoinReceiverWallet<PrivatePondPayjoinProposalContext>
    {
        private readonly ExplorerClient _explorerClient;
        private readonly Network _network;
        private readonly TransactionBroadcasterService _transactionBroadcasterService;
        private readonly IDbContextFactory<PrivatePondDbContext> _dbContextFactory;
        private readonly DepositService _depositService;
        private readonly WalletService _walletService;
        private readonly TransferRequestService _transferRequestService;
        private readonly PayJoinLockService _payJoinLockService;
        private readonly NBXplorerSummaryProvider _nbXplorerSummaryProvider;
        private readonly IOptions<PrivatePondOptions> _options;

        public PayjoinReceiverWallet(ExplorerClient explorerClient, 
            Network network, 
            TransactionBroadcasterService transactionBroadcasterService, 
            IDbContextFactory<PrivatePondDbContext> dbContextFactory,
            DepositService depositService,
            WalletService walletService,
            TransferRequestService transferRequestService,
            PayJoinLockService payJoinLockService,
            NBXplorerSummaryProvider nbXplorerSummaryProvider,
            IOptions<PrivatePondOptions> options)
        {
            _explorerClient = explorerClient;
            _network = network;
            _transactionBroadcasterService = transactionBroadcasterService;
            _dbContextFactory = dbContextFactory;
            _depositService = depositService;
            _walletService = walletService;
            _transferRequestService = transferRequestService;
            _payJoinLockService = payJoinLockService;
            _nbXplorerSummaryProvider = nbXplorerSummaryProvider;
            _options = options;
        }
        protected override Task<bool> SupportsType(ScriptPubKeyType scriptPubKeyType)
        {
            return Task.FromResult(scriptPubKeyType != ScriptPubKeyType.Legacy);
        }

        protected override async Task<bool> InputsSeenBefore(PSBTInputList inputList)
        {
            return ! await _payJoinLockService.TryLockInputs(inputList.Select(input => input.PrevOut).ToArray());
        }

        protected override async Task<string> IsMempoolEligible(PSBT psbt)
        {
            var result = await _explorerClient.BroadcastAsync(psbt.ExtractTransaction(), true);
            return result.Success ? null : result.RPCCodeMessage;
        }

        protected override async Task BroadcastOriginalTransaction(PrivatePondPayjoinProposalContext context, TimeSpan scheduledTime)
        {
            if (scheduledTime == TimeSpan.Zero)
            {
                await _explorerClient.BroadcastAsync(context.OriginalTransaction);
                return;
            }

            await _transactionBroadcasterService.Schedule(new ScheduledTransaction()
            {
                BroadcastAt = DateTimeOffset.UtcNow.Add(scheduledTime),
                Id = context.OriginalTransaction.GetHash().ToString(),
                Transaction = context.OriginalTransaction.ToHex()
            });
        }

        protected override async Task ComputePayjoinModifications(PrivatePondPayjoinProposalContext context)
        {
            var enforcedLowR = context.OriginalPSBT.Inputs.All(IsLowR);
            Money due = context.PaymentRequest.Amount;
            Dictionary<OutPoint, Coin> selectedUTXOs = new Dictionary<OutPoint, Coin>();
            var utxos = context.WalletUTXOS.SelectMany(pair => pair.Value).ToArray();
            // In case we are paying ourselves, we need to make sure
            // we can't take spent outpoints.
            var prevOuts = context.OriginalTransaction.Inputs.Select(o => o.PrevOut).ToHashSet();
            utxos = utxos.Where(u => !prevOuts.Contains(u.Outpoint)).ToArray();
            //let's also remove coins which are locked by other ongoing pjs
            utxos = await _payJoinLockService.FilterOutLockedCoins(utxos);
            
            Array.Sort(utxos, CoinDeterministicComparer.Instance);
           
            if (!utxos.Any())
            {
                return;
            }
            
            var minRelayTxFee = _nbXplorerSummaryProvider.LastSummary?.Status?.BitcoinStatus?.MinRelayTxFee ??
                                new FeeRate(1.0m);
            var newTx = context.OriginalTransaction.Clone();
            var originalPaymentOutput = newTx.Outputs[context.OriginalPaymentRequestOutput.Index];
            HashSet<TxOut> isOurOutput = new HashSet<TxOut>();
            isOurOutput.Add(originalPaymentOutput);

            List<TxOut> newOutputs = new List<TxOut>();
            
            Money contributedAmount = Money.Zero;
            var canBatch = _options.Value.BatchTransfersInPayjoin &&
                           context.PayjoinParameters.DisableOutputSubstitution is not true;

            var batchedTransfers = new List<TransferRequestData>();

            if (canBatch)
            {
                //we will be conservative for v1 of batchesd transfers

                await _transferRequestService.ProcessTask.Task;
                var potentialBatchedTransfers = await _transferRequestService.GetTransferRequests(
                    new TransferRequestQuery()
                    {
                        Statuses = new[]
                        {
                            TransferStatus.Pending
                        },
                        TransferTypes = new[] {TransferType.External},
                        Take = 1

                    });
                if (potentialBatchedTransfers.Any())
                {
                    //TODO: we can make this a loop to try out combinations of transfers to batch 

                    var batchedTransfersSum = potentialBatchedTransfers.Sum(data => data.Amount);

                    var runningBalanceToPaymentOutput =
                        originalPaymentOutput.Value.ToDecimal(MoneyUnit.BTC) - batchedTransfersSum;
                    foreach (var utxo in utxos)
                    {
                        var cloned = originalPaymentOutput.Clone();
                        cloned.Value = Money.FromUnit(runningBalanceToPaymentOutput, MoneyUnit.BTC);
                        var isDust = cloned.IsDust(minRelayTxFee);
                        if (runningBalanceToPaymentOutput < 0 || isDust)
                        {
                            selectedUTXOs.Add(utxo.Outpoint, utxo);
                            runningBalanceToPaymentOutput += utxo.Amount.ToDecimal(MoneyUnit.BTC);
                        }
                        else 
                        {
                            break;
                        }
                    }

                    var cloned2 = originalPaymentOutput.Clone();
                    cloned2.Value = Money.FromUnit(runningBalanceToPaymentOutput, MoneyUnit.BTC);
                    if (runningBalanceToPaymentOutput == 0 || !cloned2.IsDust(minRelayTxFee))
                    {
                        originalPaymentOutput.Value = Money.FromUnit(runningBalanceToPaymentOutput, MoneyUnit.BTC);
                        batchedTransfers.AddRange(potentialBatchedTransfers);
                        newOutputs = batchedTransfers.Select(data =>
                        {
                            var txout = _network.Consensus.ConsensusFactory.CreateTxOut();
                            
                            txout.Value = Money.FromUnit(data.Amount, MoneyUnit.BTC);
                            txout.ScriptPubKey = BitcoinAddress.Create(
                                    HelperExtensions.GetAddress(data.Destination, _network, out _, out _, out _),
                                    _network)
                                .ScriptPubKey;
                            return txout;
                        }).ToList();
                        newTx.Outputs.AddRange(newOutputs);
                    }
                    else
                    {
                        //not enough money to batch the selected transfers
                        selectedUTXOs.Clear();
                    }
                    
                }

            }

            if(!selectedUTXOs.Any() && !batchedTransfers.Any())
            {
                foreach (var utxo in (await SelectUTXO(utxos,
                    context.OriginalPSBT.Inputs.Select(input => input.WitnessUtxo.Value.ToDecimal(MoneyUnit.BTC)),
                    context.OriginalPaymentRequestOutput.Value.ToDecimal(MoneyUnit.BTC),
                    context.OriginalPSBT.Outputs
                        .Where(psbtOutput => psbtOutput.Index != context.OriginalPaymentRequestOutput.Index)
                        .Select(psbtOutput => psbtOutput.Value.ToDecimal(MoneyUnit.BTC)))))
                {
                    selectedUTXOs.Add(utxo.Outpoint, utxo);
                }

            }
           
            if (selectedUTXOs.Count == 0 && !batchedTransfers.Any())
            {
                return;
            }

            TxOut feeOutput =
                context.PayjoinParameters.AdditionalFeeOutputIndex is int feeOutputIndex &&
                context.PayjoinParameters.MaxAdditionalFeeContribution > Money.Zero &&
                feeOutputIndex >= 0
                && feeOutputIndex < newTx.Outputs.Count
                && !isOurOutput.Contains(newTx.Outputs[feeOutputIndex])
                    ? newTx.Outputs[feeOutputIndex]
                    : null;
            var rand = new Random();
            int senderInputCount = newTx.Inputs.Count;
            foreach (var selectedUTXO in selectedUTXOs.Select(o => o.Value))
            {
                contributedAmount += selectedUTXO.Amount;
                var newInput = newTx.Inputs.Add(selectedUTXO.Outpoint);
                newInput.Sequence = newTx.Inputs[rand.Next(0, senderInputCount)].Sequence;
            }

            originalPaymentOutput.Value += contributedAmount;

            // Remove old signatures as they are not valid anymore
            foreach (var input in newTx.Inputs)
            {
                input.WitScript = WitScript.Empty;
            }

            Money ourFeeContribution = Money.Zero;
            // We need to adjust the fee to keep a constant fee rate
            var txBuilder = _network.CreateTransactionBuilder();
            var coins = context.OriginalPSBT.Inputs.Select(i => i.GetSignableCoin())
                .Concat(selectedUTXOs.Select(o => o.Value)).ToArray();

            txBuilder.AddCoins(coins);
            Money expectedFee = txBuilder.EstimateFees(newTx, context.OriginalPSBT.GetEstimatedFeeRate());
            Money actualFee = newTx.GetFee(txBuilder.FindSpentCoins(newTx));
            Money additionalFee = expectedFee - actualFee;
            bool notEnoughMoney = false;
            Money feeFromOutputIndex = Money.Zero;
            if (additionalFee > Money.Zero)
            {
                // If the user overpaid, taking fee on our output (useful if sender dump a full UTXO for privacy)
                for (int i = 0; i < newTx.Outputs.Count && additionalFee > Money.Zero && due < Money.Zero; i++)
                {
                    if (context.PayjoinParameters.DisableOutputSubstitution is true)
                        break;
                    if (isOurOutput.Contains(newTx.Outputs[i]))
                    {
                        var outputContribution = Money.Min(additionalFee, -due);
                        outputContribution = Money.Min(outputContribution,
                            newTx.Outputs[i].Value - newTx.Outputs[i].GetDustThreshold(minRelayTxFee));
                        newTx.Outputs[i].Value -= outputContribution;
                        additionalFee -= outputContribution;
                        due += outputContribution;
                        ourFeeContribution += outputContribution;
                    }
                }

                // The rest, we take from user's change
                if (feeOutput != null)
                {
                    var outputContribution = Money.Min(additionalFee, feeOutput.Value);
                    outputContribution = Money.Min(outputContribution,
                        feeOutput.Value - feeOutput.GetDustThreshold(minRelayTxFee));
                    outputContribution = Money.Min(outputContribution,
                        context.PayjoinParameters.MaxAdditionalFeeContribution);
                    feeOutput.Value -= outputContribution;

                    additionalFee -= outputContribution;
                    feeFromOutputIndex = outputContribution;
                }

                if (additionalFee > Money.Zero)
                {
                    // We could not pay fully the additional fee, however, as long as
                    // we are not under the relay fee, it should be OK.
                    var newVSize = txBuilder.EstimateSize(newTx, true);
                    var newFeePaid = newTx.GetFee(txBuilder.FindSpentCoins(newTx));
                    if (new FeeRate(newFeePaid, newVSize) < (context.PayjoinParameters.MinFeeRate ?? minRelayTxFee))
                    {
                        notEnoughMoney = true;
                    }
                }
            }

            if (!notEnoughMoney)
            {
                var newPsbt = PSBT.FromTransaction(newTx, _network);
                foreach (var derivationStrategyBase in context.HotWallets)
                {
                    var wo = _options.Value.Wallets.Single(option => option.WalletId == derivationStrategyBase.Key);
                    var resp = await _explorerClient.UpdatePSBTAsync(new UpdatePSBTRequest()
                    {
                        DerivationScheme = derivationStrategyBase.Value,
                        RebaseKeyPaths = wo.ParsedRootedKeyPaths.Select((s, i) =>
                            new PSBTRebaseKeyRules()
                            {
                                AccountKey = new BitcoinExtPubKey(
                                    derivationStrategyBase.Value.GetExtPubKeys().ElementAt(i),
                                    _network),
                                AccountKeyPath = s
                            }).ToList(),
                        PSBT = newPsbt
                    });
                    newPsbt = resp.PSBT;
                }
              
                newPsbt = await _walletService.SignWithHotWallets(context.HotWallets.Keys.ToArray(), newPsbt, new SigningOptions()
                {
                    EnforceLowR = enforcedLowR,
                    SigHash = SigHash.All
                }, CancellationToken.None);
                var ourCoins = new List<Coin>();
                foreach (var coin in selectedUTXOs.Select(o => o.Value))
                {
                    var signedInput = newPsbt.Inputs.FindIndexedInput(coin.Outpoint);
                    ourCoins.Add(coin);
                    signedInput.UpdateFromCoin(coin);
                    signedInput.FinalizeInput();
                    newTx.Inputs[signedInput.Index].WitScript =
                        newPsbt.Inputs[(int)signedInput.Index].FinalScriptWitness;
                }
                foreach (var newPsbtInput in newPsbt.Inputs)
                {
                    if (!newPsbtInput.IsFinalized())
                    {
                        newPsbtInput.WitnessUtxo = null;
                        newPsbtInput.NonWitnessUtxo = null;
                    }
                }
                foreach (var newPsbtOutput in newPsbt.Outputs)
                {
                    newPsbtOutput.HDKeyPaths.Clear();
                }
                newPsbt.GlobalXPubs.Clear();
                
                context.PayjoinReceiverWalletProposal = new PayjoinReceiverWalletProposal()
                {
                    PayjoinTransactionHash = GetExpectedHash(newPsbt,  coins.Concat(ourCoins).ToArray()),
                    PayjoinPSBT = newPsbt,
                    ContributedInputs =ourCoins.ToArray(),
                    ContributedOutputs = newOutputs.ToArray(),
                    ModifiedPaymentRequest = originalPaymentOutput,
                    ExtraFeeFromAdditionalFeeOutput = feeFromOutputIndex,
                    ExtraFeeFromReceiverInputs = ourFeeContribution,
                };
                
            }

            if (context.PayjoinReceiverWalletProposal is null)
                await _payJoinLockService.TryUnlock(
                    context.OriginalPSBT.Inputs.Select(input => input.PrevOut)
                        .Concat(selectedUTXOs.Select(pair => pair.Key)).ToArray()
                        .ToArray());
            if (notEnoughMoney)
            {
                throw new PayjoinReceiverException(
                    PayjoinReceiverHelper.GetErrorCode(PayjoinReceiverWellknownErrors.NotEnoughMoney),
                    "Not enough money is sent to pay for the additional payjoin inputs");
            }
            
            await using var dbcontext = _dbContextFactory.CreateDbContext();
            context.OriginalPSBT.TryGetFinalizedHash(out var originalPsbtHash);
            context.PayjoinRecord = new PayjoinRecord()
            {
                Id = context.PayjoinReceiverWalletProposal.PayjoinTransactionHash.ToString(),
                DepositContributedAmount =
                    context.PayjoinReceiverWalletProposal.ModifiedPaymentRequest.Value?.ToDecimal(MoneyUnit.BTC),
                OriginalTransactionId = originalPsbtHash.ToString(),
                DepositRequestId = context.DepositRequest.Id
            };

            var batchedIds = batchedTransfers.Select(data => data.Id);
           
            await dbcontext.PayjoinRecords.AddAsync(context.PayjoinRecord);
            var newSigningRequest = new SigningRequest()
            {
                Status = SigningRequest.SigningRequestStatus.Signed,
                Timestamp = DateTimeOffset.UtcNow,
                RequiredSignatures = 0,
                PSBT = context.OriginalPSBT.ToBase64(),
                FinalPSBT = context.PayjoinReceiverWalletProposal.PayjoinPSBT.ToBase64(),
                Type = SigningRequest.SigningRequestType.DepositPayjoin,
                TransactionId = context.PayjoinReceiverWalletProposal.PayjoinTransactionHash.ToString(),
            };
            await dbcontext.SigningRequests.AddAsync(newSigningRequest);
            
            var trsToMark = await dbcontext.TransferRequests.Where(request => batchedIds.Contains(request.Id))
                .ToListAsync();
            
            foreach (var transferRequest in trsToMark)
            {
                transferRequest.SigningRequestId = newSigningRequest.Id;
                transferRequest.Status = TransferStatus.Processing;
            }
            await dbcontext.SaveChangesAsync();
        }

        private async Task<Coin[]>  SelectUTXO( Coin[] availableUtxos, IEnumerable<decimal> otherInputs, decimal mainPaymentOutput,
            IEnumerable<decimal> otherOutputs)
        {
            if (availableUtxos.Length == 0)
                return Array.Empty<Coin>();
            // Assume the merchant wants to get rid of the dust
            HashSet<OutPoint> locked = new HashSet<OutPoint>();
            // We don't want to make too many db roundtrip which would be inconvenient for the sender
            int maxTries = 30;
            int currentTry = 0;
            // UIH = "unnecessary input heuristic", basically "a wallet wouldn't choose more utxos to spend in this scenario".
            //
            // "UIH1" : one output is smaller than any input. This heuristically implies that that output is not a payment, and must therefore be a change output.
            //
            // "UIH2": one input is larger than any output. This heuristically implies that no output is a payment, or, to say it better, it implies that this is not a normal wallet-created payment, it's something strange/exotic.
            //src: https://gist.github.com/AdamISZ/4551b947789d3216bacfcb7af25e029e#gistcomment-2796539

            foreach (var availableUtxo in availableUtxos)
            {
                if (currentTry >= maxTries)
                    break;

                var invalid = false;
                foreach (var input in otherInputs.Concat(new[] {availableUtxo.Amount.ToDecimal(MoneyUnit.BTC)}))
                {
                    var computedOutputs =
                        otherOutputs.Concat(new[] {mainPaymentOutput + availableUtxo.Amount.ToDecimal(MoneyUnit.BTC)});
                    if (computedOutputs.Any(output => input > output))
                    {
                        //UIH 1 & 2
                        invalid = true;
                        break;
                    }
                }

                if (invalid)
                {
                    continue;
                }

                if (await _payJoinLockService.TryLock(availableUtxo.Outpoint))
                {
                    return new[] {availableUtxo};
                }

                locked.Add(availableUtxo.Outpoint);
                currentTry++;
            }

            foreach (var utxo in availableUtxos.Where(u => !locked.Contains(u.Outpoint)))
            {
                if (currentTry >= maxTries)
                    break;
                if (await _payJoinLockService.TryLock(utxo.Outpoint))
                {
                    return new[] {utxo};
                }

                currentTry++;
            }

            return Array.Empty<Coin>();

        }

        protected override async Task<PayjoinPaymentRequest> FindMatchingPaymentRequests(PrivatePondPayjoinProposalContext context)
        {
            var outputAddressMap = context.OriginalPSBT.Outputs.Select(output => (Output: output, Address: output.ScriptPubKey.GetDestinationAddress(_network))).ToList();
            await using var dbContext = _dbContextFactory.CreateDbContext();
            var deposits = await _depositService.GetDepositRequests(new DepositRequestQuery()
            {
                Active = true,
                Address = outputAddressMap.Select(address => address.Address.ToString()).ToArray()
            }, CancellationToken.None);
            if (!deposits.Any())
            {
                return null;
            }
            
            var receiverInputsType = context.OriginalPSBT.GetInputsScriptPubKeyType();
            var (wallets, utxos) = await _walletService.GetHotWallets("payjoin");
            wallets = wallets.Where(pair => pair.Value.ScriptPubKeyType() == receiverInputsType).ToDictionary(pair => pair.Key,  pair => pair.Value);
            if (!wallets.Any())
            {
                return null;
            }

            utxos = utxos.Where(pair => wallets.ContainsKey(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value);  
            context.HotWallets = wallets;
            context.WalletUTXOS = utxos;
            if (!wallets.Any())
            {
                return null;
            }
            var possibleRequests = new Dictionary<string,PayjoinPaymentRequest>();
            foreach (var depositRequest in deposits.OrderBy(request => request.Address))
            {
                var matchedOutput = outputAddressMap.First(pair => pair.Address.ToString() == depositRequest.Address);
                possibleRequests.Add(depositRequest.Id,new PayjoinPaymentRequest()
                {
                    Amount = matchedOutput.Output.Value,
                    Destination = matchedOutput.Address
                });
            }

            var picked = possibleRequests.OrderBy(request => (request.Value.Destination is BitcoinWitPubKeyAddress))
                .FirstOrDefault();
            context.DepositRequest = deposits.FirstOrDefault(request => request.Id == picked.Key);
            return picked.Value;

        }
        private uint256 GetExpectedHash(PSBT psbt, ICoin[] coins)
        {
            psbt = psbt.Clone();
            psbt.AddCoins(coins);
            if (!psbt.TryGetFinalizedHash(out var hash))
                throw new InvalidOperationException("Unable to get the finalized hash");
            return hash;
        }
        
        private static bool IsLowR(PSBTInput txin)
        {
            IEnumerable<byte[]> pushes = txin.FinalScriptWitness?.PushCount > 0 ? txin.FinalScriptWitness.Pushes :
                txin.FinalScriptSig?.IsPushOnly is true ? txin.FinalScriptSig.ToOps().Select(o => o.PushData) :
                Array.Empty<byte[]>();
            return pushes.Where(ECDSASignature.IsValidDER).All(p => p.Length <= 71);
        }
        /// <summary>
        /// This comparer sorts utxo in a deterministic manner
        /// based on a random parameter.
        /// When a UTXO is locked because used in a coinjoin, in might be unlocked
        /// later if the coinjoin failed.
        /// Such UTXO should be reselected in priority so we don't expose the other UTXOs.
        /// By making sure this UTXO is almost always coming on the same order as before it was locked,
        /// it will more likely be selected again.
        /// </summary>
        internal class CoinDeterministicComparer : IComparer<Coin>
        {
            static CoinDeterministicComparer()
            {
                _Instance = new CoinDeterministicComparer(RandomUtils.GetUInt256());
            }

            public CoinDeterministicComparer(uint256 blind)
            {
                _blind = blind.ToBytes();
            }

            static readonly CoinDeterministicComparer _Instance;
            private readonly byte[] _blind;

            public static CoinDeterministicComparer Instance => _Instance;

            public int Compare([AllowNull] Coin x, [AllowNull] Coin y)
            {
                if (x == null)
                    throw new ArgumentNullException(nameof(x));
                if (y == null)
                    throw new ArgumentNullException(nameof(y));
                Span<byte> tmpx = stackalloc byte[32];
                Span<byte> tmpy = stackalloc byte[32];
                x.Outpoint.Hash.ToBytes(tmpx);
                y.Outpoint.Hash.ToBytes(tmpy);
                for (int i = 0; i < 32; i++)
                {
                    if ((byte)(tmpx[i] ^ _blind[i]) < (byte)(tmpy[i] ^ _blind[i]))
                    {
                        return 1;
                    }

                    if ((byte)(tmpx[i] ^ _blind[i]) > (byte)(tmpy[i] ^ _blind[i]))
                    {
                        return -1;
                    }
                }

                return x.Outpoint.N.CompareTo(y.Outpoint.N);
            }
        }
    }
}