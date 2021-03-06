using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PrivatePond.Data.EF
{
    public class SigningRequest
    {
        /// <summary>
        /// the id of the signing request.This is typically the transaction id of what is being signed
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string PSBT { get; set; }
        public string FinalPSBT { get; set; }
        public int RequiredSignatures { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SigningRequestStatus Status { get; set; }
        public string WalletId { get; set; }
        public DateTimeOffset Timestamp { get; set; }

        public List<SigningRequestItem> SigningRequestItems { get; set; }
        
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
            DepositPayjoin
        }
        
    }
}