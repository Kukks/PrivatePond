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

        [HttpGet("{userId}")]
        public async Task<ActionResult<DepositRequestData>> GetDepositRequest(string userId)
        {
            var result = await _depositService.GetOrGenerateDepositRequest(userId);
            if (result is null)
            {
                return NotFound();
            }

            return result;
        }

        [HttpGet("{userId}/history")]
        public async Task<DepositRequestData> GetDepositRequestHistory(string userId)
        {
            return await _depositService.GetDepositRequestUserHistory(userId);
        }
    }
}