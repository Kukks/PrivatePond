using System;
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
        

        public PrivatePondDbContext(DbContextOptions<PrivatePondDbContext> options) : base(options)
        {
        }
    }

    public class SigningRequest
    {
        public string Id { get; set; }
        public string SigningRequestGroup { get; set; }
        public string PSBT { get; set; }
        public string SignedPSBT { get; set; }
        public SigningRequestStatus Status { get; set; }
        public string WalletId { get; set; }
        public string SignerId { get; set; }
        public DateTimeOffset Timestamp { get; set; }

        public enum SigningRequestStatus
        {
            Pending, Signed, Expired, 
        }
    }
}