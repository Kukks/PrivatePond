using NBitcoin;
using NBitcoin.Payment;
using NBXplorer.DerivationStrategy;

namespace PrivatePond
{
    public static class HelperExtensions
    {
        public static string GetAddress(string destination, Network network, out ScriptPubKeyType? scriptPubKeyType,
            out decimal? amount, out BitcoinUrlBuilder bip21)
        {
            bip21 = null;
            BitcoinAddress address;
            amount = null;
            if (destination.ToLowerInvariant().StartsWith("bitcoin:"))
            {
                bip21 = new BitcoinUrlBuilder(destination, network);
                address = bip21.Address;
                amount = bip21.Amount?.ToDecimal(MoneyUnit.BTC);
            }
            else
                address = BitcoinAddress.Create(destination, network);

            scriptPubKeyType = null;
            switch (address)
            {
                case BitcoinPubKeyAddress bitcoinPubKeyAddress:
                    scriptPubKeyType = NBitcoin.ScriptPubKeyType.Legacy;
                    break;
                case BitcoinScriptAddress bitcoinScriptAddress:
                    scriptPubKeyType = NBitcoin.ScriptPubKeyType.SegwitP2SH;
                    break;
                case BitcoinWitPubKeyAddress bitcoinWitPubKeyAddress:
                    scriptPubKeyType = NBitcoin.ScriptPubKeyType.Segwit;
                    break;
                case BitcoinWitScriptAddress bitcoinWitScriptAddress:
                    scriptPubKeyType = NBitcoin.ScriptPubKeyType.Segwit;

                    break;
            }

            return address.ToString();
        }

        public static ScriptPubKeyType ScriptPubKeyType(this DerivationStrategyBase derivationStrategyBase)
        {
            if (IsSegwitCore(derivationStrategyBase))
            {
                return NBitcoin.ScriptPubKeyType.Segwit;
            }

            return (derivationStrategyBase is P2SHDerivationStrategy p2shStrat && IsSegwitCore(p2shStrat.Inner))
                ? NBitcoin.ScriptPubKeyType.SegwitP2SH
                : NBitcoin.ScriptPubKeyType.Legacy;
        }

        private static bool IsSegwitCore(DerivationStrategyBase derivationStrategyBase)
        {
            return (derivationStrategyBase is P2WSHDerivationStrategy) ||
                   (derivationStrategyBase is DirectDerivationStrategy direct) && direct.Segwit;
        }
    }
}