using System;
using System.Collections.Generic;
using PrivatePond.Data;

namespace PrivatePond.Controllers
{
    public class TransferRequestData
    {
        public string Id { get; set; }
        public string Destination { get; set; }
        public decimal Amount { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public TransferStatus Status { get; set; }
        public List<WalletTransaction> WalletTransactions { get; set; }
    }
}