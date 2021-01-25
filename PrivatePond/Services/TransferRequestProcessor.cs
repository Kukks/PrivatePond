using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NBXplorer;
using PrivatePond.Data;
using PrivatePond.Data.EF;

namespace PrivatePond.Controllers
{
    public class TransferRequestProcessor : IHostedService
    {
        private readonly IDbContextFactory<PrivatePondDbContext> _dbContextFactory;
        private readonly IOptions<PrivatePondOptions> _options;
        private readonly ExplorerClient _explorerClient;
        private readonly WalletService _walletService;

        public TransferRequestProcessor(IDbContextFactory<PrivatePondDbContext> dbContextFactory,
            IOptions<PrivatePondOptions> options, ExplorerClient explorerClient, WalletService walletService)
        {
            _dbContextFactory = dbContextFactory;
            _options = options;
            _explorerClient = explorerClient;
            _walletService = walletService;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        private async Task MonitorBalances(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await _walletService.WaitUntilWalletsLoaded();
                var walletBalances = (await _walletService.GetWallets(new WalletQuery()
                {
                    Enabled = true
                })).ToDictionary(data => data.Id);
                var totalSum = walletBalances.Sum(data => data.Value.Balance);
                if (totalSum == 0)
                {
                    break;
                }

                var tolerance = 2;
                foreach (var wallet in _options.Value.Wallets)
                {
                    if (wallet.IdealBalance.HasValue && wallet.WalletReplenishmentSource != null)
                    {
                        var balance = walletBalances[wallet.WalletId].Balance;
                        var percentageOfTotal = (balance / totalSum) * 100;
                        if (IsWithin(percentageOfTotal, wallet.IdealBalance.Value - tolerance,
                            wallet.IdealBalance.Value + tolerance, out var above))
                        {
                            continue;
                        }

                        switch (above)
                        {
                            case true:
                                //need to send to replenishment 
                                break;
                            case false:
                                //need to receive from replenishment
                                break;
                        }
                    }
                }
            }
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

        private async Task ProcessTransferRequests(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await using var context = _dbContextFactory.CreateDbContext();
                var transferRequests = await context.TransferRequests
                    .Where(request => request.Status == TransferStatus.Pending).ToListAsync(cancellationToken: token);

                await Task.Delay(TimeSpan.FromMinutes(_options.Value.BatchTransfersEvery), token);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }
    }
}