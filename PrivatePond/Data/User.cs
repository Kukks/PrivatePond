using System;
using System.Collections.Generic;

namespace PrivatePond.Data
{
    public class User
    {
        public string Id { get; set; }
        public List<UserTransfer> UserTransfers { get; set; }
        public List<DepositRequest> DepositRequests { get; set; }
        
    }

    public class UserTransfer
    {
        public string Id { get; set; }
        public string UserId { get; set; }
        public string WalletTransactionId { get; set; }
        public string DepositRequestId { get; set; }
        public decimal Amount { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        
    }

    public class Wallet
    {
        public string Id { get; set; }
        public string DerivationStrategy { get; set; }

        public string WalletBlobJson { get; set; }
        public bool Enabled { get; set; }

        // public WalletOption GetBlob()
        // {
        //     return string.IsNullOrEmpty(WalletBlobJson) ? null : JsonSerializer.Deserialize<WalletBlob>(WalletBlobJson);
        // }
        //
        // public void SetBlob(WalletBlob blob)
        // {
        //     JsonSerializer.Serialize(blob);
        // }
       
    }
    

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
        public DateTimeOffset InactiveTimestamp { get; set; } = DateTimeOffset.UtcNow;
    }
    
    public class WalletTransaction
    {
        public string Id { get; set; }
        public string TransactionId { get; set; }
        public string DepositRequestId { get; set; }
        public string BlockHash { get; set; }
        public string OutPoint { get; set; }
        public int Confirmations { get; set; }
        public decimal Amount { get; set; }
        public WalletTransactionStatus Status { get; set; }
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
        public string WalletId { get; set; }
        public int? BlockHeight { get; set; }

        public enum WalletTransactionStatus
        {
            AwaitingConfirmation,
            Confirmed
        }
    }
}