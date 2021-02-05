using System;
using System.Text.Json.Serialization;
using PrivatePond.Data;

namespace PrivatePond.Controllers
{
    public class TransferRequestData
    {
        public string Id { get; set; }
        public string Destination { get; set; }
        public decimal Amount { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TransferStatus Status { get; set; }
        public string TransactionHash { get; set; }
    }
}