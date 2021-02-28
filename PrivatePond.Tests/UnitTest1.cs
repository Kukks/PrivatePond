using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NBitcoin;
using NBitcoin.OpenAsset;
using NBitcoin.RPC;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PrivatePond.Controllers;
using PrivatePond.Data;
using PrivatePond.Data.EF;
using PrivatePond.Services.NBXplorer;
using Xunit;
using Xunit.Sdk;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace PrivatePond.Tests
{
    public class BasicTests
        : IClassFixture<WebApplicationFactory<Startup>>
    {
        private readonly WebApplicationFactory<Startup> _factory;

        public BasicTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        private (WebApplicationFactory<Startup>, HttpClient) Create(Dictionary<string, string> config,
            Action<IServiceCollection> additionalServiceConfig = null)
        {
            var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(
                configurationBuilder => { configurationBuilder.AddInMemoryCollection(config); }).ConfigureServices(
                collection =>
                {
                    additionalServiceConfig?.Invoke(collection);
                    Task.WaitAll(collection.BuildServiceProvider().GetServices<IStartupTask>()
                        .Select(task => task.ExecuteAsync()).ToArray());
                }));

            var client = factory.CreateClient();
            return (factory, client);
        }

        private (WebApplicationFactory<Startup>, HttpClient) CreateServerWithStandardSetup(PrivatePondOptions options,
            string dbName = null)
        {
            dbName ??= RandomDbName();


            return Create(new Dictionary<string, string>()
                {
                    {
                        "ConnectionStrings:PRIVATEPONDDATABASE",
                        $"User ID=postgres;Host=127.0.0.1;Port=65466;Database={dbName};persistsecurityinfo=True"
                    },
                    {"NBXPLORER:EXPLORERURI", "http://localhost:65467"},
                },
                collection =>
                {
                    collection.Configure<PrivatePondOptions>(pondOptions =>
                    {
                        JsonConvert.PopulateObject(
                            JsonSerializer.Serialize(options), pondOptions);
                    });
                });
        }


        [Fact]
        public async Task ConfigurationTests()
        {
            Assert.Throws<ConfigurationException>(() => { Create(new Dictionary<string, string>()); });
            Assert.Throws<ConfigurationException>(() =>
            {
                Create(new Dictionary<string, string>()
                {
                    {
                        "PrivatePond:Wallets:0:DerivationScheme",
                        "tpubDCZB6sR48s4T5Cr8qHUYSZEFCQMMHRg8AoVKVmvcAP5bRw7ArDKeoNwKAJujV3xCPkBvXH5ejSgbgyN6kREmF7sMd41NdbuHa8n1DZNxSMg"
                    },
                    {"PrivatePond:Wallets:0:AllowForDeposits", "true"},
                    {"PrivatePond:Wallets:0:RootedKeyPaths:0", "5c9e228d/m/84'/1'/0'"},
                });
            });
            //default is mainnet, so privatepond will not connect either
            var app = Create(new Dictionary<string, string>()
            {
                {
                    "PrivatePond:Wallets:0:DerivationScheme",
                    "tpubDCZB6sR48s4T5Cr8qHUYSZEFCQMMHRg8AoVKVmvcAP5bRw7ArDKeoNwKAJujV3xCPkBvXH5ejSgbgyN6kREmF7sMd41NdbuHa8n1DZNxSMg"
                },
                {"PrivatePond:Wallets:0:AllowForDeposits", "true"},
                {"PrivatePond:Wallets:0:RootedKeyPaths:0", "5c9e228d/m/84'/1'/0'"},
                {"NBXPLORER:EXPLORERURI", "http://localhost:65467"},
            });
            using (app.Item1)
            {
                var summaryProvider = app.Item1.Services.GetService<NBXplorerSummaryProvider>();
                await summaryProvider.UpdateClientState(CancellationToken.None);
                Assert.Equal(NBXplorerState.NotConnected, summaryProvider.LastSummary.State);
                Assert.Contains("different chain", summaryProvider.LastSummary.Error,
                    StringComparison.InvariantCultureIgnoreCase);
            }

            //let's run a new db and see if that works well
            app = Create(new Dictionary<string, string>()
            {
                {
                    "PrivatePond:Wallets:0:DerivationScheme",
                    "tpubDCZB6sR48s4T5Cr8qHUYSZEFCQMMHRg8AoVKVmvcAP5bRw7ArDKeoNwKAJujV3xCPkBvXH5ejSgbgyN6kREmF7sMd41NdbuHa8n1DZNxSMg"
                },
                {"PrivatePond:Wallets:0:AllowForDeposits", "true"},
                {"PrivatePond:Wallets:0:RootedKeyPaths:0", "5c9e228d/m/84'/1'/0'"},
                {"PrivatePond:NetworkType", "regtest"},
                {"NBXPLORER:EXPLORERURI", "http://localhost:65467"},
                {
                    "ConnectionStrings:PRIVATEPONDDATABASE",
                    $"User ID=postgres;Host=127.0.0.1;Port=65466;Database={RandomDbName()};persistsecurityinfo=True"
                }
            });
            using (app.Item1)
            {
                var summaryProvider = app.Item1.Services.GetService<NBXplorerSummaryProvider>();
                await summaryProvider.UpdateClientState(CancellationToken.None);
                Assert.Null(summaryProvider.LastSummary.Error);
                Assert.True(
                    Task.WaitAll(new[] {app.Item1.Services.GetService<WalletService>().WaitUntilWalletsLoaded()},
                        5000));
                var walletOptions = app.Item1.Services.GetService<IOptions<PrivatePondOptions>>().Value.Wallets;
                Assert.Equal(1, walletOptions.Length);
                Assert.True(walletOptions.All(option => !string.IsNullOrEmpty(option.WalletId)));
            }
        }

        private string RandomDbName()
        {
            return Guid.NewGuid().ToString().Replace("-", "");
        }

        private static RPCClient RpcClient = new RPCClient(new RPCCredentialString()
        {
            Server = "http://localhost:65468",
            UserPassword = new NetworkCredential("ceiwHEbqWI83", "DwubwWsoo3")
        }, "http://localhost:65468", Network.RegTest);

        private async Task<T> GetJson<T>(HttpResponseMessage message)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };
            message.EnsureSuccessStatusCode();
            return JsonSerializer.Deserialize<T>(await message.Content.ReadAsStringAsync(), options);
        }

        private async Task Eventually(Func<Task> action, int maxTries = 5)
        {
            int tries = 0;
            while (tries < maxTries)
            {
                try
                {
                    await action.Invoke();
                    break;
                }
                catch (XunitException e)
                {
                }

                tries++;
                await Task.Delay(500 * tries);
            }
        }

        [Fact]
        public async Task DepositTests()
        {
            // test1: generate deposit addresses correctly
            var dbName = RandomDbName();
            var seed = new Mnemonic(Wordlist.English);
            var seedFingerprint = seed.DeriveExtKey().GetPublicKey().GetHDFingerPrint();
            var segwitKeyPath = new RootedKeyPath(seedFingerprint, new KeyPath($"m/84'/1'/0'"));
            var segwitp2shKeyPath = new RootedKeyPath(seedFingerprint, new KeyPath($"m/49'/1'/0'"));
            var segwitFirstAddr = seed.DeriveExtKey().Derive(segwitKeyPath).Neuter().Derive(new KeyPath("0/0")).PubKey
                .GetAddress(ScriptPubKeyType.Segwit, Network.RegTest);
            var segwitp2shFirstAddr = seed.DeriveExtKey().Derive(segwitp2shKeyPath).Neuter().Derive(new KeyPath("0/0"))
                .PubKey
                .GetAddress(ScriptPubKeyType.SegwitP2SH, Network.RegTest);
            var app = CreateServerWithStandardSetup(new PrivatePondOptions()
            {
                NetworkType = NetworkType.Regtest,

                EnablePayjoinDeposits = false,
                Wallets = new WalletOption[]
                {
                    new WalletOption()
                    {
                        DerivationScheme = seed.DeriveExtKey().Derive(segwitKeyPath).Neuter().ToString(Network.RegTest),
                        AllowForDeposits = true,
                        RootedKeyPaths = new[] {segwitKeyPath.ToString()}
                    },
                    new WalletOption()
                    {
                        DerivationScheme = seed.DeriveExtKey().Derive(segwitp2shKeyPath).Neuter()
                            .ToString(Network.RegTest) + "-[p2sh]",
                        AllowForDeposits = true,
                        RootedKeyPaths = new[] {segwitp2shKeyPath.ToString()}
                    }
                }
            });
            using (app.Item1)
            {
                var resp = await app.Item2.GetAsync("api/v1/deposits/users/user1");
                var user1DepositRequest1 = await GetJson<List<DepositRequestData>>(resp);
                Assert.True(user1DepositRequest1.Count == 2);
                Assert.All(user1DepositRequest1, data => { Assert.Equal("user1", data.UserId); });
                //ensure that the order is correct
                Assert.Equal(segwitFirstAddr.ToString(), user1DepositRequest1[0].Destination);
                Assert.Equal(segwitp2shFirstAddr.ToString(), user1DepositRequest1[1].Destination);

                //ensure all users have diff deposits
                resp = await app.Item2.GetAsync("api/v1/deposits/users/user2");
                var user2DepositRequest1 = await GetJson<List<DepositRequestData>>(resp);
                Assert.True(user2DepositRequest1.Count == 2);
                Assert.All(user2DepositRequest1, data => { Assert.Equal("user2", data.UserId); });
                //ensure that the order is correct
                Assert.NotEqual(segwitFirstAddr.ToString(), user2DepositRequest1[0].Destination);
                Assert.NotEqual(segwitp2shFirstAddr.ToString(), user2DepositRequest1[1].Destination);


                resp = await app.Item2.GetAsync("api/v1/deposits/users/  USER1  ");
                var user1DepositRequest2 = await GetJson<List<DepositRequestData>>(resp);
                //ensure that user id is normalized
                Assert.All(user1DepositRequest2, data => { Assert.Equal("user1", data.UserId); });
                //ensure that the addresses are still the same
                Assert.Equal(segwitFirstAddr.ToString(), user1DepositRequest2[0].Destination);
                Assert.Equal(segwitp2shFirstAddr.ToString(), user1DepositRequest2[1].Destination);

                //pay to first address
                var user1deposit1txamt = Money.Satoshis(20000m);
                var user1deposit1txid = await RpcClient.SendToAddressAsync(
                    BitcoinAddress.Create(user1DepositRequest2[0].Destination, Network.RegTest),
                    user1deposit1txamt);

                await Eventually(async () =>
                {
                    resp = await app.Item2.GetAsync("api/v1/deposits/users/user1/history");
                    var user1DepositRequestHistory1 = await GetJson<List<DepositRequestData>>(resp);
                    //should still be 2 as we lazy generate new deposit requests
                    Assert.True(user1DepositRequestHistory1.Count == 2);

                    resp = await app.Item2.GetAsync("api/v1/deposits/users/user1");
                    var user1DepositRequest3 = await GetJson<List<DepositRequestData>>(resp);
                    //first addr should be regenerated but second should be still the same
                    Assert.NotEqual(segwitFirstAddr.ToString(), user1DepositRequest3[0].Destination);
                    Assert.Equal(segwitp2shFirstAddr.ToString(), user1DepositRequest3[1].Destination);
                });


                resp = await app.Item2.GetAsync("api/v1/deposits/users/user1/history");
                var user1DepositRequestHistory1 = await GetJson<List<DepositRequestData>>(resp);
                //after the get call above, there should be 3 now
                Assert.True(user1DepositRequestHistory1.Count == 3);

                var usedHistory =
                    user1DepositRequestHistory1.Single(data => data.Destination == segwitFirstAddr.ToString());
                Assert.Single(usedHistory.History);
                Assert.Equal(user1deposit1txid.ToString(), usedHistory.History.First().TransactionId);
                Assert.Equal(user1deposit1txamt.ToDecimal(MoneyUnit.BTC), usedHistory.History.First().Value);
                Assert.False(usedHistory.History.First().Confirmed);
            }
        }
    }
}