using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace PrivatePond.Data.EF
{
    public class SigningRequestItem
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public string Id { get; set; }

        public string SigningRequestId { get; set; }

        public string SignedPSBT { get; set; }
[JsonIgnore]
        public SigningRequest SigningRequest { get; set; }
    }
}