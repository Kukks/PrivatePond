using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace PrivatePond.Controllers
{
    [Route("api/v1/users")]
    public class UsersController : ControllerBase
    {
        private readonly UserService _userService;
        private readonly DepositService _depositService;

        public UsersController(UserService userService, DepositService depositService)
        {
            _userService = userService;
            _depositService = depositService;
        }

        [HttpPost("")]
        public async Task<UserData> Create()
        {
            return await _userService.CreateUser();
        }

        [HttpGet("")]
        public async Task<ActionResult<List<UserData>>> List(int skip = 0, int take = int.MaxValue)
        {
            return await _userService.GetUsers(skip, take);
        }

        [HttpPost("")]
        [HttpGet("{userId}/deposit")]
        public async Task<ActionResult<DepositRequestData>> GetDepositRequest(string userId)
        {
            var result = await _depositService.GetOrGenerateDepositRequest(userId);
            if (result is null)
            {
                return NotFound();
            }

            return result;
        }

        [HttpGet("{userId}/deposit/history")]
        public async Task<DepositRequestData> GetDepositRequestHistory(string userId)
        {
            return await _depositService.GetDepositRequestUserHistory(userId);
        }
    }
}