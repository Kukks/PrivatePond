namespace PrivatePond.Controllers
{
    public class DepositRequestQuery
    {
        public bool? Active { get; set; }
        public bool IncludeWalletTransactions { get; set; }
        public string[] WalletIds { get; set; }
        public string[] Ids { get; set; }
        public string[] UserIds { get; set; }
    }
}