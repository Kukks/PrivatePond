using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.Serialization;
using NBitcoin;

namespace PrivatePond.Data
{
    public class WalletTransaction
    {
        public string Id { get; set; }
        public string DepositRequestId { get; set; }
        public string BlockHash { get; set; }
        public int Confirmations { get; set; }
        public decimal Amount { get; set; }
        public WalletTransactionStatus Status { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public string WalletId { get; set; }
        public int? BlockHeight { get; set; }

        [NotMapped]
        public OutPoint OutPoint
        {
            get => string.IsNullOrEmpty(Id) ? null : OutPoint.Parse(Id);
            set => Id = value.ToString();
        }

        public Wallet Wallet { get; set; }
        public bool InactiveDepositRequest { get; set; }

        public enum WalletTransactionStatus
        {
            AwaitingConfirmation,
            Confirmed,
            Replaced,
            RequiresApproval
        }
    }
}