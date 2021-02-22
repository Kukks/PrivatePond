using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NBitcoin;
using NBXplorer;
using NBXplorer.Models;
using PrivatePond.Controllers;
using PrivatePond.Data;

namespace PrivatePond.Services.NBXplorer
{
    public class NBXplorerListener : IHostedService
    {
        private readonly ExplorerClient _explorerClient;
        private readonly NBXplorerSummaryProvider _nbXplorerSummaryProvider;
        private readonly ILogger<NBXplorerListener> _logger;
        private readonly WalletService _walletService;
        private readonly IOptions<PrivatePondOptions> _options;
        private readonly DepositService _depositService;
        private readonly TransferRequestService _transferRequestService;

        public NBXplorerListener(ExplorerClient explorerClient, NBXplorerSummaryProvider nbXplorerSummaryProvider,
            ILogger<NBXplorerListener> logger, WalletService walletService, IOptions<PrivatePondOptions> options,
            DepositService depositService, TransferRequestService transferRequestService)
        {
            _explorerClient = explorerClient;
            _nbXplorerSummaryProvider = nbXplorerSummaryProvider;
            _logger = logger;
            _walletService = walletService;
            _options = options;
            _depositService = depositService;
            _transferRequestService = transferRequestService;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _ = ListenForEvents(cancellationToken);
            _ = UpdateSummaryContinuously(cancellationToken);
            return Task.CompletedTask;
        }

