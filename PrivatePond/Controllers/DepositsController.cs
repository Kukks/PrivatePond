using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;

namespace PrivatePond.Controllers
{
    [Route("api/v1/deposits")]
    public class DepositsController : ControllerBase
    {
        private readonly DepositService _depositService;

        public DepositsController(DepositService depositService)
        {
            _depositService = depositService;
        }

        [HttpGet("")]
        public async Task<ActionResult<List<DepositRequestData>>> GetDepositRequests(DepositRequestQuery depositRequestQuery)
        {
            return Ok(await _depositService.GetDepositRequests(depositRequestQuery, CancellationToken.None));
        }
        
        [HttpGet("users/{userId}")]
        public async Task<ActionResult<List<DepositRequestData>>> GetUserDepositRequest(string userId)
        {
            var result = await _depositService.GetOrGenerateDepositRequest(userId);
            if (result is null)
            {
                return NotFound();
            }

            return result;
        }

        [HttpGet("users/{userId}/history")]
        public async Task<ActionResult<List<DepositRequestData>>> GetDepositRequestHistory(string userId)
        {
            return Ok(await _depositService.GetDepositRequests(new DepositRequestQuery()
            {
                UserIds = new[] {userId},
                IncludeWalletTransactions = true
            }, CancellationToken.None));
        }
    }
}