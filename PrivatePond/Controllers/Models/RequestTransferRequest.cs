using System.ComponentModel.DataAnnotations;

namespace PrivatePond.Controllers
{
    public class RequestTransferRequest
    {
        /// <summary>
        /// A bitcoin address or a BIP21 payment link
        /// </summary>
        [Required]
        public string Destination { get; set; }
        /// <summary>
        /// The amount for transfer. If a BIP21 payment link is provided in `Destination`, leave Amount as null or of equal value
        /// </summary>
        public decimal? Amount { get; set; }
        /// <summary>
        /// if true, and feature is enabled through config, sends the payment instantly in its own dedicated transaction.
        /// </summary>
        public bool Express { get; set; }
    }
}