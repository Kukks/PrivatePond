using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.BIP78.Sender;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NBitcoin;
using NBitcoin.Altcoins.Elements;
using NBitcoin.OpenAsset;
using NBitcoin.Payment;
using NBitcoin.RPC;
using NBXplorer;
using NBXplorer.DerivationStrategy;
using NBXplorer.Models;
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

        [Fact]
        public async Task DepositTests()
        {
            var seed = new Mnemonic(Wordlist.English);
            var seedFingerprint = seed.DeriveExtKey().GetPublicKey().GetHDFingerPrint();
            var segwitKeyPath = new RootedKeyPath(seedFingerprint, new KeyPath($"m/84'/1'/0'"));
            var segwitp2shKeyPath = new RootedKeyPath(seedFingerprint, new KeyPath($"m/49'/1'/0'"));
            var segwitXpriv = seed.DeriveExtKey().Derive(segwitKeyPath);
            var segwitXpub = segwitXpriv.Neuter();
            var segwitFirstAddr = segwitXpub.Derive(new KeyPath("0/0")).PubKey
                .GetAddress(ScriptPubKeyType.Segwit, Network.RegTest);

            var segwitp2shXpriv = seed.DeriveExtKey().Derive(segwitp2shKeyPath);
            var segwitp2shXpub = segwitp2shXpriv.Neuter();
            var segwitp2shFirstAddr = segwitp2shXpub.Derive(new KeyPath("0/0"))
                .PubKey
                .GetAddress(ScriptPubKeyType.SegwitP2SH, Network.RegTest);

            var options = new PrivatePondOptions()
            {
                NetworkType = NetworkType.Regtest,

                EnablePayjoinDeposits = false,
                MinimumConfirmations = 1,
                KeysDir = "keys",
                Wallets = new WalletOption[]
                {
                    new WalletOption()
                    {
                        DerivationScheme = segwitXpub.ToString(Network.RegTest),
                        AllowForDeposits = true,
                        RootedKeyPaths = new[] {segwitKeyPath.ToString()}
                    },
                    new WalletOption()
                    {
                        DerivationScheme = segwitp2shXpub
                            .ToString(Network.RegTest) + "-[p2sh]",
                        AllowForDeposits = true,
                        RootedKeyPaths = new[] {segwitp2shKeyPath.ToString()}
                    }
                }
            };
            var app = CreateServerWithStandardSetup(options);
            using (app.Item1)
            {
                var resp = await app.Item2.GetAsync("api/v1/deposits/users/user1");
                var user1DepositRequest1 = await GetJson<List<DepositRequestData>>(resp);
                Assert.True(user1DepositRequest1.Count == 2);
                Assert.All(user1DepositRequest1, data => { Assert.Equal("user1", data.UserId); });
                //ensure that the order is correct
                Assert.Equal(segwitFirstAddr.ToString(), user1DepositRequest1[0].Destination);
                Assert.Equal(segwitp2shFirstAddr.ToString(), user1DepositRequest1[1].Destination);

                Assert.All(user1DepositRequest1,
                    data =>
                    {
                        Assert.False(new BitcoinUrlBuilder(data.PaymentLink, Network.RegTest).UnknowParameters
                            .ContainsKey("pj"));
                    });

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

                await RpcClient.GenerateAsync(1);
                var segwitWalletId = "";
                await Eventually(async () =>
                {
                    resp = await app.Item2.GetAsync("api/v1/deposits/users/user1/history");
                    var user1DepositRequestHistory1 = await GetJson<List<DepositRequestData>>(resp);
                    var usedHistory =
                        user1DepositRequestHistory1.Single(data => data.Destination == segwitFirstAddr.ToString());
                    segwitWalletId = usedHistory.WalletId;
                    Assert.True(usedHistory.History.First().Confirmed);
                });

                //paying to same address will require an approval from api as we need to discourage address reuse

                var user1deposit2txamt = Money.Satoshis(30000m);
                var user1deposit2txid = await RpcClient.SendToAddressAsync(
                    BitcoinAddress.Create(user1DepositRequest2[0].Destination, Network.RegTest),
                    user1deposit2txamt);
                await RpcClient.GenerateAsync(1);
                await Eventually(async () =>
                {
                    resp = await app.Item2.GetAsync($"api/v1/wallets/{segwitWalletId}/transactions");
                    var segwitWalletTxs = await GetJson<List<WalletTransaction>>(resp);
                    Assert.Equal(2, segwitWalletTxs.Count);
                    Assert.Contains(segwitWalletTxs, transaction => transaction.InactiveDepositRequest is false &&
                                                                    transaction.Amount ==
                                                                    user1deposit1txamt.ToDecimal(MoneyUnit.BTC) &&
                                                                    transaction.WalletId == segwitWalletId &&
                                                                    transaction.Confirmations == 2 &&
                                                                    transaction.Status is WalletTransaction
                                                                        .WalletTransactionStatus.Confirmed &&
                                                                    transaction.OutPoint.Hash == user1deposit1txid
                    );
                    var inactivedeposit = segwitWalletTxs.Single(transaction =>
                        transaction.InactiveDepositRequest is true &&
                        transaction.Amount == user1deposit2txamt.ToDecimal(MoneyUnit.BTC) &&
                        transaction.WalletId == segwitWalletId &&
                        transaction.Confirmations == 1 &&
                        transaction.Status is WalletTransaction.WalletTransactionStatus.RequiresApproval &&
                        transaction.OutPoint.Hash == user1deposit2txid);

                    //random rout values should 404!
                    resp = await app.Item2.PostAsync($"api/v1/wallets/abcd/transactions/{inactivedeposit.Id}/approve",
                        new StringContent(""));
                    Assert.False(resp.IsSuccessStatusCode);
                    resp = await app.Item2.PostAsync($"api/v1/wallets/{segwitWalletId}/transactions/abcd/approve",
                        new StringContent(""));
                    Assert.False(resp.IsSuccessStatusCode);

                    resp = await app.Item2.PostAsync(
                        $"api/v1/wallets/{segwitWalletId}/transactions/{inactivedeposit.Id}/approve",
                        new StringContent(""));
                    resp.EnsureSuccessStatusCode();

                    resp = await app.Item2.GetAsync($"api/v1/wallets/{segwitWalletId}/transactions");
                    segwitWalletTxs = await GetJson<List<WalletTransaction>>(resp);
                    Assert.All(segwitWalletTxs,
                        transaction =>
                        {
                            Assert.Equal(WalletTransaction.WalletTransactionStatus.Confirmed, transaction.Status);
                        });
                });
            }

            //payjoin tests
            options.EnablePayjoinDeposits = true;
            options.PayjoinEndpointRoute = "https://yourwebsite.com/pj";
            
            app = CreateServerWithStandardSetup(options);
            using (app.Item1)
            {
                var resp = await app.Item2.GetAsync("api/v1/deposits/users/user1");
                var user1DepositRequest1 = await GetJson<List<DepositRequestData>>(resp);
                BitcoinUrlBuilder bip21 = null;
                Assert.All(user1DepositRequest1,
                    data =>
                    {
                        if (bip21 is null)
                        {
                            bip21 = new BitcoinUrlBuilder(data.PaymentLink, Network.RegTest);
                        }
                        Assert.True(new BitcoinUrlBuilder(data.PaymentLink, Network.RegTest).UnknowParameters
                            .TryGetValue("pj", out var pjendoint));
                        Assert.Equal(options.PayjoinEndpointRoute, pjendoint);

                    });

                var explorerClient = app.Item1.Services.GetRequiredService<ExplorerClient>();
                var wallet = await explorerClient.GenerateWalletAsync(new GenerateWalletRequest()
                {
                    SavePrivateKeys = true,
                    ScriptPubKeyType = ScriptPubKeyType.Segwit
                });
                var walletAddr = await explorerClient.GetUnusedAsync(wallet.DerivationScheme, DerivationFeature.Deposit, 0, true);
                await RpcClient.SendToAddressAsync(walletAddr.Address, new Money(0.01m, MoneyUnit.BTC));
                await RpcClient.GenerateAsync(2);
                
                bip21.UnknowParameters["pj"] = new Uri(app.Item2.BaseAddress, "pj").ToString();
                await Eventually(async () =>
                {
                    Assert.Equal(0.01m,
                        ((Money) (await explorerClient.GetBalanceAsync(wallet.DerivationScheme)).Confirmed).ToDecimal(
                            MoneyUnit.BTC));
                });
                var psbtResponse = await explorerClient.CreatePSBTAsync(wallet.DerivationScheme, new CreatePSBTRequest()
                {
                    IncludeGlobalXPub = false,
                    FeePreference = new FeePreference()
                    {
                        FallbackFeeRate = new FeeRate(20m)
                    },
                    Destinations = new List<CreatePSBTDestination>()
                    {
                        new CreatePSBTDestination()
                        {
                            Amount = new Money(0.001m, MoneyUnit.BTC),
                            Destination = bip21.Address
                        }
                    }
                });

                var originalPSBT = psbtResponse.PSBT;
                originalPSBT = originalPSBT.SignAll(wallet.DerivationScheme, wallet.AccountHDKey, wallet.AccountKeyPath);
                var payjoinServerCommunicator = new PayjoinTestCommunicator(app.Item1);
                
                var pjClient = new PayjoinClient(payjoinServerCommunicator);
                var walletService = app.Item1.Services.GetService<WalletService>();
                //confirm that all the wallets arent hot

                options = app.Item1.Services.GetRequiredService<IOptions<PrivatePondOptions>>().Value;
                foreach (var walletOption in options.Wallets)
                {
                    Assert.False(
                        await walletService.IsHotWallet(walletOption.WalletId));
                }    
                
                //we did not configure hot wallets, so payjoin will never be available
                await Assert.ThrowsAsync<PayjoinReceiverException>(async () => await pjClient.RequestPayjoin(bip21,
                    new NBXplorerPayjoinWallet(wallet.DerivationScheme, new[] {wallet.AccountKeyPath}), originalPSBT,
                    CancellationToken.None));

               //lets configure hot wallets!
               
               //the keys dir should have been created on startup if it did not exist
               Assert.True(Directory.Exists(options.KeysDir));

               await File.WriteAllTextAsync(Path.Combine(options.KeysDir, segwitXpub.ToString(Network.RegTest)), segwitXpriv.ToString(Network.RegTest));
               await File.WriteAllTextAsync(Path.Combine(options.KeysDir, segwitp2shXpub.ToString(Network.RegTest)), segwitp2shXpriv.ToString(Network.RegTest));
               await Eventually(async () =>
               {
                   //the priv keys get deleted and an encrypted version should be created instead
                   Assert.False(File.Exists(Path.Combine(options.KeysDir, segwitXpub.ToString(Network.RegTest))));
                   Assert.False(File.Exists(Path.Combine(options.KeysDir, segwitp2shXpub.ToString(Network.RegTest))));
                   Assert.True(File.Exists(Path.Combine(options.KeysDir,
                       segwitXpub.ToString(Network.RegTest) + "encrypted")));
                   Assert.True(File.Exists(Path.Combine(options.KeysDir,
                       segwitp2shXpub.ToString(Network.RegTest) + "encrypted")));
               });
               foreach (var walletOption in options.Wallets)
               {
                   if (walletOption.DerivationScheme.Contains(segwitXpub.ToString(Network.RegTest)) ||
                       walletOption.DerivationScheme.Contains(segwitp2shXpub.ToString(Network.RegTest)))
                   {
                       
                       Assert.True(
                           await walletService.IsHotWallet(walletOption.WalletId));
                   }
                   else
                   {
                       Assert.False(
                           await walletService.IsHotWallet(walletOption.WalletId));
                   }
               }    
               
               var payjoinPSBT = await pjClient.RequestPayjoin(bip21,
                    new NBXplorerPayjoinWallet(wallet.DerivationScheme, new[] {wallet.AccountKeyPath}), originalPSBT, CancellationToken.None);
                payjoinPSBT = payjoinPSBT.SignAll(wallet.DerivationScheme, wallet.AccountHDKey, wallet.AccountKeyPath).Finalize();
                Assert.True((await explorerClient.BroadcastAsync(payjoinPSBT.ExtractTransaction())).Success);

                await Eventually(async () =>
                {

                    resp = await app.Item2.GetAsync("api/v1/deposits/users/user1/history");
                    user1DepositRequest1 = await GetJson<List<DepositRequestData>>(resp);
                    var payjoinedDeposit =
                        user1DepositRequest1.SingleOrDefault(data => data.Destination == bip21.Address.ToString());
                    Assert.NotNull(payjoinedDeposit);
                    var history = Assert.Single(payjoinedDeposit.History);
                    Assert.Equal(0.0001m, history.Value);
                    Assert.NotNull(history.PayjoinValue);
                    Assert.Equal(payjoinPSBT.ExtractTransaction().GetHash().ToString(), history.TransactionId);
                });
                
                
                //ok tested deposits, let's test the querying
                resp = await app.Item2.GetAsync("api/v1/deposits");
                var drs = await GetJson<List<DepositRequestData>>(resp);
                Assert.Equal(2, drs.Count());
                
                resp = await app.Item2.GetAsync("api/v1/deposits?active=false");
                drs = await GetJson<List<DepositRequestData>>(resp);
                var dr = Assert.Single(drs);
                Assert.False(dr.Active);
                Assert.True(Assert.Single(dr.History).PayjoinValue.HasValue);
            }
            
        }

        [Fact]
        public async Task TransferTests()
        {
            var seed = new Mnemonic(Wordlist.English);
            var seedFingerprint = seed.DeriveExtKey().GetPublicKey().GetHDFingerPrint();
            var segwitKeyPath = new RootedKeyPath(seedFingerprint, new KeyPath($"m/84'/1'/0'"));
            var segwitp2shKeyPath = new RootedKeyPath(seedFingerprint, new KeyPath($"m/49'/1'/0'"));
            var segwitXpriv = seed.DeriveExtKey().Derive(segwitKeyPath);
            var segwitXpub = segwitXpriv.Neuter();
            var segwitFirstAddr = segwitXpub.Derive(new KeyPath("0/0")).PubKey
                .GetAddress(ScriptPubKeyType.Segwit, Network.RegTest);

            var segwitp2shXpriv = seed.DeriveExtKey().Derive(segwitp2shKeyPath);
            var segwitp2shXpub = segwitp2shXpriv.Neuter();
            var segwitp2shFirstAddr = segwitp2shXpub.Derive(new KeyPath("0/0"))
                .PubKey
                .GetAddress(ScriptPubKeyType.SegwitP2SH, Network.RegTest);

            var options = new PrivatePondOptions()
            {
                NetworkType = NetworkType.Regtest,

                EnablePayjoinDeposits = false,
                MinimumConfirmations = 1,
                KeysDir = "keys",
                BatchTransfersEvery = 0,//please don't do this in production, you will never be able to replenish from cold wallet since txs will be ongoing
                Wallets = new WalletOption[]
                {
                    new WalletOption()
                    {
                        DerivationScheme = segwitXpub.ToString(Network.RegTest),
                        AllowForDeposits = true,
                        RootedKeyPaths = new[] {segwitKeyPath.ToString()},
                        AllowForTransfers = true,
                    },
                    new WalletOption()
                    {
                        DerivationScheme = segwitp2shXpub
                            .ToString(Network.RegTest) + "-[p2sh]",
                        AllowForDeposits = true,
                        RootedKeyPaths = new[] {segwitp2shKeyPath.ToString()},
                        AllowForTransfers = true,
                    }
                }
            };
            var app = CreateServerWithStandardSetup(options);
            using (app.Item1)
            {
                //the keys dir should have been created on startup if it did not exist
                Assert.True(Directory.Exists(options.KeysDir));

                await File.WriteAllTextAsync(Path.Combine(options.KeysDir, segwitXpub.ToString(Network.RegTest)), segwitXpriv.ToString(Network.RegTest));
                await File.WriteAllTextAsync(Path.Combine(options.KeysDir, segwitp2shXpub.ToString(Network.RegTest)), segwitp2shXpriv.ToString(Network.RegTest));
                await Eventually(async () =>
                {
                    //the priv keys get deleted and an encrypted version should be created instead
                    Assert.False(File.Exists(Path.Combine(options.KeysDir, segwitXpub.ToString(Network.RegTest))));
                    Assert.False(File.Exists(Path.Combine(options.KeysDir, segwitp2shXpub.ToString(Network.RegTest))));
                    Assert.True(File.Exists(Path.Combine(options.KeysDir,
                        segwitXpub.ToString(Network.RegTest) + "encrypted")));
                    Assert.True(File.Exists(Path.Combine(options.KeysDir,
                        segwitp2shXpub.ToString(Network.RegTest) + "encrypted")));
                });
                
                var walletService = app.Item1.Services.GetService<WalletService>();
                var explorerClient = app.Item1.Services.GetService<ExplorerClient>();
                options = app.Item1.Services.GetRequiredService<IOptions<PrivatePondOptions>>().Value;
                foreach (var walletOption in options.Wallets)
                {
                    if (walletOption.DerivationScheme.Contains(segwitXpub.ToString(Network.RegTest)) ||
                        walletOption.DerivationScheme.Contains(segwitp2shXpub.ToString(Network.RegTest)))
                    {
                       
                        Assert.True(
                            await walletService.IsHotWallet(walletOption.WalletId));
                    }
                    else
                    {
                        Assert.False(
                            await walletService.IsHotWallet(walletOption.WalletId));
                    }
                }    
                
                var resp = await app.Item2.GetAsync("api/v1/deposits/users/user1");
                var user1DepositRequest1 = await GetJson<List<DepositRequestData>>(resp);
                foreach (var depositRequestData in user1DepositRequest1)
                {
                    await RpcClient.SendToAddressAsync(
                        BitcoinAddress.Create(depositRequestData.Destination, Network.RegTest),
                        new Money(0.01m, MoneyUnit.BTC));
                }
                await RpcClient.GenerateAsync(2);
                //there should be 0.02m spendable on transfers
                
                
                //this one wont be able to be processed until there's funds to process it
                var request = new RequestTransferRequest()
                {
                    Amount = 0.1m,
                    Destination = (await RpcClient.GetNewAddressAsync()).ToString()
                };
                var bigTransferRequest  = await
                    GetJson<TransferRequestData>(await app.Item2.PostAsJsonAsync("api/v1/transfers", request));
                Assert.Equal(bigTransferRequest.Amount, request.Amount);
                Assert.Equal(bigTransferRequest.Destination, request.Destination);
                Assert.Equal(TransferStatus.Pending, bigTransferRequest.Status);
                
                Assert.Equal(JsonSerializer.Serialize(bigTransferRequest), JsonSerializer.Serialize(await
                    GetJson<TransferRequestData>(await app.Item2.GetAsync($"api/v1/transfers/{bigTransferRequest.Id}"))));

                //let's do 5 transfer requests of a total of 0.0015. This means it will spend through coins on both hot wallets
                List<TransferRequestData> smallTransfers = new List<TransferRequestData>();
                TransferRequestData smallTransferRequestData;
                for (int j = 0; j < 5; j++)
                {
                    request = new RequestTransferRequest()
                    {
                        Amount = 0.0003m,
                        Destination = (await RpcClient.GetNewAddressAsync()).ToString()
                    };
                    
                    smallTransferRequestData = await
                        GetJson<TransferRequestData>(await app.Item2.PostAsJsonAsync("api/v1/transfers", request));
                    smallTransfers.Add(smallTransferRequestData);
                }
                
                //let's also try two other requests of 0.0001 each but using BIP21 formats

                request = new RequestTransferRequest()
                {
                    Amount = 0.0001m,
                    Destination = $"bitcoin:{(await RpcClient.GetNewAddressAsync())}"
                };
                    
                smallTransferRequestData = await
                    GetJson<TransferRequestData>(await app.Item2.PostAsJsonAsync("api/v1/transfers", request));
                smallTransfers.Add(smallTransferRequestData);
                request = new RequestTransferRequest()
                {
                    Destination = $"bitcoin:{(await RpcClient.GetNewAddressAsync())}?amount=0.0001m"
                };
                    
                smallTransferRequestData = await
                    GetJson<TransferRequestData>(await app.Item2.PostAsJsonAsync("api/v1/transfers", request));
                smallTransfers.Add(smallTransferRequestData);
                
                await Eventually(async () =>
                {
                    foreach (var smallTransfer in smallTransfers)
                    {
                        var tr = await
                            GetJson<TransferRequestData>(await app.Item2.GetAsync($"api/v1/transfers/{smallTransfer.Id}"));
                        Assert.Equal(TransferStatus.Processing, tr.Status);
                        Assert.False(string.IsNullOrEmpty(tr.TransactionHash));
                        var tx = await explorerClient.GetTransactionAsync(uint256.Parse(tr.TransactionHash));
                        Assert.Equal(0, tx.Confirmations );
                        Assert.NotNull(tx.Transaction.Outputs.Single(txOut => txOut.IsTo(BitcoinAddress.Create(
                                                                                  HelperExtensions.GetAddress(tr.Destination, Network.RegTest, out _, out _,
                                                                                      out _), Network.RegTest)) &&
                                                                              txOut.Value.ToDecimal(MoneyUnit.BTC) == tr.Amount));
                    }
                });

                await RpcClient.GenerateAsync(2);
                await Eventually(async () =>
                {
                    foreach (var smallTransfer in smallTransfers)
                    {
                        var tr = await
                            GetJson<TransferRequestData>(await app.Item2.GetAsync($"api/v1/transfers/{smallTransfer.Id}"));
                        Assert.Equal(TransferStatus.Completed, tr.Status);
                    }
                });
                
                //while all the small ones went through, the big one stuck. We dont let a huge request block smaller ones that could be done in the meantime
                Assert.Equal(JsonSerializer.Serialize(bigTransferRequest), JsonSerializer.Serialize(await
                    GetJson<TransferRequestData>(await app.Item2.GetAsync($"api/v1/transfers/{bigTransferRequest.Id}"))));
                
                //we also allow BIP21 destination in transfers
                request = new RequestTransferRequest()
                {
                    Amount = 0.0004m,
                    Destination = $"bitcoin:{(await RpcClient.GetNewAddressAsync())}?amount=0.003"
                };
                //if amount also specified, needs to match the bip21 amount 
                await Assert.ThrowsAsync<HttpRequestException>(async () =>
                {
                    await
                        GetJson<TransferRequestData>(await app.Item2.PostAsJsonAsync("api/v1/transfers", request));
                });
                request = new RequestTransferRequest()
                {
                    Destination = $"bitcoin:{(await RpcClient.GetNewAddressAsync())}?amount=0.00"
                };
                await Assert.ThrowsAsync<HttpRequestException>(async () =>
                {
                    await
                        GetJson<TransferRequestData>(await app.Item2.PostAsJsonAsync("api/v1/transfers", request));
                });
                request = new RequestTransferRequest()
                {
                    Destination = $"bitcoin:{(await RpcClient.GetNewAddressAsync())}"
                };
                await Assert.ThrowsAsync<HttpRequestException>(async () =>
                {
                    await
                        GetJson<TransferRequestData>(await app.Item2.PostAsJsonAsync("api/v1/transfers", request));
                });
                
                //ok let's fill up the bank and see if the big request will now be processed
                foreach (var depositRequestData in user1DepositRequest1)
                {
                    await RpcClient.SendToAddressAsync(
                        BitcoinAddress.Create(depositRequestData.Destination, Network.RegTest),
                        new Money(0.055m, MoneyUnit.BTC));
                }
                await RpcClient.GenerateAsync(2);
                
                await Eventually(async () =>
                {
                    var tr = await
                        GetJson<TransferRequestData>(await app.Item2.GetAsync($"api/v1/transfers/{bigTransferRequest.Id}"));
                    Assert.Equal(TransferStatus.Processing, tr.Status);
                    Assert.False(string.IsNullOrEmpty(tr.TransactionHash));
                    var tx = await explorerClient.GetTransactionAsync(uint256.Parse(tr.TransactionHash));
                    Assert.Equal(0, tx.Confirmations );
                    Assert.NotNull(tx.Transaction.Outputs.Single(txOut => txOut.IsTo(BitcoinAddress.Create(
                                                                              HelperExtensions.GetAddress(tr.Destination, Network.RegTest, out _, out _,
                                                                                  out _), Network.RegTest)) &&
                                                                          txOut.Value.ToDecimal(MoneyUnit.BTC) == tr.Amount));
                });

                await RpcClient.GenerateAsync(2);
                await Eventually(async () =>
                {
                    var tr = await
                        GetJson<TransferRequestData>(await app.Item2.GetAsync($"api/v1/transfers/{bigTransferRequest.Id}"));
                    Assert.Equal(TransferStatus.Completed, tr.Status);
                });
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
                catch (Exception e)
                {
                    if (tries == maxTries)
                    {
                        throw;
                    }
                }

                tries++;
                await Task.Delay(500 * tries);
            }
        }
        public class PayjoinTestCommunicator : HttpClientPayjoinServerCommunicator
        {
            private readonly WebApplicationFactory<Startup> _client;

            public PayjoinTestCommunicator(WebApplicationFactory<Startup> client)
            {
                _client = client;
            }
            protected override HttpClient CreateHttpClient(Uri uri)
            {
                return _client.CreateClient();
            }
        }
    }
}