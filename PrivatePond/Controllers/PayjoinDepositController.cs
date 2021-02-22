using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PrivatePond.Controllers.Filters;

namespace PrivatePond.Controllers
{
    public class PayjoinDepositController : Controller
    {
        [HttpPost("~/pj")]
        [IgnoreAntiforgeryToken]
        [MediaTypeConstraint("text/plain")]
        public async Task<IActionResult> SubmitPayjoinDeposit()
        {
            return BadRequest();
        }
    }
}