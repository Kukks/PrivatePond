using System;
using System.Collections.Generic;
using System.Linq;
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

        public NBXplorerListener(ExplorerClient explorerClient, NBXplorerSummaryProvider nbXplorerSummaryProvider,
            ILogger<NBXplorerListener> logger, WalletService walletService, IOptions<PrivatePondOptions> options)
        {
            _explorerClient = explorerClient;
            _nbXplorerSummaryProvider = nbXplorerSummaryProvider;
            _logger = logger;
            _walletService = walletService;
            _options = options;
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
                    await _explorerClient.WaitServerStartedAsync(cancellationToken);
                    await using var notificationSession =
                        await _explorerClient.CreateWebsocketNotificationSessionAsync(cancellationToken);
                    await notificationSession.ListenNewBlockAsync(cancellationToken);
                    await notificationSession.ListenAllTrackedSourceAsync(false, cancellationToken);

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
                                    foreach (var matchedOutput in txEvent.Outputs)
                                    {
                                        //check if already recorded  
                                        _walletService.GetWalletTransactions(new WalletService.WalletTransactionQuery())
                                    }
                                }
                                
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
                        $"Error while connecting to WebSocket of NBXplorer");
                }
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

        public async Task CheckPendingTransactions(CancellationToken cancellationToken)
        {
            var walletTransactions = await _walletService.GetWalletTransactions(new WalletService.WalletTransactionQuery()
            {
                Statuses = new [] {WalletTransaction.WalletTransactionStatus.AwaitingConfirmation}
            });
            var txIds = walletTransactions.Select(transaction => uint256.Parse(transaction.TransactionId)).ToHashSet();
            var txFetchTasks = txIds.ToDictionary(s => s.ToString(), s => _explorerClient.GetTransactionAsync(s, cancellationToken));
            await Task.WhenAll(txFetchTasks.Values);
            var updated = new List<WalletTransaction>();
            foreach (var walletTransaction in walletTransactions)
            {
                var txResult = await txFetchTasks[walletTransaction.TransactionId];
                var hash = walletTransaction.GetHashCode();
                walletTransaction.Confirmations = txResult.Confirmations;
                walletTransaction.BlockHash = txResult.BlockId?.ToString();
                walletTransaction.Timestamp = txResult.Timestamp;
                walletTransaction.BlockHeight = txResult.Height;
                walletTransaction.Status = walletTransaction.Confirmations >= _options.Value.MinimumConfirmations
                    ? WalletTransaction.WalletTransactionStatus.Confirmed
                    : WalletTransaction.WalletTransactionStatus.AwaitingConfirmation;
                if (hash != walletTransaction.GetHashCode())
                {
                    updated.Add(walletTransaction);
                }
            }

            await _walletService.UpdateWalletTransactions(updated);

        }
        
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}