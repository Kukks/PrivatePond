using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NBitcoin;
using NBXplorer;
using NBXplorer.DerivationStrategy;
using NBXplorer.Models;
using Newtonsoft.Json.Converters;
using PrivatePond.Data;
using PrivatePond.Data.EF;
using PrivatePond.Services.NBXplorer;

namespace PrivatePond.Controllers
{
    public class WalletService : IHostedService
    {
        private readonly IOptions<PrivatePondOptions> _options;
        private readonly IDbContextFactory<PrivatePondDbContext> _dbContextFactory;
        private readonly ExplorerClient _explorerClient;
        private readonly DerivationStrategyFactory _derivationStrategyFactory;
        private readonly ILogger<WalletService> _logger;
        private readonly NBXplorerSummaryProvider _nbXplorerSummaryProvider;
        private FileSystemWatcher _fileSystemWatcher;
        private IDataProtector _protector;
        private Dictionary<string, bool> HotWallet = new();
        private TaskCompletionSource tcs = new();

        private Dictionary<string, DerivationStrategyBase> Derivations =
            new();

        private Dictionary<string, DerivationStrategyBase> WalletIdDerivations =
            new();

        public WalletService(IOptions<PrivatePondOptions> options,
            IDbContextFactory<PrivatePondDbContext> dbContextFactory,
            ExplorerClient explorerClient,
            IDataProtectionProvider dataProtectionProvider,
            DerivationStrategyFactory derivationStrategyFactory,
            ILogger<WalletService> logger, NBXplorerSummaryProvider nbXplorerSummaryProvider)
        {
            _options = options;
            _dbContextFactory = dbContextFactory;
            _explorerClient = explorerClient;
            _derivationStrategyFactory = derivationStrategyFactory;
            _logger = logger;
            _nbXplorerSummaryProvider = nbXplorerSummaryProvider;
            _protector = dataProtectionProvider.CreateProtector("wallet");
        }

        public Task WaitUntilWalletsLoaded()
        {
            return tcs.Task;
        }

