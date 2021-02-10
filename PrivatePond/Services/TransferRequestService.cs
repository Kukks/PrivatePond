using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NBitcoin;
using NBXplorer;
using NBXplorer.DerivationStrategy;
using NBXplorer.Models;
using PrivatePond.Data;
using PrivatePond.Data.EF;

namespace PrivatePond.Controllers
{
    public class TransferRequestService : IHostedService
    {
        private readonly IDbContextFactory<PrivatePondDbContext> _dbContextFactory;
        private readonly IOptions<PrivatePondOptions> _options;
        private readonly ExplorerClient _explorerClient;
        private readonly WalletService _walletService;
        private readonly Network _network;
        private readonly ILogger<TransferRequestService> _logger;

        public TransferRequestService(IDbContextFactory<PrivatePondDbContext> dbContextFactory,
            IOptions<PrivatePondOptions> options, ExplorerClient explorerClient, WalletService walletService,
            Network network, ILogger<TransferRequestService> logger)
        {
            _dbContextFactory = dbContextFactory;
            _options = options;
            _explorerClient = explorerClient;
            _walletService = walletService;
            _network = network;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _walletService.WaitUntilWalletsLoaded();
            await ClearInternalPendingRequests(cancellationToken);

            _ = ProcessTransferRequestsWithHotWallet(cancellationToken);
        }

        private async Task ClearInternalPendingRequests(CancellationToken cancellationToken)
        {
            //we should clear any pending requests for internal transfers and elt the system compute them on startup
            await using var context = _dbContextFactory.CreateDbContext();
            await ClearInternalPendingRequests(context, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        private async Task ClearInternalPendingRequests(PrivatePondDbContext context,
            CancellationToken cancellationToken)
        {
            var transferRequests = await GetTransferRequests(context, new TransferRequestQuery()
            {
                TransferTypes = new[] {TransferType.Internal}, Statuses = new[] {TransferStatus.Pending}
            });

            transferRequests.ForEach(request => request.Status = TransferStatus.Cancelled);
            var signingRequestsToCancel = transferRequests.Select(request => request.SigningRequestId).Distinct();
            var signingRequests = await context.SigningRequests
                .Where(request => signingRequestsToCancel.Contains(request.Id))
                .ToListAsync(cancellationToken: cancellationToken);
            signingRequests.ForEach(request => request.Status = SigningRequest.SigningRequestStatus.Expired);
        }

        //
        // private async Task<List<TransferRequest>> BalanceWallets(Dictionary<string, decimal> walletBalances,CancellationToken token)
        // {
        //     var totalSum = walletBalances.Sum(data => data.Value);
        //     if (totalSum == 0)
        //     {
        //         return new List<TransferRequest>();
        //     }
        //
        //     var result = new List<TransferRequest>();
        //     // we can never be 100% accurate to the desired ratios. Allow a variation of specified percentage 
        //     var tolerance = 2;
        //     foreach (var wallet in _options.Value.Wallets)
        //     {
        //         if (wallet.IdealBalance.HasValue && wallet.WalletReplenishmentSource != null)
        //         {
        //             var balance = walletBalances[wallet.WalletId];
        //             var percentageOfTotal = (balance / totalSum) * 100;
        //             var idealBalanceAmt = (totalSum * 0.01m) * wallet.IdealBalance.Value;
        //             if (IsWithin(percentageOfTotal, wallet.IdealBalance.Value - tolerance,
        //                 wallet.IdealBalance.Value + tolerance, out var above))
        //             {
        //                 continue;
        //             }
        //
        //             var transferRequest = new TransferRequest()
        //             {
        //                 TransferType = TransferType.Internal,
        //                 Timestamp = DateTimeOffset.UtcNow,
        //                 Status = TransferStatus.Pending,
        //                 
        //             };
        //             DerivationStrategyBase derivation = null;
        //             switch (above)
        //             {
        //                 case true:
        //                     transferRequest.Amount = balance - idealBalanceAmt;
        //                     transferRequest.ToWalletId = wallet.WalletReplenishmentSourceWalletId;
        //                     transferRequest.FromWalletId = wallet.WalletId;
        //                     derivation = await _walletService.GetDerivationsByWalletId(wallet.WalletId);
        //                     //need to send to replenishment 
        //                     break;
        //                 case false:
        //                     transferRequest.Amount = idealBalanceAmt - balance;
        //                     transferRequest.ToWalletId = wallet.WalletId;
        //                     transferRequest.FromWalletId = wallet.WalletReplenishmentSourceWalletId;
        //                     derivation = await _walletService.GetDerivationsByWalletId(wallet.WalletId);
        //                     //need to receive from replenishment
        //                     break;
        //             }
        //             var destination =
        //                 await _explorerClient.GetUnusedAsync(derivation, DerivationFeature.Deposit, 0, true, token);
        //             transferRequest.Destination = destination.Address.ToString();
        //             result.Add(transferRequest);
        //         }
        //     }
        //
        //     return result;
        //
        // }

        private (decimal Amount, bool Above)? HandleReplenishmentRequests(Dictionary<string, decimal> walletBalances,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_options.Value.WalletReplenishmentSourceWalletId))
            {
                return null;
            }

            var minimumAmountToBalance = 0.01m;
            var tolerance = 2;
            var totalSum = walletBalances.Sum(data => data.Value);
            if (totalSum < minimumAmountToBalance)
            {
                return null;
            }

            var balance = walletBalances[_options.Value.WalletReplenishmentSourceWalletId];
            var percentageOfTotal = (balance / totalSum) * 100;
            var idealBalanceAmt = (totalSum * 0.01m) * _options.Value.WalletReplenishmentIdealBalancePercentage.Value;
            if (IsWithin(percentageOfTotal, _options.Value.WalletReplenishmentIdealBalancePercentage.Value - tolerance,
                _options.Value.WalletReplenishmentIdealBalancePercentage.Value + tolerance, out var above))
            {
                return null;
            }

            return (Amount: above.Value ? balance - idealBalanceAmt : idealBalanceAmt - balance, Above: above.Value);
        }

