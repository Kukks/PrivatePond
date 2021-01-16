using System.Collections.Generic;

namespace PrivatePond.Controllers
{
    public class DepositRequestData
    {
        public List<DepositRequestDataItem> Items { get; set; }
        public class DepositRequestDataItem
        {
            public string Label { get; set; }
            public string Destination { get; set; }
            public string PaymentLink { get; set; }
        }
    }
}