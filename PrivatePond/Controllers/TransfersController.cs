using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace PrivatePond.Controllers
{
    [Route("api/v1/transfers")]
    public class TransfersController : ControllerBase 
    {
        
        [HttpGet("{userId}")]
        public async Task<ActionResult<DepositRequestData>> RequestTransfer(RequestTransferRequest request)
        {
            throw new NotSupportedException();
        }
    }
}