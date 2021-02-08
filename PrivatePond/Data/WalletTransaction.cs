using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using NBitcoin;

namespace PrivatePond.Data
{
    public class WalletTransaction
    {
        /// <summary>
        /// the identifier -- usually the utxo (format: txid:outputindex)
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// the deposit request this transaction is linked to
        /// </summary>
        public string DepositRequestId { get; set; }

        public string TransferRequestId { get; set; }

        /// <summary>
        /// the first block hash it was confirmed in 
        /// </summary>
        public string BlockHash { get; set; }

        /// <summary>
        /// the number of confirmations it has. 0 if still in mempool
        /// </summary>
        public int Confirmations { get; set; }

        /// <summary>
        /// the amount being transferred
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// the status of this transaction (AwaitingConfirmation,Confirmed,Replaced,RequiresApproval)
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public WalletTransactionStatus Status { get; set; }

        /// <summary>
        /// the timestamp this transactionw as created
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        /// the wallet id this transaction is linked to
        /// </summary>
        public string WalletId { get; set; }

        /// <summary>
        /// the block number this tx was mined in
        /// </summary>
        public int? BlockHeight { get; set; }

        [NotMapped]
        [JsonConverter(typeof(OutPointJsonConverter))]
        public OutPoint OutPoint
        {
            get => string.IsNullOrEmpty(Id) ? null : OutPoint.Parse(Id);
            set => Id = value.ToString();
        }

        [JsonIgnore] public Wallet Wallet { get; set; }

        /// <summary>
        /// If this tx was recorded to a deposit request that was marked as inactive. If true, the transaction may require approval before it is accepted as a deposit.
        /// </summary>
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