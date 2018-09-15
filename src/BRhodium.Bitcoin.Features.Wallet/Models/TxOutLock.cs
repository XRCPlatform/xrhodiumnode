using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace BRhodium.Bitcoin.Features.Wallet.Models
{
    public class TxOutLock
    {
        [JsonProperty(PropertyName = "txid")]
        public string TxId { get; set; }

        [JsonProperty(PropertyName = "vout")]
        public int Vout { get; set; }
    }
}
