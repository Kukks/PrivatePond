using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrivatePond.Data
{
    public class TransferRequest
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public string Id { get; set; }
        public List<WalletTransaction> WalletTransactions { get; set; }
        public TransferStatus Status { get; set; }
        public decimal Amount { get; set; }
        public string Destination { get; set; }
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
        public TransferType TransferType { get; set; }
    }
}