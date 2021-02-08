using System.Text.Json.Serialization;
using PrivatePond.Data;

namespace PrivatePond.Controllers
{
    public class TransferRequestQuery
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TransferStatus[] Statuses { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TransferType[] TransferTypes { get; set; }
        public string[] Ids { get; set; }
    }
}