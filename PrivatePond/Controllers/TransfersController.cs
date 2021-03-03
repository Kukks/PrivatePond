using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
using NBitcoin;
using PrivatePond.Data;

namespace PrivatePond.Controllers
{
    [Route("api/v1/transfers")]
    public class TransfersController : ControllerBase
    {
        private readonly TransferRequestService _transferRequestService;
        private readonly Network _network;
        private readonly IOptions<PrivatePondOptions> _options;

        public TransfersController(TransferRequestService transferRequestService, Network network,
            IOptions<PrivatePondOptions> options)
        {
            _transferRequestService = transferRequestService;
            _network = network;
            _options = options;
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
                            out var bip21Amount, out _);

                    if (bip21Amount.HasValue && request.Amount.HasValue && request.Amount != bip21Amount.Value && bip21Amount.Value != 0)
                    {
                        ModelState.AddModelError((RequestTransferRequest x) => x.Amount,
                            "An amount was specified for this transfer but the destination is a payment link with a different amount");
                    }
                    else if (bip21Amount.HasValue && bip21Amount.Value > 0)
                    {
                        request.Amount = bip21Amount;
                    }

                    if (request.Amount <= 0)
                    {
                        ModelState.AddModelError((RequestTransferRequest x) => x.Amount,
                            "An amount greater than 0 must be specified or needs to be present in the payment link");
                    }

                    if (request.Express && !_options.Value.EnableExternalExpressTransfers)
                    {
                        ModelState.AddModelError((RequestTransferRequest x) => x.Amount,
                            "Express option is disabled.");
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

            var result = await _transferRequestService.CreateTransferRequest(request);
            if (result is not null)
                return Ok(result);
            return BadRequest("Could not create transfer.");
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