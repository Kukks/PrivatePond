using System.Linq;
using System.Threading.Tasks;
using NBitcoin;

namespace PrivatePond.Data
{
    public class PrivatePondOptions
    {
        public const string OptionsConfigSection = "PrivatePond";
        public TaskCompletionSource WalletsConfigured = new TaskCompletionSource();
        public NetworkType NetworkType { get; set; }

        public WalletOption[] Wallets { get; set; } = new WalletOption[0];
        public string KeysDir { get; set; }
        public int MinimumConfirmations { get; set; } = 6;

        //Transfers processed every x minutes
        public int BatchTransfersEvery { get; set; }
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
        //the ideal max amount of funds in percentage of the sum of total enabled wallet balances
        public decimal? MaximumFunds { get; set; }
        //the ideal minimum amount of funds in percentage of the sum of total enabled wallet balances
        public decimal? IdealBalance { get; set; }
        //if the min/max is reached, suggest a transfer from/to this wallet
        public string WalletReplenishmentSource { get; set; }
        //If this wallet can be used for user withdrawal transfers
        public decimal? AllowForTransfersUpTo { get; set; }
        public decimal? AllowForTransfersFrom { get; set; }
    }
}