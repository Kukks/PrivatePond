using System.Collections.Generic;

namespace PrivatePond.Data
{
    public class User
    {
        public string Id { get; set; }
        public List<DepositRequest> DepositRequests { get; set; }
    }
}