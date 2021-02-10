using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

        public async Task<List<DepositRequestData>> GetOrGenerateDepositRequest(string userId)
        {
            await _walletService.WaitUntilWalletsLoaded();
            await using var dbContext = _dbContextFactory.CreateDbContext();
            userId = NormalizeUserId(userId);
            var result = new List<DepositRequestData>();
            var existingActive = await dbContext.DepositRequests.Where(request =>
                request.UserId == userId && request.Active).ToListAsync();
            var depositActivatedWallets=  _options.Value.Wallets.Where(option => option.AllowForDeposits).ToList();
            var walletsToRequestDepositDetails = depositActivatedWallets.Where(option => 
                !existingActive.Exists(request => request.WalletId == option.WalletId)).ToList();
            result.AddRange(existingActive.Select(FromDbModel));
            foreach (var walletToRequestDepositDetail in walletsToRequestDepositDetails)
            {
                var kpi = await _walletService.ReserveAddress(walletToRequestDepositDetail.WalletId);
                if (kpi is null)
                {
                    continue;
                }

                var dr = new DepositRequest()
                {
                    Id = kpi.ScriptPubKey.Hash.ToString(),
                    UserId = userId,
                    Active = true,
                    WalletTransactions = new List<WalletTransaction>(),
                    WalletId = walletToRequestDepositDetail.WalletId,
                    Address = kpi.Address.ToString(),
                    KeyPath = kpi.KeyPath.ToString()
                };
                await dbContext.AddAsync(dr);
                result.Add(FromDbModel(dr));
            }

            await dbContext.SaveChangesAsync();
            //sort them before returning
            return depositActivatedWallets.Select(wallet => result.Single(data => data.WalletId == wallet.WalletId)).ToList();
        }

        private DepositRequestData FromDbModel(DepositRequest request)
        {
            return new()
            {
                UserId = NormalizeUserId(request.UserId),
                WalletId = request.WalletId,
                Destination = request.Address,
                Label = request.Id,
                PaymentLink = $"bitcoin:{request.Address}",
                History = request?.WalletTransactions?.Select(transaction =>
                    new DepositRequestDataItemPaymentItem()
                    {
                        Confirmed = transaction.Status == WalletTransaction.WalletTransactionStatus.Confirmed,
                        Timestamp = transaction.Timestamp,
                        Value = transaction.Amount,
                        TransactionId = transaction.OutPoint.Hash.ToString()
                    })?.ToList()
            };
        }
        
        public async Task<List<DepositRequest>> GetDepositRequests(DepositRequestQuery query,
            CancellationToken cancellationToken)
        {
            
            await _walletService.WaitUntilWalletsLoaded();
            await using var dbContext = _dbContextFactory.CreateDbContext();

            var queryable = dbContext.DepositRequests.AsQueryable();
            if (query.IncludeWalletTransactions)
            {
                queryable = queryable.Include(request => request.WalletTransactions);
            }

            if (query.WalletIds?.Any() is true)
            {
                queryable = queryable.Where(transaction =>
                    query.WalletIds.Contains(transaction.WalletId));
            }

            if (query.Active.HasValue)
            {
                queryable = queryable.Where(transaction =>
                    query.Active == transaction.Active);
            }

            if (query.Ids?.Any() is true)
            {
                queryable = queryable.Where(transaction =>
                    query.Ids.Contains(transaction.Id));
            }
            if (query.UserIds?.Any() is true)
            {
                query.UserIds = query.UserIds.Select(NormalizeUserId).ToArray();
                queryable = queryable.Where(transaction =>
                    query.UserIds.Contains(transaction.UserId));
            }

            return await queryable.ToListAsync(cancellationToken);
        }

        private string NormalizeUserId(string userId)
        {
            return userId.ToLowerInvariant().Trim();
        }
    }
}