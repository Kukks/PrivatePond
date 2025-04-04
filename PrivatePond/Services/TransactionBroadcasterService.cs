using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBXplorer;
using PrivatePond.Data;
using PrivatePond.Data.EF;
using PrivatePond.Services.NBXplorer;

namespace PrivatePond.Controllers
{
    public class TransactionBroadcasterService : IHostedService
    {
        private readonly ExplorerClient _explorerClient;
        private readonly Network _network;
        private readonly IDbContextFactory<PrivatePondDbContext> _dbContextFactory;
        private readonly ILogger<TransactionBroadcasterService> _logger;
        private readonly WalletService _walletService;
        private readonly NBXplorerSummaryProvider _nbXplorerSummaryProvider;

        public TransactionBroadcasterService(
            ExplorerClient explorerClient,
            Network network,
            IDbContextFactory<PrivatePondDbContext> dbContextFactory,
            ILogger<TransactionBroadcasterService> logger, WalletService walletService,
            NBXplorerSummaryProvider nbXplorerSummaryProvider)
        {
            _explorerClient = explorerClient;
            _network = network;
            _dbContextFactory = dbContextFactory;
            _logger = logger;
            _walletService = walletService;
            _nbXplorerSummaryProvider = nbXplorerSummaryProvider;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _ = BroadcastPlanned(cancellationToken);
            return Task.CompletedTask;
        }
        public async Task Schedule(ScheduledTransaction transaction)
        {
            await using var ctx = _dbContextFactory.CreateDbContext();
            await ctx.ScheduledTransactions.AddAsync(transaction);
            await ctx.SaveChangesAsync();
        }

        private async Task BroadcastPlanned(CancellationToken token)
        {
            await _walletService.WaitUntilWalletsLoaded();
            while (!token.IsCancellationRequested)
            {
                await _explorerClient.WaitServerStartedAsync(token);
                await using var context = _dbContextFactory.CreateDbContext();
                var t = DateTimeOffset.Now;
                var txs = await context.ScheduledTransactions
                    .Where(transaction => transaction.BroadcastAt <= t)
                    .ToListAsync(token);
               foreach (var scheduledTransaction in txs)
               {
                   try
                   {
                       var tx = Transaction.Parse(scheduledTransaction.Transaction, _network);
                       if ((await _explorerClient.BroadcastAsync(tx, token))
                           .Success && !string.IsNullOrEmpty(scheduledTransaction.ReplacesSigningRequestId))
                       {
                           var replacementSigningRequestId = (await context.SigningRequests.SingleOrDefaultAsync(
                               request => request.TransactionId == tx.GetHash().ToString(), token))?.Id;

                           if (replacementSigningRequestId is null)
                           {
                               
                               var newSigningRequest = new SigningRequest()
                               {
                                   Status = SigningRequest.SigningRequestStatus.Signed,
                                   Timestamp = DateTimeOffset.UtcNow,
                                   RequiredSignatures = 0,
                                   TransactionId = tx.GetHash().ToString(),
                                   PSBT = tx.CreatePSBT(_network).ToBase64(),
                                   FinalPSBT = tx.CreatePSBT(_network).ToBase64(),
                                   Type = SigningRequest.SigningRequestType.PayjoinFallback
                               };
                               await context.AddAsync(newSigningRequest, token);
                               replacementSigningRequestId = newSigningRequest.Id;
                           }
                           
                           _logger.LogInformation($"Planned tx {tx.GetHash()} broadcasted. {(string.IsNullOrEmpty(scheduledTransaction.ReplacesSigningRequestId)? "": $"Replacing signing request {scheduledTransaction.ReplacesSigningRequestId}")} with {replacementSigningRequestId}");
                           var trs = await context.TransferRequests
                               .Where(request =>
                                   request.SigningRequestId == scheduledTransaction.ReplacesSigningRequestId)
                               .ToListAsync(token);

                           var failedSignedRequest =
                               await context.SigningRequests.FindAsync(scheduledTransaction.ReplacesSigningRequestId);

                           if (failedSignedRequest.Type == SigningRequest.SigningRequestType.DepositPayjoin)
                           {
                               //transfer requests linked to this failed signing request were batched
                               trs.ForEach(request =>
                               {
                                   request.SigningRequestId = null;
                                   request.Status = TransferStatus.Pending;
                               });
                           }
                           else
                           {
                               trs.ForEach(request =>
                               {
                                   request.SigningRequestId = replacementSigningRequestId;
                                   request.Status = TransferStatus.Processing;
                               });
                           }
                           
                           
                       };
                       context.Remove(tx);
                   }
                   catch (Exception e)
                   {
                       _logger.LogInformation($"Failed to attempt to broadcast: {e.Message}");
                   }
               }

               await context.SaveChangesAsync(token);
               await Task.Delay(TimeSpan.FromMinutes(1), token);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}