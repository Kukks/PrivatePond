using System;

namespace PrivatePond.Data
{
    public class ScheduledTransaction
    {
        public string Id { get; set; }
        public string Transaction { get; set; }
        public DateTimeOffset BroadcastAt { get; set; }
        public string ReplacesSigningRequestId { get; set; }
    }
}