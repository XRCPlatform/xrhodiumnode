using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace BRhodium.Bitcoin.Features.Wallet.Models
{
    public class GetWalletInfoModel
    {
        [JsonProperty(PropertyName = "walletname")]
        public string WalletName { get; set; }

        [JsonProperty(PropertyName = "walletversion")]
        public string WalletVersion { get; set; }

        [JsonProperty(PropertyName = "balance")]
        public decimal Balance { get; set; }

        [JsonProperty(PropertyName = "unconfirmed_balance")]
        public decimal UnconfirmedBalance { get; set; }

        [JsonProperty(PropertyName = "immature_balance")]
        public decimal Immaturebalance { get; set; }

        [JsonProperty(PropertyName = "txcount")]
        public int TxCount { get; set; }

        [JsonProperty(PropertyName = "keypoololdest")]
        public long KeyPoolOldest { get; set; }

        [JsonProperty(PropertyName = "keypoolsize")]
        public long KeyPoolSize { get; set; }

        [JsonProperty(PropertyName = "keypoolsize_hd_internal")]
        public long KeyPoolSizeHdInternal { get; set; }

        [JsonProperty(PropertyName = "unlocked_until")]
        public long UnlockedUntil { get; set; }

        [JsonProperty(PropertyName = "paytxfee")]
        public decimal PayTxFee { get; set; }
        
    }
}
