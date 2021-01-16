using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NBitcoin;
using NBXplorer;
using NBXplorer.DerivationStrategy;
using NBXplorer.Models;
using PrivatePond.Data;
using PrivatePond.Data.EF;

namespace PrivatePond.Controllers
{
    public class WalletService : IHostedService
    {
        private readonly IOptions<PrivatePondOptions> _options;
        private readonly IDbContextFactory<PrivatePondDbContext> _dbContextFactory;
        private readonly ExplorerClient _explorerClient;
        private readonly DerivationStrategyFactory _derivationStrategyFactory;
        private readonly ILogger<WalletService> _logger;
        private FileSystemWatcher _fileSystemWatcher;
        private IDataProtector _protector;
        private Dictionary<string, bool> HotWallet = new();
        private TaskCompletionSource tcs = new();

        private Dictionary<string, DerivationStrategyBase> Derivations =
            new();

        public WalletService(IOptions<PrivatePondOptions> options,
            IDbContextFactory<PrivatePondDbContext> dbContextFactory,
            ExplorerClient explorerClient,
            IDataProtectionProvider dataProtectionProvider,
            DerivationStrategyFactory derivationStrategyFactory,
            ILogger<WalletService> logger)
        {
            _options = options;
            _dbContextFactory = dbContextFactory;
            _explorerClient = explorerClient;
            _derivationStrategyFactory = derivationStrategyFactory;
            _logger = logger;
            _protector = dataProtectionProvider.CreateProtector("wallet");
        }

        public Task WaitUntilWalletsLoaded()
        {
            return tcs.Task;
        }

        public static string GetWalletId(DerivationStrategyBase derivationStrategy)
        {
            return derivationStrategy.ToString().GetHashCode().ToString();
            ;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
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

            await using var dbContext = _dbContextFactory.CreateDbContext();

            await dbContext.Wallets.ForEachAsync(wallet1 => { wallet1.Enabled = false; }, cancellationToken);

            var loadedWallets = new HashSet<string>();
            foreach (var walletOption in _options.Value.Wallets)
            {
                _logger.LogInformation($"Loading wallet {walletOption.DerivationScheme}");
                var derivationStrategy = GetDerivationStrategy(walletOption.DerivationScheme);
                var walletId = GetWalletId(derivationStrategy);
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
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            tcs.SetResult();
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

        public class WalletTransactionQuery
        {
            public WalletTransaction.WalletTransactionStatus[] Statuses { get; set; }
            public string[] WalletIds { get; set; }
        }

        public class DepositRequestQuery
        {
            public bool IncludeWalletTransactions { get; set; }
            public string[] WalletIds { get; set; }
        }

        public async Task<List<DepositRequest>> GetDepositRequests(DepositRequestQuery query)
        {
            await using var dbContext = _dbContextFactory.CreateDbContext();
            
            var queryable = dbContext.DepositRequests.AsQueryable();
            if (query.IncludeWalletTransactions)
            {
                queryable = queryable.Include(request => request.WalletTransactions);
            }
            if (query.WalletIds?.Any() is true)
            {
                queryable = queryable.Where(transaction =>
                    query.WalletIds.Contains(transaction.WalletId));
            }
            
            return await queryable.ToListAsync();
        }
        
        public async Task<List<WalletTransaction>> GetWalletTransactions(WalletTransactionQuery walletTransactionQuery)
        {
            await WaitUntilWalletsLoaded();
            
            await using var dbContext = _dbContextFactory.CreateDbContext();

            var queryable = dbContext.WalletTransactions.AsQueryable();

            if (walletTransactionQuery.Statuses?.Any() is true)
            {
                queryable = queryable.Where(transaction =>
                    walletTransactionQuery.Statuses.Contains(transaction.Status));
            }
            if (walletTransactionQuery.WalletIds?.Any() is true)
            {
                queryable = queryable.Where(transaction =>
                    walletTransactionQuery.WalletIds.Contains(transaction.WalletId));
            }
            return await queryable.ToListAsync();
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
                    var unencrypted = await File.ReadAllTextAsync(walletKeyPath);
                    key = ExtKey.Parse(unencrypted, network);
                    if (!key.Neuter()
                        .Equals(extPubKey))
                    {
                        _logger.LogError(
                            $"Mismatch in loading the unencrypted key for {extPubKey.GetWif(network)}. File will be deleted. If you wish to automatically sign transfers with this xpub, recreate the file with the correct key.");
                        File.Delete(walletKeyPath);
                        key = null;
                    }
                    else
                    {
                        await File.WriteAllTextAsync(walletKeyPathEncrypted, key.GetWif(network).ToString());
                        File.Delete(walletKeyPath);
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

            _fileSystemWatcher.Dispose();
            return Task.CompletedTask;
        }


        private void FileSystemWatcherOnChanged(object sender, FileSystemEventArgs e)
        {
            HotWallet.Clear();
        }

        private DerivationStrategyBase GetDerivationStrategy(string derivationScheme)
        {
            if (Derivations.TryGetValue(derivationScheme, out var derivScheme))
            {
                return derivScheme;
            }

            var derivationStrategy = _derivationStrategyFactory.Parse(derivationScheme);
            Derivations.Add(derivationScheme, derivationStrategy);
            return derivationStrategy;
        }

        public async Task UpdateWalletTransactions(List<WalletTransaction> updated)
        {
            await using var dbContext = _dbContextFactory.CreateDbContext();
            dbContext.AttachRange(updated);
            await dbContext.SaveChangesAsync();
        }
    }
}