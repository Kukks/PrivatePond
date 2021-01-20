using System;
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

            public List<DepositRequestDataItemPaymentItem> History { get; set; }
        }

        public class DepositRequestDataItemPaymentItem
        {
            public string TransactionId { get; set; }
            public decimal Value { get; set; }
            public DateTimeOffset Timestamp { get; set; }
            public bool Confirmed { get; set; }
        }
    }
}