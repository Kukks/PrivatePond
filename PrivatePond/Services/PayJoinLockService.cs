using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using NBitcoin;
using PrivatePond.Data;
using PrivatePond.Data.EF;

namespace PrivatePond.Controllers
{
    public class PayJoinLockService:IHostedService
    {
        private readonly IDbContextFactory<PrivatePondDbContext> _dbContextFactory;

        public PayJoinLockService(IDbContextFactory<PrivatePondDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public async Task<T[]> FilterOutLockedCoins<T>(T[] coins)  where T: ICoin
        {
            await using var ctx = _dbContextFactory.CreateDbContext();
            var idToCoins = coins.ToDictionary(coin => coin.Outpoint.ToString());
            var ids = idToCoins.Keys.ToArray();
            var matchedLocks = (await ctx.PayjoinLocks.Where(pjLock => ids.Contains(pjLock.Id)).ToArrayAsync()).Select(pjLock => pjLock.Id);
            return idToCoins.Where(pair => !matchedLocks.Contains(pair.Key)).Select(pair => pair.Value).ToArray();

        } 
        
        public async Task<bool> TryLock(OutPoint outpoint)
        {
            await using var ctx = _dbContextFactory.CreateDbContext();
            await ctx.PayjoinLocks.AddAsync(new PayjoinLock()
            {
                Id = outpoint.ToString()
            });
            try
            {
                return await ctx.SaveChangesAsync() == 1;
            }
            catch (DbUpdateException)
            {
                return false;
            }
        }

        public async Task<bool> TryUnlock(params OutPoint[] outPoints)
        {
            await using var ctx = _dbContextFactory.CreateDbContext();
            foreach (OutPoint outPoint in outPoints)
            {
                ctx.PayjoinLocks.Remove(new PayjoinLock()
                {
                    Id = outPoint.ToString()
                });
            }
            try
            {
                return await ctx.SaveChangesAsync() == outPoints.Length;
            }
            catch (DbUpdateException)
            {
                return false;
            }
        }

        public async Task<bool> TryLockInputs(OutPoint[] outPoints)
        {
            await using var ctx = _dbContextFactory.CreateDbContext();
            await ctx.PayjoinLocks.AddRangeAsync(outPoints.Select(point => new PayjoinLock()
            {
                // Random flag so it does not lock same id
                // as the lock utxo
                Id = $"K-{point}"
            }));
            try
            {
                return await ctx.SaveChangesAsync() == outPoints.Length;
            }
            catch (DbUpdateException)
            {
                return false;
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            async Task Loop()
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await using var context = _dbContextFactory.CreateDbContext();
                    var t = DateTimeOffset.UtcNow.AddMinutes(-4);
                    var expiredLocks = await  context.PayjoinLocks.Where(pjLock =>
                        pjLock.Timestamp < t && !pjLock.Id.StartsWith("K-")).ToListAsync(cancellationToken);
                    context.PayjoinLocks.RemoveRange(expiredLocks);
                    await context.SaveChangesAsync(cancellationToken);
                    await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
                }
            }

            _ = Loop();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}