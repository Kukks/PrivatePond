using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using NBitcoin;

namespace PrivatePond.Controllers
{
    [Route("api/v1/transfers")]
    public class TransfersController : ControllerBase
    {
        private readonly TransferRequestService _transferRequestService;
        private readonly Network _network;

        public TransfersController(TransferRequestService transferRequestService, Network network)
        {
            _transferRequestService = transferRequestService;
            _network = network;
        }

        /// <summary>
        /// Get Transfer requests
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        [HttpGet("")]
        public async Task<ActionResult<List<TransferRequestData>>> GetTransferRequests(TransferRequestQuery query)
        {
            return Ok(await _transferRequestService.GetTransferRequests(query));
        }

        /// <summary>
        /// Create a transfer request
        /// </summary>
        /// <param name="request"></param>
        /// <returns>The created request</returns>
        [HttpPost("")]
        public async Task<ActionResult<TransferRequestData>> RequestTransfer(RequestTransferRequest request)
        {
            if (!string.IsNullOrEmpty(request.Destination))
            {
                try
                {
                    var address =
                        HelperExtensions.GetAddress(request.Destination, _network, out var scriptPubKeyType,
                            out var bip21Amount);

                    if (bip21Amount.HasValue && request.Amount.HasValue && request.Amount != bip21Amount.Value)
                    {
                        ModelState.AddModelError((RequestTransferRequest x) => x.Amount,
                            "An amount was specified for this transfer but the destination is a payment link with a different amount");
                    }
                    else if (bip21Amount.HasValue)
                    {
                        request.Amount = bip21Amount;
                    }
                }
                catch (Exception e)
                {
                    ModelState.AddModelError((RequestTransferRequest x) => x.Destination,
                        "Destination was invalid. It must be a bitcoin address or a BIP21 payment link");
                }
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            return Ok(await _transferRequestService.CreateTransferRequest(request));
        }

        /// <summary>
        /// Get a transfer request
        /// </summary>
        /// <param name="transferRequestId">the specific transfer request Id</param>
        /// <returns></returns>
        [HttpGet("{transferRequestId}")]
        public async Task<ActionResult<TransferRequestData>> GetTransferRequestId(string transferRequestId)
        {
            var result = await _transferRequestService.GetTransferRequests(new TransferRequestQuery()
            {
                Ids = new[] {transferRequestId}
            });

            if (!result.Any())
            {
                return NotFound();
            }

            return Ok(result.First());
        }
    }
}