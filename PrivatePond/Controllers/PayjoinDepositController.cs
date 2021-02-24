using System;
using System.Threading.Tasks;
using BIP78.Receiver;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NBitcoin;
using NBXplorer;
using PrivatePond.Controllers.Filters;
using PrivatePond.Data;
using PrivatePond.Data.EF;

namespace PrivatePond.Controllers
{
    public class PayjoinDepositController : Controller
    {
        private readonly TransferRequestService _transferRequestService;
        private readonly IOptions<PrivatePondOptions> _options;

        public PayjoinDepositController(TransferRequestService transferRequestService, IOptions<PrivatePondOptions> options)
        {
            _transferRequestService = transferRequestService;
            _options = options;
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
            
            if (v != 1)
            {
                return BadRequest(new {
                    errorCode="version-unsupported",
                    supported = new []{1},
                    message = "This version of payjoin is not supported."
                });
            }
            
            return BadRequest();
        }
    }

    public class PayjoinReceiverWaller : PayjoinReceiverWallet<PayjoinProposalContext>
    {
        private readonly ExplorerClient _explorerClient;
        private readonly Network _network;
        private readonly TransactionBroadcasterService _transactionBroadcasterService;

        public PayjoinReceiverWaller(ExplorerClient explorerClient, Network network, TransactionBroadcasterService transactionBroadcasterService )
        {
            _explorerClient = explorerClient;
            _network = network;
            _transactionBroadcasterService = transactionBroadcasterService;
        }
        protected override Task<bool> SupportsType(ScriptPubKeyType scriptPubKeyType)
        {
            return Task.FromResult(scriptPubKeyType != ScriptPubKeyType.Legacy);
        }

        protected override Task<bool> InputsSeenBefore(PSBTInputList inputList)
        {
            throw new NotImplementedException();
        }

        protected override async Task<string> IsMempoolEligible(PSBT psbt)
        {
            var result = await _explorerClient.BroadcastAsync(psbt.ExtractTransaction(), true);
            return result.Success ? null : result.RPCCodeMessage;
        }

        protected override async Task BroadcastOriginalTransaction(PayjoinProposalContext context, TimeSpan scheduledTime)
        {
            if (scheduledTime == TimeSpan.Zero)
            {
                await _explorerClient.BroadcastAsync(context.OriginalTransaction);
                return;
            }

            await _transactionBroadcasterService.Schedule(new ScheduledTransaction()
            {
                BroadcastAt = DateTimeOffset.UtcNow.Add(scheduledTime),
                Id = context.OriginalTransaction.GetHash().ToString(),
                Transaction = context.OriginalTransaction.ToHex()
            });
        }

        protected override Task ComputePayjoinModifications(PayjoinProposalContext context)
        {
            throw new NotImplementedException();
        }

        protected override Task<PayjoinPaymentRequest> FindMatchingPaymentRequests(PayjoinProposalContext psbt)
        {
            throw new NotImplementedException();
        }
    }
}