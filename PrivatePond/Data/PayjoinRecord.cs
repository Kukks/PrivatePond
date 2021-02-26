namespace PrivatePond.Data
{
    public class PayjoinRecord
    {
        public string Id { get; set; }
        public string OriginalTransactionId { get; set; }
        public string DepositRequestId { get; set; }
        public decimal? DepositContributedAmount { get; set; }
    }
}