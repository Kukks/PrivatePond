using Microsoft.EntityFrameworkCore;

namespace PrivatePond.Data.EF
{
    public class PrivatePondDbContext : DbContext
    {
        public const string DatabaseConnectionStringName = "PrivatePondDatabase";
        public DbSet<DepositRequest> DepositRequests { get; set; }
        public DbSet<WalletTransaction> WalletTransactions { get; set; }
        public DbSet<Wallet> Wallets { get; set; }

        public PrivatePondDbContext(DbContextOptions<PrivatePondDbContext> options) : base(options)
        {
        }
    }
}