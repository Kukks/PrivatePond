using System;
using System.Collections.Generic;

namespace PrivatePond.Controllers
{
    public class DepositRequestData
    {
        /// <summary>
        /// The wallet id that this deposit request is created for
        /// </summary>
        public string WalletId { get; set; }
        /// <summary>
        /// An identifier for this deposit request
        /// </summary>
        public string Label { get; set; }
        /// <summary>
        /// the bitcoin address
        /// </summary>
        public string Destination { get; set; }
        /// <summary>
        /// a payment link (BIP21) that can be read by Bitcoin wallets either via QR code or by an anchor tag
        /// </summary>
        public string PaymentLink { get; set; }

        /// <summary>
        /// Any transactions linked to this deposit request
        /// </summary>
        public List<DepositRequestDataItemPaymentItem> History { get; set; } =
            new List<DepositRequestDataItemPaymentItem>();
        /// <summary>
        /// user id linked to this deposit request
        /// </summary>
        public string UserId { get; set; }
        /// <summary>
        /// if the deposit request is active
        /// </summary>
        public bool Active { get; set; }
    }

    public class DepositRequestDataItemPaymentItem
    {
        /// <summary>
        /// the bitcoin transaction id
        /// </summary>
        public string TransactionId { get; set; }
        /// <summary>
        /// the amount deposited
        /// </summary>
        public decimal Value { get; set; }
        /// <summary>
        /// the value of the transaction output if a payjoin occurred
        /// </summary>
        public decimal? PayjoinValue { get; set; }
        //the time of payment
        public DateTimeOffset Timestamp { get; set; }
        //whether it was confirmed
        public bool Confirmed { get; set; }
    }
}