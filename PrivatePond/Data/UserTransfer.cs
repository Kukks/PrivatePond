using System;

namespace PrivatePond.Data
{
    public class UserTransfer
    {
        public string Id { get; set; }
        public string UserId { get; set; }
        public string WalletTransactionId { get; set; }
        public string DepositRequestId { get; set; }
        public decimal Amount { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }
}