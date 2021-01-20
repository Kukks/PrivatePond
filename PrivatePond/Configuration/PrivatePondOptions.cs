using System.Linq;
using NBitcoin;

namespace PrivatePond.Data
{
    public class PrivatePondOptions
    {
        public const string OptionsConfigSection = "PrivatePond";
        public NetworkType NetworkType { get; set; }

        public WalletOption[] Wallets { get; set; } = new WalletOption[0];
        public string KeysDir { get; set; }
        public int MinimumConfirmations { get; set; } = 6;

        public WalletOption GetWalletById(string walletId)
        {
            return Wallets.SingleOrDefault(option => option.WalletId == walletId);
        }
    }

    public class WalletOption
    {
        //configured by the application itself
        public string WalletId { get; set; }
        //the derivation scheme in NBX format
        public string DerivationScheme { get; set; }
        //If this wallet can be suggested to users to do deposits
        public bool AllowForDeposits { get; set; }
        //mandatory keypath needed to create PSBTs for transfers
        public string[] RootedKeyPaths { get; set; }
        //the ideal maximum amount of funds 
        public decimal? MaximumFunds { get; set; }
        //the ideal minimum amount of funds 
        public decimal? MinimumFunds { get; set; }
        //if the min is reached, suggest a transfer from this wallet
        public string WalletReplenishmentSource { get; set; }
        //If the max is reached, suggest a transfer to this wallet (derivationscheme nbx format is value)
        public string WalletDestinationAfterMaximumReached { get; set; }
        //If this wallet can be used for user withdrawal transfers
        public bool AllowForWithdrawals { get; set; }
        //Transfers processed every x minutes
        public int BatchTransfersEvery { get; set; }
    }
    
}