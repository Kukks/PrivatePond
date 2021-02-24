using System.ComponentModel.DataAnnotations.Schema;

namespace PrivatePond.Data.EF
{
    public class SigningRequestItem
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public string Id { get; set; }

        public string SigningRequestId { get; set; }

        public string SignedPSBT { get; set; }

        public string SignerId { get; set; }

        public SigningRequest SigningRequest { get; set; }
    }
}