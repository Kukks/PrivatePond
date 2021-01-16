using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PrivatePond.Data;
using PrivatePond.Data.EF;

namespace PrivatePond.Controllers
{
    public class DepositService
    {
        private readonly IOptions<PrivatePondOptions> _options;
        private readonly IDbContextFactory<PrivatePondDbContext> _dbContextFactory;
        private readonly WalletService _walletService;

        public DepositService(IOptions<PrivatePondOptions> options,
            IDbContextFactory<PrivatePondDbContext> dbContextFactory,
            WalletService walletService)
        {
            _options = options;
            _dbContextFactory = dbContextFactory;
            _walletService = walletService;
        }

        public async Task<DepositRequestData> GetOrGenerateDepositRequest(string userId)
        {
            await _walletService.WaitUntilWalletsLoaded();
            await using var dbContext = _dbContextFactory.CreateDbContext();

            var result = new DepositRequestData();
            var existingActive = await dbContext.DepositRequests.Where(request =>
                request.UserId == userId && request.Active).ToListAsync();

            var walletsToRequestDepositDetails = _options.Value.Wallets.Where(option => option.DefaultDeposit &&
                !existingActive.Exists(request => request.WalletId == option.WalletId)).ToList();
            result.Items.AddRange(existingActive.Select(FromDbModel));
            foreach (var walletToRequestDepositDetail in walletsToRequestDepositDetails)
            {
                var kpi = await _walletService.ReserveAddress(walletToRequestDepositDetail.WalletId);
                if (kpi is null)
                {
                    continue;
                }
                var dr = new DepositRequest()
                {
                    UserId = userId,
                    Active = true,
                    WalletTransactions = new List<WalletTransaction>(),
                    WalletId = walletToRequestDepositDetail.WalletId,
                    Address = kpi.Address.ToString(),
                    KeyPath = kpi.KeyPath.ToString()
                };
                await dbContext.AddAsync(dr);
                result.Items.Add(FromDbModel(dr));
            }
            await dbContext.SaveChangesAsync();
            
            return result;
        }

        private DepositRequestData.DepositRequestDataItem FromDbModel(DepositRequest request)
        {
            return new()
            {
                Destination = request.Address,
                Label = request.Id,
                PaymentLink = $"bitcoin:{request.Address}"
            };
        }
    }
}