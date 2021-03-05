using PrivatePond.Data.EF;

namespace PrivatePond.Controllers
{
    public class SigningRequestQuery
    {
        /// <summary>
        /// the status of the signing request to filter on (Pending,Signed,Expired)
        /// </summary>
        public SigningRequest.SigningRequestStatus[] Status { get; set; }
        
        /// <summary>
        /// the type of the signing request to filter on ( HotWallet,Replenishment,ExpressTransfer,ExpressTransferPayjoin,DepositPayjoin)
        /// </summary>
        public SigningRequest.SigningRequestType[] Type { get; set; } 
    }
}