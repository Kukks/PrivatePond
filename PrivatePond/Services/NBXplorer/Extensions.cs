using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NBitcoin;
using NBXplorer;
using NBXplorer.DerivationStrategy;
using PrivatePond.Controllers;
using PrivatePond.Data;

namespace PrivatePond.Services.NBXplorer
{
    public static class Extensions
    {
        public static IServiceCollection AddNBXPlorerIntegration(this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddOptions<NBXplorerOptions>()
                .Bind(configuration.GetSection(NBXplorerOptions.OptionsConfigSection));
            services.AddSingleton<NBXplorerNetworkProvider>(provider =>
            {
                var ppOptions = provider.GetRequiredService<IOptions<PrivatePondOptions>>();
                return new NBXplorerNetworkProvider(ppOptions.Value.NetworkType);
            });
            services.AddSingleton<DerivationStrategyFactory>(provider =>
            {
                var explorerClient = provider.GetRequiredService<ExplorerClient>();
                return new DerivationStrategyFactory(explorerClient.Network.NBitcoinNetwork);
            });

            services.AddSingleton<ExplorerClient>(provider =>
            {
                var nbxNetworkProvider = provider.GetRequiredService<NBXplorerNetworkProvider>();
                var logger = provider.GetRequiredService<ILogger<ExplorerClient>>();
                var nbxOptions = provider.GetRequiredService<IOptions<NBXplorerOptions>>();
                var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
                var cookieFile = nbxOptions.Value.CookieFile;
                if (string.IsNullOrEmpty(cookieFile?.Trim()) || cookieFile.Trim() == "0" )
                    cookieFile = null;
                if (nbxOptions.Value.ExplorerUri is null)
                {
                    throw new ConfigurationException("NBXPlorer", "NBXplorer connection string not configured");
                }
                logger.LogInformation($"Explorer url is {(nbxOptions.Value.ExplorerUri.AbsoluteUri)}");
                logger.LogInformation($"Cookie file is {(nbxOptions.Value.CookieFile ?? "not set")}");
                var explorer = nbxNetworkProvider.GetBTC().CreateExplorerClient(nbxOptions.Value.ExplorerUri);
                explorer.SetClient(httpClientFactory.CreateClient());
                if (cookieFile == null)
                {
                    logger.LogWarning($"{explorer.CryptoCode}: Not using cookie authentication");
                    explorer.SetNoAuth();
                }else if (!explorer.SetCookieAuth(cookieFile))
                {
                    logger.LogWarning(
                        $"{explorer.CryptoCode}: Using cookie auth against NBXplorer, but {cookieFile} is not found");
                }

                return explorer;
            });
            services.AddSingleton<Network>(provider => provider.GetRequiredService<ExplorerClient>().Network.NBitcoinNetwork);
            services.AddSingleton<NBXplorerSummaryProvider>();
            services.AddHostedService<NBXplorerListener>();
            services.AddSingleton<WalletService>();
            services.AddSingleton<IHostedService,WalletService>(provider => provider.GetRequiredService<WalletService>());
            return services;
        }
    }
}