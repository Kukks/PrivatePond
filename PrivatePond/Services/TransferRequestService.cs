using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
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

        public TransferRequestService(IDbContextFactory<PrivatePondDbContext> dbContextFactory,
            IOptions<PrivatePondOptions> options, ExplorerClient explorerClient, WalletService walletService)
        {
            _dbContextFactory = dbContextFactory;
            _options = options;
            _explorerClient = explorerClient;
            _walletService = walletService;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _ = MonitorBalances(cancellationToken);
            _ = ProcessTransferRequests(cancellationToken);
            return Task.CompletedTask;
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
                    .Where(request => request.Status == TransferStatus.Pending && request.TransferType == TransferType.External)
                    .OrderBy(request => request.Timestamp)
                    .ToListAsync(cancellationToken: token);
                if (transferRequests.Any())
                {
                    var allowed = _options.Value.Wallets.Where(option => option.AllowForTransfers).ToList();
                    var hotWalletTasks = allowed.ToDictionary(option => option.WalletId,
                        option => _walletService.IsHotWallet(option.WalletId));
                    await Task.WhenAll(hotWalletTasks.Values);
                    var walletBalances = await _walletService.GetWallets(new WalletQuery()
                    {
                        Enabled = true,
                        Ids = hotWalletTasks.Where(task => task.Value.Result).Select(pair => pair.Key).ToArray()
                    });
                    var totalBalance = walletBalances.Sum(data => data.Balance);
                    
                    var feeRate = await _explorerClient.GetFeeRateAsync(1, token);
                    

                }

                await Task.Delay(TimeSpan.FromMinutes(_options.Value.BatchTransfersEvery), token);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public async Task<List<TransferRequestData>> GetTransferRequests(TransferRequestQuery query)
        {
            await using var context = _dbContextFactory.CreateDbContext();
            var queryable = context.TransferRequests.AsQueryable();
            if (query.IncludeWalletTransactions)
            {
                queryable = queryable.Include(request => request.WalletTransactions);
            }

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
            var data = await queryable.ToListAsync();
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
                ToUserId = request.UserId,
                Status =  TransferStatus.Pending
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
                WalletTransactions = request.WalletTransactions
            };
        }
    }

    public class TransferRequestQuery
    {
        public bool IncludeWalletTransactions { get; set; }
        public TransferStatus[] Statuses { get; set; }
        public TransferType[] TransferTypes { get; set; }
        public string[] Ids { get; set; }
    }
}