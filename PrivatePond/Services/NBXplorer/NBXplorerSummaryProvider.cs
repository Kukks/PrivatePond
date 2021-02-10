using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NBXplorer;
using NBXplorer.Models;
using PrivatePond.Data;

namespace PrivatePond.Services.NBXplorer
{
    public class NBXplorerSummaryProvider
    {
        private readonly ILogger<NBXplorerSummaryProvider> _logger;
        private readonly ExplorerClient _explorerClient;

        public NBXplorerSummary LastSummary
        {
            get { return _lastSummary; }
            private set
            {
                _lastSummary = value;
                SummaryUpdated.InvokeAsync();
            }
        }

        private EventCallback SummaryUpdated;
        private readonly IOptions<PrivatePondOptions> _options;
        private NBXplorerSummary _lastSummary;

        public NBXplorerSummaryProvider(IOptions<PrivatePondOptions> options, ILogger<NBXplorerSummaryProvider> logger,
            ExplorerClient explorerClient)
        {
            _options = options;
            _logger = logger;
            _explorerClient = explorerClient;
        }

        public async Task UpdateClientState(CancellationToken cancellation)
        {
            if (LastSummary is null || !string.IsNullOrEmpty(LastSummary.Error) ||
                LastSummary.State != NBXplorerState.Ready)
                _logger.LogInformation($"Updating summary for {_explorerClient.CryptoCode}");
            var state = (NBXplorerState?) null;
            string error = null;
            StatusResult status = null;
            try
            {
                status = await _explorerClient.GetStatusAsync(cancellation);
                if (status == null)
                {
                    state = NBXplorerState.NotConnected;
                }
                else if (status.IsFullySynched)
                {
                    state = NBXplorerState.Ready;
                }
                else if (!status.IsFullySynched)
                {
                    state = NBXplorerState.Synching;
                }
            }
            catch (Exception ex) when (!cancellation.IsCancellationRequested)
            {
                _logger.LogWarning($"Could not update summary for {_explorerClient.CryptoCode} because {ex.Message}");
                error = ex.Message;
            }

            if (status != null && error == null && status.NetworkType != _options.Value.NetworkType)
            {
                error =
                    $"{_explorerClient.CryptoCode}: NBXplorer is on a different ChainType (actual: {status.NetworkType}, expected: {_options.Value.NetworkType})";
            }

            if (error != null)
            {
                state = NBXplorerState.NotConnected;
            }

            var summary = new NBXplorerSummary()
            {
                Status = status,
                State = state.GetValueOrDefault(NBXplorerState.NotConnected),
                Error = error
            };
            if (LastSummary is null || LastSummary.Error != summary.Error || LastSummary.State != summary.State)
                _logger.LogInformation($"summary updated {_explorerClient.CryptoCode} ({summary.State})");
            LastSummary = summary;
        }
    }
}