using System.ComponentModel.DataAnnotations;

namespace PrivatePond.Controllers
{
    public class RequestTransferRequest
    {
        [Required]
        public string Destination { get; set; }
        public decimal? Amount { get; set; }
        public string UserId { get; set; }
    }
}