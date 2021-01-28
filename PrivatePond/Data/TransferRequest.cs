using System;
using System.Collections.Generic;
using NBitcoin;
using NBitcoin.Payment;

namespace PrivatePond.Data
{
    public class TransferRequest
    {
        public string Id { get; set; }
        public string ToUserId { get; set; }
        public string ToWalletId { get; set; }
        public string FromWalletId { get; set; }
        public List<WalletTransaction> WalletTransactions { get; set; }
        public TransferStatus Status { get; set; }
        public decimal Amount { get; set; }
        public string Destination { get; set; }
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
        public TransferType TransferType { get; set; }
    }
}