using Microsoft.EntityFrameworkCore;

namespace PrivatePond.Data.EF
{
    public class PrivatePondDbContext : DbContext
    {
        public const string DatabaseConnectionStringName = "PrivatePondDatabase";
        public DbSet<DepositRequest> DepositRequests { get; set; }
        public DbSet<WalletTransaction> WalletTransactions { get; set; }
        public DbSet<Wallet> Wallets { get; set; }
        public DbSet<TransferRequest> TransferRequests { get; set; }
        public DbSet<SigningRequest> SigningRequests { get; set; }
        public DbSet<SigningRequestItem> SigningRequestItems { get; set; }
        public DbSet<ScheduledTransaction> ScheduledTransactions { get; set; }
        


        public PrivatePondDbContext(DbContextOptions<PrivatePondDbContext> options) : base(options)
        {
        }
    }
}