        private async Task ListenForEvents(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _walletService.WaitUntilWalletsLoaded();
                    _logger.LogInformation("Waiting for NBX to be ready");
                    await _explorerClient.WaitServerStartedAsync(cancellationToken);
                    await using var notificationSession =
                        await _explorerClient.CreateWebsocketNotificationSessionAsync(cancellationToken);
                    await notificationSession.ListenNewBlockAsync(cancellationToken);
                    await notificationSession.ListenAllTrackedSourceAsync(false, cancellationToken);
                    await CheckForMissingTxs(_options.Value.Wallets.Select(option => option.WalletId).ToArray(),
                        cancellationToken);
                    await CheckPendingTransactions(cancellationToken);
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var evt = await notificationSession.NextEventAsync(cancellationToken);
                        switch (evt)
                        {
                            case NewBlockEvent blockEvent:
                                await CheckPendingTransactions(cancellationToken);
                                break;
                            case NewTransactionEvent txEvent:
                                if (txEvent.DerivationStrategy is not null)
                                {
                                    var walletId = WalletService.GetWalletId(txEvent.DerivationStrategy);

                                    var depositIdToOutput =
                                        txEvent.Outputs.ToDictionary(output => output.ScriptPubKey.Hash.ToString());

                                    var matchedDepositRequests = await _depositService.GetDepositRequests(
                                        new DepositRequestQuery()
                                        {
                                            IncludeWalletTransactions = true,
                                            Ids = depositIdToOutput.Keys.ToArray(),
                                            WalletIds = new[] {walletId}
                                        }, cancellationToken);
                                    var updatedDepositRequests = new List<DepositRequest>();
                                    var updatedWalletTransactions = new List<WalletTransaction>();
                                    var newWalletTransactions = new List<WalletTransaction>();
                                    foreach (var depositRequest in matchedDepositRequests)
                                    {
                                        var matchedOutput = depositIdToOutput[depositRequest.Id];
                                        depositIdToOutput.Remove(depositRequest.Id);
                                        UpdateDepositRequest(depositRequest, updatedDepositRequests, matchedOutput,
                                            txEvent.TransactionData, updatedWalletTransactions, walletId,
                                            newWalletTransactions);
                                    }

                                    //whatever is left in depositIdToOutput, is not a deposit request but some external transfer. We should log it 
                                    var unmatchedWalletTransactions = await _walletService.GetWalletTransactions(
                                        new WalletTransactionQuery()
                                        {
                                            WalletIds = new[] {walletId},
                                            Ids = depositIdToOutput.Select(pair => new OutPoint(txEvent.TransactionData.TransactionHash, pair.Value.Index).ToString()).ToArray()
                                        }, cancellationToken);
                                    foreach (var unmatchedWalletTransaction in unmatchedWalletTransactions)
                                    {
                                        depositIdToOutput.Remove(unmatchedWalletTransaction.Id);
                                        if (UpdateWalletTransactionFromTransactionResult(unmatchedWalletTransaction,
                                            txEvent.TransactionData))
                                        {
                                            updatedWalletTransactions.Add(unmatchedWalletTransaction);
                                        }
                                    }

                                    foreach (var keyValuePair in depositIdToOutput)
                                    {
                                        var outpoint = new OutPoint(txEvent.TransactionData.TransactionHash,
                                            keyValuePair.Value.Index);
                                        if (unmatchedWalletTransactions.Any(transaction =>
                                            transaction.Id == outpoint.ToString()))
                                        {
                                            continue;
                                        }
                                        var newWalletTransaction = new WalletTransaction()
                                        {
                                            OutPoint = outpoint,
                                            Amount = (keyValuePair.Value.Value as Money).ToDecimal(MoneyUnit.BTC),
                                            WalletId = walletId,
                                            DepositRequestId = null
                                        };
                                        UpdateWalletTransactionFromTransactionResult(newWalletTransaction,
                                            txEvent.TransactionData);
                                        newWalletTransactions.Add(newWalletTransaction);
                                    }

                                    await _walletService.Update(new WalletService.UpdateContext()
                                    {
                                        AddedWalletTransactions = newWalletTransactions,
                                        UpdatedDepositRequests = updatedDepositRequests,
                                        UpdatedWalletTransactions = updatedWalletTransactions
                                    }, cancellationToken);
                                }

                                break;
                            case UnknownEvent unknownEvent:
                                _logger.LogWarning(
                                    $"Received unknown message from NBXplorer ({unknownEvent.CryptoCode}), ID: {unknownEvent.EventId}");
                                break;
                        }
                    }
                }
                catch when (cancellationToken.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        $"Error while listening to NBX events. Restarting listener");
                }
            }
        }

        private void UpdateDepositRequest(DepositRequest depositRequest, List<DepositRequest> updatedDepositRequests,
            MatchedOutput matchedOutput,
            TransactionResult transactionResult, List<WalletTransaction> updatedWalletTransactions, string walletId,
            List<WalletTransaction> newWalletTransactions)
        {
            var wasActive = depositRequest.Active;
            if (wasActive)
            {
                depositRequest.Active = false;
                updatedDepositRequests.Add(depositRequest);
            }

            var outpoint = new OutPoint(transactionResult.TransactionHash,
                matchedOutput.Index);
            depositRequest.Active = false;
            var matchedWalletTransaction =
                depositRequest.WalletTransactions?.SingleOrDefault(transaction =>
                    transaction.OutPoint == outpoint);
            if (matchedWalletTransaction != null)
            {
                //this tx was already registered
                UpdateWalletTransactionFromTransactionResult(matchedWalletTransaction,
                    transactionResult);
            }
            else
            {
                var newWalletTransaction = new WalletTransaction()
                {
                    OutPoint = outpoint,
                    Amount = (matchedOutput.Value as Money).ToDecimal(MoneyUnit.BTC),
                    WalletId = walletId,
                    DepositRequestId = depositRequest.Id,
                    InactiveDepositRequest = !wasActive
                };
                UpdateWalletTransactionFromTransactionResult(newWalletTransaction,
                    transactionResult);
                newWalletTransactions.Add(newWalletTransaction);
            }
        }

        private async Task UpdateSummaryContinuously(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await _nbXplorerSummaryProvider.UpdateClientState(cancellationToken);
                await Task.Delay(
                    TimeSpan.FromSeconds(_nbXplorerSummaryProvider.LastSummary.State == NBXplorerState.Ready ? 30 : 5),
                    cancellationToken);
            }
        }

        private async Task CheckForMissingTxs(string[] walletIds, CancellationToken cancellationToken)
        {
            var ctx = new WalletService.UpdateContext();
            foreach (var walletId in walletIds)
            {
                var derivation = await _walletService.GetDerivationsByWalletId(walletId);
                if (derivation is null)
                {
                    continue;
                }

                var utxos = (await _explorerClient.GetUTXOsAsync(derivation, cancellationToken));
                var utxoDict = utxos.Confirmed.UTXOs.Concat(utxos.Unconfirmed.UTXOs)
                    .ToDictionary(utxo => utxo.Outpoint.ToString());
                var walletTransactions = await _walletService.GetWalletTransactions(
                    new WalletTransactionQuery()
                    {
                        Ids = utxoDict.Keys.ToArray()
                    }, cancellationToken);
                var missingTxs =
                    utxoDict.Where(pair => walletTransactions.All(transaction => transaction.Id != pair.Key));
                var potentialDepositRequestIds = missingTxs.Select(pair => pair.Value.ScriptPubKey.Hash.ToString());
                var matchedDepositRequests = (await _depositService.GetDepositRequests(
                    new DepositRequestQuery()
                    {
                        Ids = potentialDepositRequestIds.ToArray()
                    }, cancellationToken)).ToDictionary(request => request.Id);
                foreach (var keyValuePair in missingTxs)
                {
                    var tx = await
                        _explorerClient.GetTransactionAsync(keyValuePair.Value.TransactionHash, cancellationToken);
                    var depositRequestId = keyValuePair.Value.ScriptPubKey.Hash.ToString();
                    if (matchedDepositRequests.TryGetValue(depositRequestId, out var depositRequest))
                    {
                        UpdateDepositRequest(depositRequest, ctx.UpdatedDepositRequests, new MatchedOutput()
                        {
                            Index = keyValuePair.Value.Index,
                            Value = keyValuePair.Value.Value,
                            KeyPath = keyValuePair.Value.KeyPath,
                            ScriptPubKey = keyValuePair.Value.ScriptPubKey
                        }, tx, ctx.UpdatedWalletTransactions, walletId, ctx.AddedWalletTransactions);
                    }
                    else
                    {
                        var newWalletTransaction = new WalletTransaction()
                        {
                            OutPoint = keyValuePair.Value.Outpoint,
                            Amount = ((Money) keyValuePair.Value.Value).ToDecimal(MoneyUnit.BTC),
                            WalletId = walletId,
                            DepositRequestId = null,
                            InactiveDepositRequest = false
                        };
                        UpdateWalletTransactionFromTransactionResult(newWalletTransaction, tx);
                        ctx.AddedWalletTransactions.Add(newWalletTransaction);
                    }
                }
            }

            await _walletService.Update(ctx, cancellationToken);
        }


        private async Task CheckPendingTransactions(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Updating pending transactions");
            //refactor and use wallet get tx from start
            var walletTransactions = await _walletService.GetWalletTransactions(
                new WalletTransactionQuery()
                {
                    IncludeWallet = true,
                    Statuses = new[] {WalletTransaction.WalletTransactionStatus.AwaitingConfirmation}
                }, cancellationToken);
            var txIds = walletTransactions.Select(transaction => transaction.OutPoint.Hash).ToHashSet();
            var txFetchTasks =
                txIds.ToDictionary(s => s, s => _explorerClient.GetTransactionAsync(s, cancellationToken));
            await Task.WhenAll(txFetchTasks.Values);
            var updated = new List<WalletTransaction>();

            foreach (var walletTransactionGroup in walletTransactions.GroupBy(transaction => transaction.OutPoint.Hash))
            {
                var txResult = await txFetchTasks[walletTransactionGroup.Key];
                foreach (var walletTransaction in walletTransactionGroup)
                {
                    if (UpdateWalletTransactionFromTransactionResult(walletTransaction, txResult))
                    {
                        updated.Add(walletTransaction);
                    }
                }
            }

            var notUpdated0ConfirmationTransactions = walletTransactions.Where(transaction =>
                    transaction.Confirmations <= 0 && !updated.Contains(transaction))
                .GroupBy(transaction => transaction.WalletId).ToList();
            if (notUpdated0ConfirmationTransactions.Any())
            {
                foreach (var notUpdated0ConfirmationTransactionGroup in notUpdated0ConfirmationTransactions)
                {
                    var wallet = notUpdated0ConfirmationTransactionGroup.First().Wallet.DerivationStrategy;
                    var deriv = _walletService.GetDerivationStrategy(wallet);
                    var txs = await _explorerClient.GetTransactionsAsync(deriv, cancellationToken);
                    var replacedWalletTransactions = notUpdated0ConfirmationTransactionGroup.Where(transaction =>
                        txs.ReplacedTransactions.Transactions.Any(information =>
                            information.TransactionId == transaction.OutPoint.Hash));
                    foreach (var replacedWalletTransaction in replacedWalletTransactions)
                    {
                        replacedWalletTransaction.Status = WalletTransaction.WalletTransactionStatus.Replaced;
                        updated.Add(replacedWalletTransaction);
                    }
                }
            }

            await _walletService.Update(new WalletService.UpdateContext() {UpdatedWalletTransactions = updated},
                cancellationToken);

            //let's handle transfer requests that were:
            // a withdrawal request that was processed
            // an internal tx that was broadcasted

            var processingRequests = await _transferRequestService.GetTransferRequests(new TransferRequestQuery()
            {
                Statuses = new[] {TransferStatus.Pending,TransferStatus.Processing}
            });

            var txFetchResult = processingRequests.GroupBy(data => data.TransactionHash)
                .Where(datas => datas.Key is not null).Select(datas =>
                    (datas, _explorerClient.GetTransactionAsync(uint256.Parse(datas.Key), cancellationToken)));
            await Task.WhenAll(txFetchResult.Select(t => t.Item2));
            Dictionary<TransferStatus, List<string>> markMap = new Dictionary<TransferStatus, List<string>>()
            {
                {TransferStatus.Processing, new List<string>()},
                {TransferStatus.Completed, new List<string>()}
            };
            foreach (var txFetch in txFetchResult)
            {
                var tx = await txFetch.Item2;
                if (tx != null && tx.Confirmations >= _options.Value.MinimumConfirmations)
                {
                    markMap[TransferStatus.Completed].AddRange(txFetch.datas.Select(data => data.Id));
                }else if (tx != null)
                {
                    markMap[TransferStatus.Processing].AddRange(txFetch.datas.Select(data => data.Id));
                }
            }

            await _transferRequestService.Mark(markMap);
        }

        private bool UpdateWalletTransactionFromTransactionResult(WalletTransaction walletTransaction,
            TransactionResult transactionResult)
        {
            var hash = JsonSerializer.Serialize(walletTransaction);
            walletTransaction.Confirmations = transactionResult.Confirmations;
            walletTransaction.BlockHash = transactionResult.BlockId?.ToString();
            walletTransaction.Timestamp = transactionResult.Timestamp;
            walletTransaction.BlockHeight = transactionResult.Height;
            walletTransaction.Status = walletTransaction.Confirmations >= _options.Value.MinimumConfirmations
                ? walletTransaction.InactiveDepositRequest
                    ? WalletTransaction.WalletTransactionStatus.RequiresApproval
                    : WalletTransaction.WalletTransactionStatus.Confirmed
                : walletTransaction.Status == WalletTransaction.WalletTransactionStatus.Confirmed
                    ? WalletTransaction.WalletTransactionStatus.Confirmed
                    : WalletTransaction.WalletTransactionStatus.AwaitingConfirmation;

            return hash == JsonSerializer.Serialize(walletTransaction);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}