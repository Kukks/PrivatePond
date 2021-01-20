using System;
using System.Collections.Generic;

namespace PrivatePond.Data
{
    public class DepositRequest
    {
        public string Id { get; set; }
        public string UserId { get; set; }
        public string WalletId { get; set; }
        public List<WalletTransaction> WalletTransactions { get; set; }
        public bool Active { get; set; }
        public string Address { get; set; }
        public string KeyPath { get; set; }

        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
        public Wallet Wallet { get; set; }
    }
}