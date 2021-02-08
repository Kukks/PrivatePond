namespace PrivatePond.Controllers
{
    public class WalletData
    {
        /// <summary>
        /// Whether the wallet is enabled or not. Only disabled if it was created in a previous run but not present currently in the config
        /// </summary>
        public bool Enabled { get; set; }
        /// <summary>
        /// the curret balance of the wallet
        /// </summary>
        public decimal Balance { get; set; }
        /// <summary>
        /// the id of the wallet.
        /// </summary>
        public string Id { get; set; }
    }
}