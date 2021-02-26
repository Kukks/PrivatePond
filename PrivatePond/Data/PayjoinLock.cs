using System;
using System.ComponentModel.DataAnnotations;

namespace PrivatePond.Data
{
    public class PayjoinLock
    {
        [Key]
        [MaxLength(100)]
        public string Id { get; set; }

        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    }
}