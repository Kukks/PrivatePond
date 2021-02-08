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
    }
}