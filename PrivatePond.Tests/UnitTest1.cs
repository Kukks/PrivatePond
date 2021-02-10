using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PrivatePond.Data.EF;
using Xunit;

namespace PrivatePond.Tests
{
    public class CustomWebApplicationFactory<TStartup>
        : WebApplicationFactory<TStartup> where TStartup : class
    {

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // builder.ConfigureAppConfiguration(configurationBuilder =>
            //     {
            //         configurationBuilder.Sources.Clear();
            //         configurationBuilder.AddInMemoryCollection(Configuration);
            //     }
            // );

            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType ==
                         typeof(IDbContextFactory<PrivatePondDbContext>));
                
                services.Remove(descriptor);
                
                services.AddDbContextFactory<PrivatePondDbContext>(options =>
                {
                    options.UseInMemoryDatabase("InMemoryDbForTesting");
                });

                var sp = services.BuildServiceProvider();

                using (var scope = sp.CreateScope())
                {
                    var scopedServices = scope.ServiceProvider;
                    var db = scopedServices.GetRequiredService<IDbContextFactory<PrivatePondDbContext>>();

                    db.CreateDbContext().Database.EnsureCreated();
                }
                
            });
        }
    }

    public class BasicTests
        : IClassFixture<WebApplicationFactory<Startup>>
    {
        private readonly WebApplicationFactory<Startup> _factory;

        public BasicTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task ConfigurationTests()
        {
            (WebApplicationFactory<Startup>, HttpClient) Create(Dictionary<string, string> config)
            {
                var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(
                    configurationBuilder =>
                    {
                        configurationBuilder.AddInMemoryCollection(config);
                    }));
                var client = factory.CreateClient();

                return (factory, client);
            }

            Assert.Throws<ConfigurationException>(() =>
            {
                Create(new Dictionary<string, string>());
            });
            Assert.Throws<ConfigurationException>(() =>
            {
                Create(new Dictionary<string, string>()
                {
                    {"PrivatePond:Wallets:0:DerivationScheme", "tpubDCZB6sR48s4T5Cr8qHUYSZEFCQMMHRg8AoVKVmvcAP5bRw7ArDKeoNwKAJujV3xCPkBvXH5ejSgbgyN6kREmF7sMd41NdbuHa8n1DZNxSMg"},
                    {"PrivatePond:Wallets:0:AllowForDeposits", "true"},
                    {"PrivatePond:Wallets:0:RootedKeyPaths:0", "5c9e228d/m/84'/1'/0'"},
                });
            });
            //default is mainnet, so a tpub wallet will not work
            Assert.Throws<ConfigurationException>(() =>
            {
                 Create(new Dictionary<string, string>()
                {
                    {"PrivatePond:Wallets:0:DerivationScheme", "tpubDCZB6sR48s4T5Cr8qHUYSZEFCQMMHRg8AoVKVmvcAP5bRw7ArDKeoNwKAJujV3xCPkBvXH5ejSgbgyN6kREmF7sMd41NdbuHa8n1DZNxSMg"},
                    {"PrivatePond:Wallets:0:AllowForDeposits", "true"},
                    {"PrivatePond:Wallets:0:RootedKeyPaths:0", "5c9e228d/m/84'/1'/0'"},
                    {"NBXPLORER:EXPLORERURI", "http://nbxplorer:65467"},
                
                });
            });
            var x = Create(new Dictionary<string, string>()
            {
                {"PrivatePond:Wallets:0:DerivationScheme", "tpubDCZB6sR48s4T5Cr8qHUYSZEFCQMMHRg8AoVKVmvcAP5bRw7ArDKeoNwKAJujV3xCPkBvXH5ejSgbgyN6kREmF7sMd41NdbuHa8n1DZNxSMg"},
                {"PrivatePond:Wallets:0:AllowForDeposits", "true"},
                {"PrivatePond:Wallets:0:RootedKeyPaths:0", "5c9e228d/m/84'/1'/0'"},
                {"PrivatePond:NetworkType", "regtest"},
                {"NBXPLORER:EXPLORERURI", "http://nbxplorer:65467"},
            });
           

        }
    }
}