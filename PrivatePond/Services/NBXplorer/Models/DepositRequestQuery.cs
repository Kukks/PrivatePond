using System.Text.Json.Serialization;

namespace PrivatePond.Controllers
{
    public class DepositRequestQuery
    {
        /// <summary>
        /// Retrieve only active/inactive deposit requests. Leave null for both
        /// </summary>
        public bool? Active { get; set; }
        /// <summary>
        /// Include the history of transactions associated with this deposit request
        /// </summary>
        public bool IncludeWalletTransactions { get; set; }

        [JsonIgnore]
        /// <summary>
        /// Include the history of transactions associated with this deposit request
        /// </summary>
        public bool IncludePayjoinRecords { get; set; } = true;
        /// <summary>
        /// Filter based on wallet id.
        /// </summary>
        public string[] WalletIds { get; set; }
        /// <summary>
        /// Filter based on deposit request ids
        /// </summary>
        public string[] Ids { get; set; }
        
        /// <summary>
        /// Filter based on users
        /// </summary>
        public string[] UserIds { get; set; }

        /// <summary>
        /// Filter based on addresses
        /// </summary>
        public string[] Address { get; set; }
    }
}