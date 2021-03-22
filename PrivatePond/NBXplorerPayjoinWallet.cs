using System.Linq;
using BTCPayServer.BIP78.Sender;
using NBitcoin;
using NBXplorer.DerivationStrategy;

namespace PrivatePond
{
    public class NBXplorerPayjoinWallet : IPayjoinWallet
    {
        private readonly DerivationStrategyBase _derivationStrategyBase;
        private readonly RootedKeyPath[] _rootedKeyPaths;

        public NBXplorerPayjoinWallet(DerivationStrategyBase derivationStrategyBase, RootedKeyPath[] rootedKeyPaths)
        {
            _derivationStrategyBase = derivationStrategyBase;
            _rootedKeyPaths = rootedKeyPaths;
        }
        public IHDScriptPubKey Derive(KeyPath keyPath)
        {
            return ((IHDScriptPubKey)_derivationStrategyBase).Derive(keyPath);
        }

        public bool CanDeriveHardenedPath()
        {
            return _derivationStrategyBase.CanDeriveHardenedPath();
        }

        public Script ScriptPubKey => ((IHDScriptPubKey)_derivationStrategyBase).ScriptPubKey;
        public ScriptPubKeyType ScriptPubKeyType => _derivationStrategyBase.ScriptPubKeyType();

        public RootedKeyPath RootedKeyPath => _rootedKeyPaths.FirstOrDefault();

        public IHDKey AccountKey => _derivationStrategyBase.GetExtPubKeys().FirstOrDefault();
    }
}