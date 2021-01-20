using Microsoft.EntityFrameworkCore;

namespace PrivatePond.Data.EF
{
    public class PrivatePondDbContext : DbContext
    {
        public const string DatabaseConnectionStringName = "PrivatePondDatabase";
        public DbSet<User> Users { get; set; }
        public DbSet<DepositRequest> DepositRequests { get; set; }
        public DbSet<WalletTransaction> WalletTransactions { get; set; }
        public DbSet<Wallet> Wallets { get; set; }

        public PrivatePondDbContext()
        {
            
        }
        public PrivatePondDbContext(DbContextOptions<PrivatePondDbContext> options) : base(options)
        {
        }
    }

    public class PrivatePondDesignTimeDbContextFactory :
        DesignTimeDbContextFactoryBase<PrivatePondDbContext>
    {
        protected override PrivatePondDbContext CreateNewInstance(DbContextOptions<PrivatePondDbContext> options)
        {
            return new PrivatePondDbContext(options);
        }
    }
}