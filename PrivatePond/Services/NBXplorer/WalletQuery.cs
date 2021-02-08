namespace PrivatePond.Controllers
{
    public class WalletQuery
    {
        /// <summary>
        /// list of wallet ids to filter with
        /// </summary>
        public string[] Ids { get; set; }
        /// <summary>
        /// filter based on wallets being enabled/disabled. Leave blank for both
        /// </summary>
        public bool? Enabled { get; set; }
    }
}