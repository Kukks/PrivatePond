using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
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


        public PrivatePondDbContext(DbContextOptions<PrivatePondDbContext> options) : base(options)
        {
        }
    }

    public class SigningRequestItem
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public string Id { get; set; }

        public string SigningRequestId { get; set; }

        public string SignedPSBT { get; set; }

        public string SignerId { get; set; }

        public SigningRequest SigningRequest { get; set; }
    }

    public class SigningRequest
    {
        public string Id { get; set; }
        public string PSBT { get; set; }
        public string FinalPSBT { get; set; }
        public int RequiredSignatures { get; set; }
        public SigningRequestStatus Status { get; set; }
        public string WalletId { get; set; }
        public DateTimeOffset Timestamp { get; set; }

        public List<SigningRequestItem> SigningRequestItems { get; set; }


        public enum SigningRequestStatus
        {
            Pending,
            Signed,
            Expired,
        }
    }
}