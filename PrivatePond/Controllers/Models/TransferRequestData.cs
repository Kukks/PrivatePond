using System;
using System.Text.Json.Serialization;
using PrivatePond.Data;

namespace PrivatePond.Controllers
{
    public class TransferRequestData
    {
        /// <summary>
        /// the id of the transfer request
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        /// the bitcoin address or payment link to transfer to
        /// </summary>
        public string Destination { get; set; }
        /// <summary>
        /// the amount being transferred
        /// </summary>
        public decimal Amount { get; set; }
        //the transfer request creation time
        public DateTimeOffset Timestamp { get; set; }
        
        /// <summary>
        /// the status of this transfer (Pending,Processing,Completed,Cancelled)
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TransferStatus Status { get; set; }
        /// <summary>
        /// The transaction id if the transfer has been processed
        /// </summary>
        public string TransactionHash { get; set; }
    }
}