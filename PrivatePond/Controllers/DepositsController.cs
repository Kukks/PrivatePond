using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

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

        /// <summary>
        /// Get Deposit Requests
        /// </summary>
        /// <param name="depositRequestQuery">Filters</param>
        /// <returns></returns>
        [HttpGet("")]
        public async Task<ActionResult<List<DepositRequestData>>> GetDepositRequests(DepositRequestQuery depositRequestQuery)
        {
            return Ok(await _depositService.GetDepositRequests(depositRequestQuery, CancellationToken.None));
        }
        
        /// <summary>
        /// Get or generate deposit requests for user 
        /// </summary>
        /// <remarks>Fetches active deposit requests for a user. If there are no active ones, they will be generated.</remarks>
        /// <param name="userId">The user id. It is normalized by lowercased and preceding and trailing white spaces trimmed.</param>
        /// <returns>A list of deposit requests available for the user. Sorted based on order in config.</returns>
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
        /// <summary>
        /// Get or deposit request history for user 
        /// </summary>
        /// <remarks>Fetches all deposit requests for a user.</remarks>
        /// <param name="userId">The user id. It is normalized by lowercased and preceding and trailing white spaces trimmed.</param>
        /// <returns>A list of deposit requests of the user. Sorted based on creation date in descending order.</returns>
        [HttpGet("users/{userId}/history")]
        public async Task<ActionResult<List<DepositRequestData>>> GetDepositRequestHistory(string userId)
        {
            return Ok(await _depositService.GetDepositRequests(new DepositRequestQuery()
            {
                UserIds = new[] {userId},
                IncludeWalletTransactions = true
            }, CancellationToken.None).ContinueWith(task => task.Result.OrderByDescending(request => request.Timestamp)));
        }
    }
}