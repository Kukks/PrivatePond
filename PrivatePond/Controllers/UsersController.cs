using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace PrivatePond.Controllers
{
    [ApiController]
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

        [HttpGet("{userId}/deposit")]
        public async Task<DepositRequestData> GetDepositRequest(string userId)
        {
            return await _depositService.GetOrGenerateDepositRequest(userId);
        }
    }
}