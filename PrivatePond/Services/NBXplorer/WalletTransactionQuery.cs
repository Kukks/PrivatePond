using System.Text.Json.Serialization;
using PrivatePond.Data;

namespace PrivatePond.Controllers
{
    public class WalletTransactionQuery
    {
        
        public bool IncludeWallet { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public WalletTransaction.WalletTransactionStatus[] Statuses { get; set; }= null;
        /// <summary>
        /// list of ids to filter with
        /// </summary>
        public string[] Ids { get; set; } = null;
        /// <summary>
        /// list of wallet ids to filter with
        /// </summary>
        public string[] WalletIds { get; set; } = null;
        public int? Skip { get; set; }
        public int? Take { get; set; }
    }
}