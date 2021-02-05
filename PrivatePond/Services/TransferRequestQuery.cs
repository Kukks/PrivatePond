using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PrivatePond.Data;

namespace PrivatePond.Controllers
{
    public class TransferRequestQuery
    {
        [System.Text.Json.Serialization.JsonConverter(typeof(JsonStringEnumConverter))]
        public TransferStatus[] Statuses { get; set; }
        [System.Text.Json.Serialization.JsonConverter(typeof(JsonStringEnumConverter))]
        public TransferType[] TransferTypes { get; set; }
        public string[] Ids { get; set; }
    }
}