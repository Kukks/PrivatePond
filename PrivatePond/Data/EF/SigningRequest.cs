using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace PrivatePond.Data.EF
{
    public class SigningRequest
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public string Id { get; set; }
        /// <summary>
        /// the transaction of the psbt being signed
        /// </summary>
        public string TransactionId { get; set; }
        /// <summary>
        /// the unsigned PSBT
        /// </summary>
        public string PSBT { get; set; }
        /// <summary>
        /// the final, signed PSBT
        /// </summary>
        public string FinalPSBT { get; set; }
        /// <summary>
        /// the amount of signers required to complete this request
        /// </summary>
        public int RequiredSignatures { get; set; }
        /// <summary>
        /// The current status (Pending,Signed,Expired,Failed)
        /// Typically, for signers, they should only look for Pending status 
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SigningRequestStatus Status { get; set; }
        /// <summary>
        /// the wallet id ( may be null if it is using UTXOS from multiple wallets)
        /// </summary>
        public string WalletId { get; set; }
        /// <summary>
        /// the time it was created
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        /// submitted signatures by signers
        /// </summary>
        public List<SigningRequestItem> SigningRequestItems { get; set; }
        
        
        /// <summary>
        /// The type of signing request (HotWallet,Replenishment,ExpressTransfer,ExpressTransferPayjoin,DepositPayjoin)
        /// Typically, for signers, they should only look for Replenishment types 
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SigningRequestType Type { get; set; }

        public enum SigningRequestStatus
        {
            Pending,
            Signed,
            Expired,
            Failed
        }
        public enum SigningRequestType
        {
            HotWallet,
            Replenishment,
            ExpressTransfer,
            ExpressTransferPayjoin,
            DepositPayjoin,
            PayjoinFallback
        }
        
    }
}