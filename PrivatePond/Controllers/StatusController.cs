using Microsoft.AspNetCore.Mvc;
using PrivatePond.Services.NBXplorer;

namespace PrivatePond.Controllers
{
    [Route("api/v1/status")]
    public class StatusController : Controller
    {
        private readonly NBXplorerSummaryProvider _nbXplorerSummaryProvider;

        public StatusController(NBXplorerSummaryProvider nbXplorerSummaryProvider)
        {
            _nbXplorerSummaryProvider = nbXplorerSummaryProvider;
        }
        [HttpGet]
        public IActionResult Index()
        {
            return Ok(_nbXplorerSummaryProvider.LastSummary);
        }
    }
}