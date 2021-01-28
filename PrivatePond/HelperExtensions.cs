using NBitcoin;
using NBitcoin.Payment;

namespace PrivatePond
{
    public class HelperExtensions
    {
        
        public static string GetAddress(string destination, Network network, out ScriptPubKeyType? scriptPubKeyType, out decimal? amount)
        {
            BitcoinAddress address;
            amount = null;
            if (destination.ToLowerInvariant().StartsWith("bitcoin:"))
            {
                var bip21 = new BitcoinUrlBuilder(destination, network);
                address = bip21.Address;
                amount = bip21.Amount?.ToDecimal(MoneyUnit.BTC);
            }
            else
                address = BitcoinAddress.Create(destination, network);

            scriptPubKeyType = null;
            switch (address)
            {
                case BitcoinPubKeyAddress bitcoinPubKeyAddress:
                    scriptPubKeyType = ScriptPubKeyType.Legacy;
                    break;
                case BitcoinScriptAddress bitcoinScriptAddress:
                    scriptPubKeyType = ScriptPubKeyType.SegwitP2SH;
                    break;
                case BitcoinWitPubKeyAddress bitcoinWitPubKeyAddress:
                    scriptPubKeyType = ScriptPubKeyType.Segwit;
                    break;
                case BitcoinWitScriptAddress bitcoinWitScriptAddress:
                    scriptPubKeyType = ScriptPubKeyType.Segwit;

                    break;
            }

            return address.ToString();
        }
    }
}