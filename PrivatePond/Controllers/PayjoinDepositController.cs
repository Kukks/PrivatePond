using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using BTCPayServer.BIP78.Receiver;
using BTCPayServer.BIP78.Sender;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NBitcoin;
using NBXplorer.DerivationStrategy;
using PrivatePond.Controllers.Filters;
using PrivatePond.Data;
using PrivatePond.Data.EF;

namespace PrivatePond.Controllers
{
    public class PayjoinDepositController : Controller
    {
        private readonly IOptions<PrivatePondOptions> _options;
        private readonly PayjoinReceiverWallet _payjoinReceiverWaller;
        private readonly Network _network;

        public PayjoinDepositController(
            IOptions<PrivatePondOptions> options,
            PayjoinReceiverWallet payjoinReceiverWaller,
            Network network)
        {
            _options = options;
            _payjoinReceiverWaller = payjoinReceiverWaller;
            _network = network;
        }

        [HttpPost("~/pj")]
        [IgnoreAntiforgeryToken]
        [MediaTypeConstraint("text/plain")]
        public async Task<IActionResult> SubmitPayjoinDeposit(
            long? maxadditionalfeecontribution,
            int? additionalfeeoutputindex,
            decimal minfeerate = -1.0m,
            bool disableoutputsubstitution = false,
            int v = 1)
        {
            if (!_options.Value.EnablePayjoinDeposits)
            {
                return NotFound();
            }

            string rawBody;
            using (StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                rawBody = (await reader.ReadToEndAsync());
            }

            if (!PSBT.TryParse(rawBody, _network, out var psbt))
            {
                return BadRequest(CreatePayjoinError("original-psbt-rejected", "invalid transaction or psbt"));
            }

            var ctx = new PrivatePondPayjoinProposalContext(psbt, new PayjoinClientParameters()
            {
                Version = v,
                DisableOutputSubstitution = disableoutputsubstitution,
                MinFeeRate = minfeerate >= 0.0m ? new FeeRate(minfeerate) : null,
                MaxAdditionalFeeContribution =
                    Money.Satoshis(maxadditionalfeecontribution is long t && t >= 0 ? t : 0),
                AdditionalFeeOutputIndex = additionalfeeoutputindex
            });
            try
            {
                await _payjoinReceiverWaller.Initiate(ctx);

                return Ok(ctx.PayjoinReceiverWalletProposal.PayjoinPSBT.ToBase64());
            }
            catch (PayjoinReceiverException e)
            {
                return BadRequest(CreatePayjoinError(e.ErrorCode, e.ReceiverMessage));
            }
        }

        private dynamic CreatePayjoinError(string errorCode, string friendlyMessage)
        {
            return new
            {
                errorCode = errorCode,
                message = friendlyMessage
            };
        }
    }

    public class PrivatePondPayjoinProposalContext : PayjoinProposalContext
    {
        public Dictionary<string, Coin[]> WalletUTXOS;
        public Dictionary<string, DerivationStrategyBase> HotWallets { get; set; }
        public DepositRequest? DepositRequest { get; set; }

        public PrivatePondPayjoinProposalContext(PSBT originalPSBT,
            PayjoinClientParameters payjoinClientParameters = null) : base(originalPSBT, payjoinClientParameters)
        {
        }
    }
}