        private static bool IsWithin(decimal value, decimal minimum, decimal maximum, out bool? above)
        {
            if (value > maximum)
            {
                above = true;
                return false;
            }

            if (value < minimum)
            {
                above = false;
                return false;
            }

            above = null;
            return true;
        }

        private async Task ProcessTransferRequestsWithHotWallet(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await _explorerClient.WaitServerStartedAsync(token);
                    await using var context = _dbContextFactory.CreateDbContext();
                    var transferRequests = await context.TransferRequests
                        .Where(request =>
                            request.Status == TransferStatus.Pending && request.TransferType == TransferType.External)
                        .OrderBy(request => request.Timestamp)
                        .ToListAsync(cancellationToken: token);
                    if (transferRequests.Any())
                    {
                        var allowed = _options.Value.Wallets.Where(option => option.AllowForTransfers).ToList();
                        var hotWalletTasks = allowed.ToDictionary(option => option.WalletId,
                            option => _walletService.IsHotWallet(option.WalletId));
                        await Task.WhenAll(hotWalletTasks.Values);
                        hotWalletTasks = hotWalletTasks.Where(pair => pair.Value.Result)
                            .ToDictionary(pair => pair.Key, pair => pair.Value);

                        var walletBalances = await _walletService.GetWallets(new WalletQuery()
                        {
                            Enabled = true
                        }).ContinueWith(task => task.Result.ToDictionary(data => data.Id, data => data.Balance), token);

                        var hotWalletDerivationSchemes =
                            hotWalletTasks.ToDictionary(s => s.Key,
                                s => _walletService.GetDerivationsByWalletId(s.Key));
                        await Task.WhenAll(hotWalletDerivationSchemes.Values);

                        var walletUtxos = hotWalletTasks.Keys.ToDictionary(s => s,
                            s => _explorerClient.GetUTXOsAsync(hotWalletDerivationSchemes[s].Result, token)
                                .ContinueWith(task => task.Result.GetUnspentCoins(_options.Value.MinimumConfirmations),
                                    token));

                        await Task.WhenAll(walletUtxos.Values);

                        var transfersProcessing = new List<TransferRequest>();

                        var feeRate = await _explorerClient.GetFeeRateAsync(1, new FeeRate(20m), token);
                        var coins = walletUtxos.Values.SelectMany(task => task.Result);
                        Transaction workingTx = null;
                        //TODO: Make this smart. We can use it to "balance" the hot wallets and also send extra funds to cold replenishment wallet.
                        var changeAddress = await _explorerClient.GetUnusedAsync(
                            hotWalletDerivationSchemes.First().Value.Result, DerivationFeature.Change, 0, true,
                            token);
                        Money fees = Money.Zero;
                        foreach (var transferRequest in transferRequests)
                        {
                            var txBuilder = _network.CreateTransactionBuilder().AddCoins(coins);
                            txBuilder.SetChange(changeAddress.Address);
                            if (workingTx is not null)
                            {
                                txBuilder = txBuilder.ContinueToBuild(workingTx);
                            }

                            var address = BitcoinAddress.Create(HelperExtensions.GetAddress(transferRequest.Destination,
                                _network, out _, out _), _network);
                            txBuilder.Send(address, new Money(transferRequest.Amount, MoneyUnit.BTC));
                           
                            try
                            {
                                var newFee = txBuilder.EstimateFees(feeRate.FeeRate);
                                var additionalFee = newFee - fees;
                                txBuilder.SendFees(additionalFee);
                                workingTx = txBuilder.BuildTransaction(false);

                                fees = newFee;
                                transfersProcessing.Add(transferRequest);
                            }
                            catch (NotEnoughFundsException e)
                            {
                                //keep going, we prioritize withdraws by time but if there is some other we can fit, we should
                            }
                        }

                        var spentCoinsByWallet = _network.CreateTransactionBuilder().AddCoins(coins)
                            .ContinueToBuild(workingTx).FindSpentCoins(workingTx).GroupBy(spentCoin => walletUtxos
                                .Single(pair => pair.Value.Result.Any(coin => spentCoin.Outpoint == coin.Outpoint))
                                .Key);
                        // todo:
                        // find spent coins, group by wallet id,
                        var balanceChangePerWallet = spentCoinsByWallet.ToDictionary(grouping => grouping.Key,
                            grouping => Money.Satoshis(grouping.Sum(coin => coin.Amount as Money))
                                .ToDecimal(MoneyUnit.BTC));
                        // compute change
                        var changeOutputs = workingTx.Outputs.Where(txOut => txOut.IsTo(changeAddress.Address));
                        var changeAmount = Money.Satoshis(changeOutputs
                            .Sum(txOut => txOut.Value)).ToDecimal(MoneyUnit.BTC);
                        // compute final balance based on spent coins

                        // run BalanceWallets and create transfer requests to balance wallets involved.
                        var balancesAfter = new Dictionary<string, decimal>();
                        if (changeAmount > 0)
                        {
                            balancesAfter.Add("change", changeAmount);
                        }

                        foreach (var keyValuePair in walletBalances)
                        {
                            if (balanceChangePerWallet.TryGetValue(keyValuePair.Key, out var balanceChange))
                            {
                                balancesAfter.Add(keyValuePair.Key, keyValuePair.Value - balanceChange);
                            }
                            else
                            {
                                balancesAfter.Add(keyValuePair.Key, keyValuePair.Value);
                            }
                        }

                        var replenishmentRequest = HandleReplenishmentRequests(balancesAfter, token);

                        if (replenishmentRequest.HasValue)
                        {
                            //replenishment wallet is below threshold
                            if (!replenishmentRequest.Value.Above)
                            {
                                var replenishmentAddress = await _explorerClient.GetUnusedAsync(
                                    await _walletService.GetDerivationsByWalletId(_options.Value
                                        .WalletReplenishmentSourceWalletId), DerivationFeature.Deposit, 0, true, token);
                                //if the change is enough, let's keep it simple and just redirect it to the replenishment wallet only
                                if (IsWithin(changeAmount, replenishmentRequest.Value.Amount * 0.98m,
                                    replenishmentRequest.Value.Amount * 1.02m, out _))
                                {
                                    foreach (var changeOutput in changeOutputs)
                                    {
                                        changeOutput.ScriptPubKey = replenishmentAddress.ScriptPubKey;
                                    }
                                }
                                else
                                {
                                    //if it's too different, let's remove the change from the tx, add more coins and build the tx again
                                    var withoutChange = workingTx.Outputs.Except(changeOutputs);
                                    var txWithReplenishment = workingTx.Clone();
                                    txWithReplenishment.Outputs.Clear();
                                    txWithReplenishment.Outputs.AddRange(withoutChange);

                                    //how much can we send? 
                                    //the base is obviously the actual amount needed to fit within quota 
                                    var replenishmentAmount = replenishmentRequest.Value.Amount;
                                    //but if not all wallets are hot wallets, then we can only move funds based on those balances.
                                    var hotWalletTotalBalance = balancesAfter
                                                                    .Where(pair =>
                                                                        hotWalletTasks.Keys.Contains(pair.Key))
                                                                    .Sum(pair => pair.Value) +
                                                                changeAmount;
                                    replenishmentAmount = Math.Min(replenishmentAmount, hotWalletTotalBalance);


                                    var txBuilder =
                                        _network.CreateTransactionBuilder().AddCoins(coins)
                                            .ContinueToBuild(txWithReplenishment);

                                    txBuilder = txBuilder
                                        .Send(replenishmentAddress.Address,
                                            new Money(replenishmentAmount, MoneyUnit.BTC));
                                    
                                    try
                                    {
                                        var newFee = txBuilder.EstimateFees(feeRate.FeeRate);
                                        var additionalFee = newFee - fees;
                                        txBuilder.SendFees(additionalFee);
                                        workingTx = txBuilder.BuildTransaction(false);
                                        var replenishmentTransferRequest = new TransferRequest()
                                        {
                                            Amount = replenishmentAmount,
                                            Status = TransferStatus.Pending,
                                            Timestamp = DateTimeOffset.UtcNow,
                                            Destination = replenishmentAddress.Address.ToString(),
                                            ToWalletId = _options.Value
                                                .WalletReplenishmentSourceWalletId,
                                            TransferType = TransferType.Internal
                                        };
                                        await context.AddAsync(replenishmentTransferRequest, token);
                                        transfersProcessing.Add(replenishmentTransferRequest);
                                    }
                                    catch (NotEnoughFundsException e)
                                    {
                                    }
                                }
                            }
                            else
                            {
                                //replenishment wallet is overflowing, let's create a request to transfer from replenishment to hot wallets.
                                balancesAfter[hotWalletDerivationSchemes.First().Key] =
                                    balancesAfter[hotWalletDerivationSchemes.First().Key] + changeAmount;
                                if (balancesAfter.ContainsKey("change"))
                                {
                                    balancesAfter.Remove("change");
                                }

                                var replenishmentWallet =
                                    await _walletService.GetDerivationsByWalletId(_options.Value
                                        .WalletReplenishmentSourceWalletId);
                                var walletOption = _options.Value.Wallets.Single(option => option.WalletId == _options
                                    .Value
                                    .WalletReplenishmentSourceWalletId);
                                var walletsToReplenish = balancesAfter.Where(pair =>
                                    pair.Key != _options.Value.WalletReplenishmentSourceWalletId);

                                var totalParts = walletsToReplenish.Sum(pair => pair.Value);
                                var onePartValue = replenishmentRequest.Value.Amount / totalParts;
                                var replenishmentAmounts =
                                    walletsToReplenish.ToDictionary(pair => pair.Key,
                                        pair => pair.Value * onePartValue);
                                var replenishmentAddresses = walletsToReplenish.ToDictionary(pair => pair.Key,
                                    pair => _walletService.GetDerivationsByWalletId(pair.Key)
                                        .ContinueWith(
                                            task => _explorerClient.GetUnusedAsync(task.Result,
                                                DerivationFeature.Deposit,
                                                0, true, token), token));
                                await Task.WhenAll(replenishmentAddresses.Values);
                                var replenishmentpsbt = await _explorerClient.CreatePSBTAsync(replenishmentWallet,
                                    new CreatePSBTRequest()
                                    {
                                        Destinations = replenishmentAddresses.Select(pair => new CreatePSBTDestination()
                                        {
                                            Destination = pair.Value.Result.Result.Address,
                                            Amount = new Money(replenishmentAmounts[pair.Key], MoneyUnit.BTC)
                                        }).ToList(),
                                        FeePreference = new FeePreference()
                                        {
                                            ExplicitFeeRate = feeRate.FeeRate
                                        },
                                        IncludeGlobalXPub = true,
                                        AlwaysIncludeNonWitnessUTXO = true,
                                        RebaseKeyPaths = walletOption.RootedKeyPaths.Select((s, i) =>
                                            new PSBTRebaseKeyRules()
                                            {
                                                AccountKey = new BitcoinExtPubKey(
                                                    replenishmentWallet.GetExtPubKeys().ElementAt(i), _network),
                                                AccountKeyPath = RootedKeyPath.Parse(s)
                                            }).ToList()
                                    }, token);

                                var signingRequest = new SigningRequest()
                                {
                                    Status = SigningRequest.SigningRequestStatus.Pending,
                                    Timestamp = DateTimeOffset.UtcNow,
                                    WalletId = _options.Value
                                        .WalletReplenishmentSourceWalletId,
                                    RequiredSignatures = replenishmentWallet.GetExtPubKeys().Count() == 1
                                        ? 1
                                        : int.Parse(replenishmentWallet.ToString().Split("-").First()),
                                    PSBT = replenishmentpsbt.PSBT.ToBase64()
                                };
                                await context.AddAsync(signingRequest, token);
                                foreach (var replenishmentAmount in replenishmentAmounts)
                                {
                                    await context.TransferRequests.AddAsync(new TransferRequest()
                                    {
                                        Amount = replenishmentAmount.Value,
                                        ToWalletId = replenishmentAmount.Key,
                                        Destination = replenishmentAddresses[replenishmentAmount.Key].Result.Result
                                            .Address.ToString(),
                                        Status = TransferStatus.Pending,
                                        SigningRequestId = signingRequest.Id,
                                        Timestamp = DateTimeOffset.UtcNow,
                                        TransferType = TransferType.Internal,
                                        FromWalletId = _options.Value.WalletReplenishmentSourceWalletId
                                    }, token);
                                }
                            }
                        }


                        var psbt = _network.CreateTransactionBuilder().AddCoins(coins).ContinueToBuild(workingTx)
                            .BuildPSBT(false);
                        foreach (var hotWalletDerivationScheme in hotWalletDerivationSchemes)
                        {
                            var walletOption = _options.Value.Wallets.Single(option =>
                                option.WalletId == hotWalletDerivationScheme.Key);
                            var res = await _explorerClient.UpdatePSBTAsync(new UpdatePSBTRequest()
                            {
                                DerivationScheme = hotWalletDerivationScheme.Value.Result,
                                IncludeGlobalXPub = true,
                                PSBT = psbt,
                                RebaseKeyPaths = walletOption.RootedKeyPaths.Select((s, i) => new PSBTRebaseKeyRules()
                                {
                                    AccountKey = new BitcoinExtPubKey(
                                        hotWalletDerivationScheme.Value.Result.GetExtPubKeys().ElementAt(i), _network),
                                    AccountKeyPath = RootedKeyPath.Parse(s)
                                }).ToList()
                            }, token);
                            psbt = res.PSBT;
                        }

                        var signedPsbt =
                            await _walletService.SignWithHotWallets(hotWalletDerivationSchemes.Keys.ToArray(), psbt);
                        signedPsbt.Finalize();
                        var tx = signedPsbt.ExtractTransaction();
                        var txId = tx.GetHash().ToString();
                        var timestamp = DateTimeOffset.UtcNow;
                        var signedPsbtBase64 = signedPsbt.ToBase64();
                        await context.SigningRequests.AddAsync(new SigningRequest()
                        {
                            Id = txId,
                            Status = SigningRequest.SigningRequestStatus.Signed,
                            PSBT = psbt.ToBase64(),
                            FinalPSBT = signedPsbtBase64,
                            Timestamp = timestamp,
                            RequiredSignatures = 0
                        }, token);
                        foreach (var transferRequest in transfersProcessing)
                        {
                            transferRequest.Status = TransferStatus.Processing;
                            transferRequest.SigningRequestId = txId;
                        }

                        var result = await _explorerClient.BroadcastAsync(tx, token);
                        if (!result.Success)
                        {
                            _logger.LogError(
                                $"Error trying to do automated transfer with hot wallet(s). Node response: {result.RPCCodeMessage}");
                        }
                        else
                        {
                            _logger.LogInformation(
                                $"Automated transfer broadcasted tx {txId} fulfilling {transfersProcessing.Count} requests");
                        }

                        await context.SaveChangesAsync(token);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error when attempting to process automated transfers");
                }

                await Task.Delay(TimeSpan.FromMinutes(_options.Value.BatchTransfersEvery), token);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private async Task<List<TransferRequest>> GetTransferRequests(PrivatePondDbContext context,
            TransferRequestQuery query)
        {
            var queryable = context.TransferRequests.AsQueryable();

            if (query.Ids?.Any() is true)
            {
                queryable = queryable.Where(transaction =>
                    query.Ids.Contains(transaction.Id));
            }

            if (query.Statuses?.Any() is true)
            {
                queryable = queryable.Where(transaction =>
                    query.Statuses.Contains(transaction.Status));
            }

            if (query.TransferTypes?.Any() is true)
            {
                queryable = queryable.Where(transaction =>
                    query.TransferTypes.Contains(transaction.TransferType));
            }

            return await queryable.ToListAsync();
        }

        public async Task<List<TransferRequestData>> GetTransferRequests(TransferRequestQuery query)
        {
            await using var context = _dbContextFactory.CreateDbContext();
            var data = await GetTransferRequests(context, query);
            return data.Select(FromDbModel).ToList();
        }

        public async Task<TransferRequestData> CreateTransferRequest(RequestTransferRequest request)
        {
            await using var context = _dbContextFactory.CreateDbContext();
            var tr = new TransferRequest()
            {
                Amount = request.Amount.Value,
                Destination = request.Destination,
                Timestamp = DateTimeOffset.UtcNow,
                TransferType = TransferType.External,
                Status = TransferStatus.Pending
            };
            await context.TransferRequests.AddAsync(tr);
            await context.SaveChangesAsync();
            return FromDbModel(tr);
        }

        private TransferRequestData FromDbModel(TransferRequest request)
        {
            return new TransferRequestData()
            {
                Id = request.Id,
                Amount = request.Amount,
                Destination = request.Destination,
                Status = request.Status,
                Timestamp = request.Timestamp,
                TransactionHash = request.SigningRequestId
            };
        }

        public async Task MarkCompleted(List<string> completedTransferRequestIds)
        {
            await using var context = _dbContextFactory.CreateDbContext();
            var items = await context.TransferRequests
                .Where(request => completedTransferRequestIds.Contains(request.Id)).ToListAsync();
            foreach (var transferRequest in items)
            {
                transferRequest.Status = TransferStatus.Completed;
            }

            _logger.LogInformation($"Marked {completedTransferRequestIds.Count} transfer requests as completed.");
            await context.SaveChangesAsync();
        }
    }
}