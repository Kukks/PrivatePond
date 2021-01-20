namespace PrivatePond.Data
{
    public class Wallet
    {
        public string Id { get; set; }
        public string DerivationStrategy { get; set; }

        public string WalletBlobJson { get; set; }
        public bool Enabled { get; set; }

        // public WalletOption GetBlob()
        // {
        //     return string.IsNullOrEmpty(WalletBlobJson) ? null : JsonSerializer.Deserialize<WalletBlob>(WalletBlobJson);
        // }
        //
        // public void SetBlob(WalletBlob blob)
        // {
        //     JsonSerializer.Serialize(blob);
        // }
    }
}