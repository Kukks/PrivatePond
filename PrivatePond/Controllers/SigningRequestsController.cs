using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using PrivatePond.Data.EF;

namespace PrivatePond.Controllers
{
    [Route("api/v1/signing-requests")]
    public class SigningRequestsController : ControllerBase
    {
        private readonly SigningRequestService _signingRequestService;
        private readonly Network _network;

        public SigningRequestsController(SigningRequestService signingRequestService, Network network)
        {
            _signingRequestService = signingRequestService;
            _network = network;
        }

        /// <summary>
        /// gets a list of signing requests based on provided filters
        /// </summary>
        /// <returns></returns>
        [HttpGet("")]
        public async Task<List<SigningRequest>> ListSigningRequests(SigningRequestQuery query)
        {
            return await _signingRequestService.GetSigningRequests(query);
        }

        [HttpPost]
        public async Task<IActionResult> SignRequest(string signingRequestId, string signedPSBT)
        {
            if (!PSBT.TryParse(signedPSBT, _network, out var psbt))
            {
                ModelState.AddModelError(nameof(signedPSBT), "psbt was in an invalid format");
                return BadRequest(ModelState);
            }

            ;
            var errorMessage = await _signingRequestService.SubmitSignedPSBT(signingRequestId, psbt);
            if (string.IsNullOrEmpty(errorMessage))
            {
                return Ok();
            }

            ModelState.AddModelError(nameof(signedPSBT), errorMessage);
            return BadRequest(ModelState);
        }
    }
}