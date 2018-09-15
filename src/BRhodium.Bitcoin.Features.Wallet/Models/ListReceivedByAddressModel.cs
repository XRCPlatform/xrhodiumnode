using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Newtonsoft.Json;

namespace BRhodium.Bitcoin.Features.Wallet.Models
{
    public class ListReceivedByAddressModel
    {
        [JsonProperty(PropertyName = "address")]
        public string Address { get; set; }

        [JsonProperty(PropertyName = "amount")]
        public decimal Amount { get; set; }

        [JsonProperty(PropertyName = "confirmations")]
        public int Confirmations { get; set; }

        [JsonProperty(PropertyName = "txids")]
        public List<uint256> TxIds { get; set; }
    }
}
