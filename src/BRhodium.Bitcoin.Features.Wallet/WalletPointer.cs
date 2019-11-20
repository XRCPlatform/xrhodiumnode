namespace BRhodium.Bitcoin.Features.Wallet
{
    public class WalletPointer
    {
        private long id;
        private string name;

        public WalletPointer(long walletId, string WalletName)
        {
            this.WalletId = walletId;
            this.WalletName = WalletName;
        }

        public string WalletName { get => this.name; set => this.name = value; }
        public long WalletId { get => this.id; set => this.id = value; }
    }
}
