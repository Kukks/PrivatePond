using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBXplorer;
using PrivatePond.Data.EF;

namespace PrivatePond.Controllers
{
    public class SigningRequestService
    {
        private readonly IDbContextFactory<PrivatePondDbContext> _contextFactory;
        private readonly Network _network;
        private readonly ILogger<SigningRequestService> _logger;
        private readonly ExplorerClient _explorerClient;

        public SigningRequestService(IDbContextFactory<PrivatePondDbContext> contextFactory, Network network,
            ILogger<SigningRequestService> logger, ExplorerClient explorerClient)
        {
            _contextFactory = contextFactory;
            _network = network;
            _logger = logger;
            _explorerClient = explorerClient;
        }


        public async Task<string> SubmitSignedPSBT(string signingRequestId, PSBT signedPSBT)
        {
            await using var context = _contextFactory.CreateDbContext();
            var signingRequest = await context.SigningRequests.FindAsync(signingRequestId);
            if (signingRequest is null)
            {
                return "Invalid signing request id";
            }

            if (signingRequest.Status is not SigningRequest.SigningRequestStatus.Pending)
            {
                return "Only a pending signing request can be signed";
            }

            if (signingRequest.PSBT == signedPSBT.ToBase64())
            {
                return "The PSBT was not signed";
            }

            if (signingRequest.SigningRequestItems.Any(item => item.SignedPSBT == signedPSBT.ToBase64()))
            {
                return "The signing request has already been signed by this signer";
            }

            if (!signedPSBT.Inputs.Any(input => input.PartialSigs.Any() || input.FinalScriptWitness != null))
            {
                return "The PSBT was not signed";
            }

            var newSigningRequestItem = new SigningRequestItem()
            {
                SigningRequestId = signingRequestId,
                SignedPSBT = signedPSBT.ToBase64()
            };
            signingRequest.SigningRequestItems.Add(newSigningRequestItem);
            if (signingRequest.RequiredSignatures <= signingRequest.SigningRequestItems.Count())
            {
                await _explorerClient.WaitServerStartedAsync();
                //signed!
                var psbts = signingRequest.SigningRequestItems.Select(item => PSBT.Parse(item.SignedPSBT, _network));
                var combined = psbts.Aggregate((p1, p2) => p1.Combine(p2));
                if (combined.TryFinalize(out var errors))
                {
                    signingRequest.FinalPSBT = combined.ToBase64();
                    signingRequest.Status = SigningRequest.SigningRequestStatus.Signed;
                    var bResult = await _explorerClient.BroadcastAsync(combined.ExtractTransaction());
                    if (bResult.Success)
                    {
                        _logger.LogInformation($"Signing request {signingRequestId} has been signed and broadcast!");
                    }
                    else
                    {
                        var error =
                            $"Could not broadcast signing request signed psbt for id {signingRequestId} because: {bResult.RPCCodeMessage}";
                        _logger.LogWarning(error);

                        signingRequest.Status = SigningRequest.SigningRequestStatus.Failed;
                        await context.SaveChangesAsync();
                        return error;
                    }
                    
                }
                else
                {
                    var error =
                        $"Could not finalize signing request psbt for id {signingRequestId} because: {(string.Join(',', errors))}";
                    _logger.LogWarning(error);

                    signingRequest.Status = SigningRequest.SigningRequestStatus.Failed;
                    await context.SaveChangesAsync();
                    return error;
                }
            }

            await context.SaveChangesAsync();
            return null;
        }

        public async Task<List<SigningRequest>> GetSigningRequests(SigningRequestQuery query)
        {
            await using var context = _contextFactory.CreateDbContext();
            var queryable = context.SigningRequests.Include(request => request.SigningRequestItems).AsQueryable();

            if (query.Status is not null)
            {
                queryable = queryable.Where(request => query.Status.Contains(request.Status));
            }

            return await queryable.ToListAsync();
        }
    }
}