        public static string GetWalletId(DerivationStrategyBase derivationStrategy)
        {
            using var sha = new System.Security.Cryptography.SHA256Managed();
            var textData = System.Text.Encoding.UTF8.GetBytes(derivationStrategy.ToString());
            var hash = sha.ComputeHash(textData);
            return BitConverter.ToString(hash).Replace("-", String.Empty);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _ = ConfigureWallets(cancellationToken);
            return Task.CompletedTask;
        }
        private async Task ConfigureWallets(CancellationToken cancellationToken)
        {
            var first = true;
            while (! cancellationToken.IsCancellationRequested  && (_nbXplorerSummaryProvider.LastSummary is null || _nbXplorerSummaryProvider.LastSummary.State == NBXplorerState.NotConnected))
            {
                if(first)
                _logger.LogInformation("Waiting to connect to NBXplorer");
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                first = false;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            
            _logger.LogInformation("Configuring wallets");
            if (!string.IsNullOrEmpty(_options.Value.KeysDir))
            {
                var keysDir = Directory.CreateDirectory(_options.Value.KeysDir);
                _logger.LogInformation($"keys directory configured: {keysDir.FullName}"); 
                _fileSystemWatcher = new FileSystemWatcher()
                {
                    Filter = "*.*",
                    NotifyFilter = NotifyFilters.LastWrite,
                    Path = _options.Value.KeysDir,
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = false
                };
                _fileSystemWatcher.Changed += FileSystemWatcherOnChanged;
                _fileSystemWatcher.Created += FileSystemWatcherOnChanged;
                _fileSystemWatcher.Renamed += FileSystemWatcherOnChanged;
                _fileSystemWatcher.Deleted += FileSystemWatcherOnChanged;
            }
            else
            {
                _logger.LogWarning(
                    "There is no keys directory configured. The system will treat every wallet as a cold wallet.");
            }

            await using var dbContext = _dbContextFactory.CreateDbContext();

            await dbContext.Wallets.ForEachAsync(wallet1 => { wallet1.Enabled = false; }, cancellationToken);

            var loadedWallets = new HashSet<string>();
            foreach (var walletOption in _options.Value.Wallets)
            {
                _logger.LogInformation($"Loading wallet {walletOption.DerivationScheme}");
                var derivationStrategy = GetDerivationStrategy(walletOption.DerivationScheme);
                var walletId = GetWalletId(derivationStrategy);
                
                var keys = derivationStrategy.GetExtPubKeys();
                if (keys.Count() != walletOption.RootedKeyPaths.Length)
                {
                    throw new ConfigurationException("Wallets",
                        "you must configure the rooted key paths for ALL xpubs in ALL wallets");
                }
                else
                {
                    foreach (var rootedKeyPath in walletOption.RootedKeyPaths)
                    {
                       if(!RootedKeyPath.TryParse(rootedKeyPath, out var parsed))
                       {
                           
                           throw new ConfigurationException("Wallets",
                               $"root key path format is invalid: {rootedKeyPath}");
                       }
                    }
                }
                WalletIdDerivations.TryAdd(walletId, derivationStrategy);
                walletOption.WalletId = walletId;
                loadedWallets.Add(walletId);
                var wallet = await dbContext.Wallets.FindAsync(walletId);
                await _explorerClient.TrackAsync(derivationStrategy, new TrackWalletRequest()
                {
                }, cancellationToken);
                if (wallet is null)
                {
                    wallet = new Wallet()
                    {
                        Id = walletId,
                        Enabled = true,
                        DerivationStrategy = derivationStrategy.ToString(),
                    };
                    await dbContext.Wallets.AddAsync(wallet, cancellationToken);
                }

                wallet.Enabled = true;
                _logger.LogInformation($"Enabling wallet {wallet.Id}");
            }

            if (_options.Value.WalletReplenishmentIdealBalancePercentage.HasValue &&
                !string.IsNullOrEmpty(_options.Value.WalletReplenishmentSource))
            {
                _options.Value.WalletReplenishmentSourceWalletId =
                    GetWalletId(GetDerivationStrategy(_options.Value.WalletReplenishmentSource));
            }

            var depositRequestsToDeactivate = await dbContext.DepositRequests.Include(request => request.Wallet)
                .Where(request => !request.Wallet.Enabled).ToListAsync(cancellationToken);

            if (depositRequestsToDeactivate.Count > 0)
            {
                _logger.LogInformation(
                    $"Deactivating {depositRequestsToDeactivate.Count} deposit requests due to the wallet being disabled.");
            }

            depositRequestsToDeactivate.ForEach(request => request.Active = false);

            //if we have previous wallets, we want to make sure nbx is still tracking them in case of unusual behavior, such as a user paying to an old deposit request.
            foreach (var s in depositRequestsToDeactivate.GroupBy(request => request.Wallet.DerivationStrategy)
                .Select(requests => requests.Key))
            {
                var derivationStrategy = GetDerivationStrategy(s);
                WalletIdDerivations.TryAdd(GetWalletId(derivationStrategy), derivationStrategy);
                await _explorerClient.TrackAsync(derivationStrategy, new TrackWalletRequest(), cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            tcs.SetResult();
            _logger.LogInformation("Wallet service is ready");
        }

        public async Task<KeyPathInformation> ReserveAddress(string walletId)
        {
            await WaitUntilWalletsLoaded();
            var walletOption =
                _options.Value.Wallets.SingleOrDefault(option =>
                    option.AllowForDeposits && option.WalletId == walletId);
            if (walletOption is null)
            {
                return null;
            }

            var derivationStrategy = GetDerivationStrategy(walletOption.DerivationScheme);
            return await _explorerClient.GetUnusedAsync(derivationStrategy, DerivationFeature.Deposit, 0, true);
        }

        public async Task<bool> IsHotWallet(string walletId)
        {
            await WaitUntilWalletsLoaded();
            if (HotWallet.TryGetValue(walletId, out var res))
            {
                return res;
            }

            await using var dbContext = _dbContextFactory.CreateDbContext();

            var wallet = await dbContext.Wallets.FindAsync(walletId);
            var derivationStrategy = GetDerivationStrategy(wallet.DerivationStrategy);
            var keysFound = 0;
            var xpubs = derivationStrategy.GetExtPubKeys();
            foreach (var extPubKey in xpubs)
            {
                if ((await LoadKey(extPubKey)) != null)
                {
                    keysFound++;
                }
            }

            res = xpubs.Count() == keysFound;
            HotWallet.AddOrReplace(walletId, res);
            return res;
        }


       
        public async Task<List<WalletTransaction>> GetWalletTransactions(WalletTransactionQuery query,
            CancellationToken cancellationToken)
        {
            await WaitUntilWalletsLoaded();

            await using var dbContext = _dbContextFactory.CreateDbContext();

            var queryable = dbContext.WalletTransactions.AsQueryable();
            if (query.IncludeWallet)
            {
                queryable = queryable.Include(request => request.Wallet);
            }

            if (query.Statuses is not null)
            {
                queryable = queryable.Where(transaction =>
                    query.Statuses.Contains(transaction.Status));
            }

            if (query.Ids is not null)
            {
                queryable = queryable.Where(transaction =>
                    query.Ids.Contains(transaction.Id));
            }

            if (query.WalletIds is not null)
            {
                queryable = queryable.Where(transaction =>
                    query.WalletIds.Contains(transaction.WalletId));
            }

            if (query.Skip.HasValue)
            {
                queryable = queryable.Skip(query.Skip.Value);
            }

            if (query.Take.HasValue)
            {
                queryable = queryable.Take(query.Take.Value);
            }

            return await queryable.ToListAsync(cancellationToken);
        }

        private async Task<ExtKey> LoadKey(ExtPubKey extPubKey)
        {
            ExtKey key = null;
            var network = _explorerClient.Network.NBitcoinNetwork;
            var keyFound = false;
            var walletKeyPath = Path.Combine(_options.Value.KeysDir,
                extPubKey.ToString(network));
            var walletKeyPathEncrypted = walletKeyPath + ".encrypted";
            if (File.Exists(walletKeyPathEncrypted))
            {
                var encrypted = await File.ReadAllTextAsync(walletKeyPathEncrypted);

                var unencrypted = _protector.Unprotect(encrypted);
                key = ExtKey.Parse(unencrypted, network);
                if (!key.Neuter()
                    .Equals(extPubKey))
                {
                    _logger.LogError(
                        $"Mismatch in loading the encrypted key for {extPubKey.GetWif(network)}. This should never happen!");
                    File.Delete(walletKeyPathEncrypted);
                }
                else
                {
                    keyFound = true;
                }
            }

            if (File.Exists(walletKeyPath))
            {
                if (keyFound)
                {
                    _logger.LogWarning(
                        $"An unencrypted key file was found when an encrypted file was already present. ");
                    File.Delete(walletKeyPath);
                }
                else
                {
                    try
                    {
                        var unencrypted = await File.ReadAllTextAsync(walletKeyPath);
                        key = ExtKey.Parse(unencrypted, network);
                        if (!key.Neuter()
                            .Equals(extPubKey))
                        {
                            _logger.LogError(
                                $"Mismatch in loading the unencrypted key for {extPubKey.GetWif(network)}. File will be deleted. If you wish to automatically sign transfers with this xpub, recreate the file with the correct key(xprv).");
                            File.Delete(walletKeyPath);
                            key = null;
                        }
                        else
                        {
                            await File.WriteAllTextAsync(walletKeyPathEncrypted, _protector.Protect( key.GetWif(network).ToString()));
                            File.Delete(walletKeyPath);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(
                            $"Mismatch in loading the unencrypted key for {extPubKey.GetWif(network)}. If you wish to automatically sign transfers with this xpub, recreate the file with the correct key(xprv).");
                    }
                    
                }
            }

            return key;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _fileSystemWatcher.Changed -= FileSystemWatcherOnChanged;
            _fileSystemWatcher.Created -= FileSystemWatcherOnChanged;
            _fileSystemWatcher.Renamed -= FileSystemWatcherOnChanged;
            _fileSystemWatcher.Deleted -= FileSystemWatcherOnChanged;

            _fileSystemWatcher?.Dispose();
            return Task.CompletedTask;
        }


        private void FileSystemWatcherOnChanged(object sender, FileSystemEventArgs e)
        {
            HotWallet.Clear();
        }

        public DerivationStrategyBase GetDerivationStrategy(string derivationScheme)
        {
            if (Derivations.TryGetValue(derivationScheme, out var derivScheme))
            {
                return derivScheme;
            }

            try
            {

            var derivationStrategy = _derivationStrategyFactory.Parse(derivationScheme);
            if (derivationStrategy.ScriptPubKeyType() == ScriptPubKeyType.Legacy)
            {
                throw new FormatException("Non segwit wallets are not supported.");
            }
            Derivations.Add(derivationScheme, derivationStrategy);
            return derivationStrategy;
            
            }
            catch (FormatException e)
            {
                
                throw new ConfigurationException("Wallets", $"{derivationScheme} invalid: {e.Message}");
            }
        }

        public async Task<DerivationStrategyBase> GetDerivationsByWalletId(string walletId)
        {
            if (WalletIdDerivations.TryGetValue(walletId, out var derivScheme))
            {
                return derivScheme;
            }

            await using var dbContext = _dbContextFactory.CreateDbContext();
            var match = await dbContext.Wallets.FindAsync(walletId);
            if (match is null)
            {
                return null;
            }

            var result = GetDerivationStrategy(match.DerivationStrategy);
            WalletIdDerivations.Add(walletId, result);
            return result;
        }

        public async Task Update(UpdateContext context, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                $"Adding {context.AddedWalletTransactions.Count} wallet txs, updating {context.UpdatedWalletTransactions.Count} wallet txs and {context.UpdatedDepositRequests.Count} deposit requests");
            await using var dbContext = _dbContextFactory.CreateDbContext();
            dbContext.UpdateRange(context.UpdatedWalletTransactions);
            dbContext.UpdateRange(context.UpdatedDepositRequests);
            await dbContext.AddRangeAsync(context.AddedWalletTransactions, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        public class UpdateContext
        {
            public List<WalletTransaction> AddedWalletTransactions = new List<WalletTransaction>();
            public List<WalletTransaction> UpdatedWalletTransactions = new List<WalletTransaction>();
            public List<DepositRequest> UpdatedDepositRequests = new List<DepositRequest>();
        }

        public async Task<IEnumerable<WalletData>> GetWallets(WalletQuery query)
        {
            await using var dbContext = _dbContextFactory.CreateDbContext();
            var queryable = dbContext.Wallets.AsQueryable();
            if (query.Ids is not null)
            {
                queryable = queryable.Where(transaction =>
                    query.Ids.Contains(transaction.Id));
            }

            if (query.Enabled.HasValue)
            {
                queryable = queryable.Where(wallet => wallet.Enabled == query.Enabled);
            }

            return await Task.WhenAll(( await queryable.ToListAsync( )).Select(FromDBModel));
        }

        private async Task<WalletData> FromDBModel(Wallet wallet)
        {
            var derivation = await GetDerivationsByWalletId(wallet.Id);
            var balance = await _explorerClient.GetBalanceAsync(derivation);
            return new WalletData()
            {
                Balance = ((Money) balance.Confirmed).ToDecimal(MoneyUnit.BTC),
                Enabled = wallet.Enabled,
                Id = wallet.Id,
                
            };
        }

        public async Task<PSBT> SignWithHotWallets(string[] walletIdsToSignWith, PSBT psbt)
        {
            var resultingPSBT = psbt.Clone();
            
            var derivationsByWalletId = walletIdsToSignWith.ToDictionary(s => s, GetDerivationsByWalletId);
            await Task.WhenAll(derivationsByWalletId.Values);
            foreach (var task in derivationsByWalletId)
            {
                var walletOption = _options.Value.Wallets.Single(option => option.WalletId == task.Key);
                var deriv = await task.Value;
                var xpubs = deriv.GetExtPubKeys();
                int i = 0;
                foreach (var xpub in xpubs)
                {
                    var key = await LoadKey(xpub);
                    if (key is null)
                    {
                        i++;
                        continue;
                    }

                    var rootedKeyPath = walletOption.ParsedRootedKeyPaths.ElementAt(i);
                    resultingPSBT = resultingPSBT.SignAll(deriv, key, rootedKeyPath);
                    i++;
                }
            }

            return resultingPSBT;
        }
    }
}