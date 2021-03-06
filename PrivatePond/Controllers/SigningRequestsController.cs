using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using PrivatePond.Controllers.Filters;
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

        /// <summary>
        /// submit a signed PSBT by one or more signers. PSBT should be in base64 format sent as a raw string to the body. text/plain media type header
        /// </summary>
        /// <param name="signingRequestId"></param>
        /// <returns></returns>
        [HttpPost("{signingRequestId}")]
        [MediaTypeConstraint("text/plain")]
        public async Task<IActionResult> SignRequest(string signingRequestId)
        {
            
            string rawBody;
            using (StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                rawBody = (await reader.ReadToEndAsync());
            }
            if (!PSBT.TryParse(rawBody, _network, out var psbt))
            {
                ModelState.AddModelError("", "psbt was in an invalid format");
                return BadRequest(ModelState);
            }

            ;
            var errorMessage = await _signingRequestService.SubmitSignedPSBT(signingRequestId, psbt);
            if (string.IsNullOrEmpty(errorMessage))
            {
                return Ok();
            }

            ModelState.AddModelError("", errorMessage);
            return BadRequest(ModelState);
        }
    }
}