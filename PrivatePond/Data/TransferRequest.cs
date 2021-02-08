using System;
using System.ComponentModel.DataAnnotations.Schema;
using PrivatePond.Data.EF;

namespace PrivatePond.Data
{
    public class TransferRequest
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public string Id { get; set; }
        public TransferStatus Status { get; set; }
        public decimal Amount { get; set; }
        public string Destination { get; set; }
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
        public TransferType TransferType { get; set; }
        public string SigningRequestId { get; set; }

        public SigningRequest SigningRequest { get; set; }
        public string ToWalletId { get; set; }
        public string FromWalletId { get; set; }
    }
}