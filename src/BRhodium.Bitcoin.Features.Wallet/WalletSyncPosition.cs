using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;

namespace BRhodium.Bitcoin.Features.Wallet
{
    public class WalletSyncPosition:IBitcoinSerializable
    {
        private int height;
        private uint256 hashBlock;
        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.height);
            stream.ReadWrite(ref this.hashBlock);
        }

        [JsonProperty(PropertyName = "hash")]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 BlockHash { get => this.hashBlock; set => this.hashBlock = value; }

        [JsonProperty(PropertyName = "height")]       
        public int Height { get => this.height; set => this.height = value; }
    }
}