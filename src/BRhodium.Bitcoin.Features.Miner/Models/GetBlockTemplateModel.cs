using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using NBitcoin;
using Newtonsoft.Json;

namespace BRhodium.Bitcoin.Features.Miner.Models
{
    public class GetBlockTemplateModel
    {
       
        [JsonProperty(PropertyName = "target")]
        public string Target { get; set; }

        [JsonProperty(Order = 1,PropertyName = "bits")]
        public string Bits { get; set; }

        [JsonProperty(Order = 2,PropertyName = "curtime")]
        public uint Curtime { get; set; }

        [JsonProperty(Order = 8, PropertyName = "version")]
        public int Version { get; set; }

        [JsonProperty(Order = 4, PropertyName = "previousblockhash")]
        public string PreviousBlockHash { get; set; }

        [JsonProperty(Order = 7, PropertyName = "transactions")]
        public List<TransactionContractModel> Transactions { get; set; }

        [JsonProperty(Order = 3, PropertyName = "height")]
        public int Height { get; set; }

        [JsonProperty(Order = 9, PropertyName = "coinbaseaux")]
        public CoinbaseauxFlagsContractModel Coinbaseaux { get; set; }

        [JsonProperty(Order = 10, PropertyName = "coinbasevalue")]
        public long CoinbaseValue { get; set; }

        [JsonProperty(PropertyName = "mutable")]
        public List<string> Mutable { get; set; }

        [JsonProperty(PropertyName = "noncerange")]
        public string NonceRange { get; set; }

        [JsonProperty(PropertyName = "capabilities")]
        public List<string> Capabilities { get; set; }

        [JsonProperty(PropertyName = "rules")]
        public List<string> Rules { get; set; }

        [JsonProperty(PropertyName = "vbavailable")]
        public List<string> Vbavailable { get; set; }

        [JsonProperty(PropertyName = "vbrequired")]
        public int Vbrequired { get; set; }

        [JsonProperty(PropertyName = "weightlimit")]
        public int Weightlimit { get; set; }

        [JsonProperty(Order = 5, PropertyName = "sigoplimit")]
        public int Sigoplimit { get; set; }

        [JsonProperty(Order = 6, PropertyName = "sizelimit")]
        public int Sizelimit { get; set; }

        [JsonProperty(PropertyName = "mintime")]
        public long Mintime { get; set; }

        [JsonProperty(PropertyName = "coinbasetxn")]
        public Transaction Coinbasetxn { get; internal set; }
    }

    public class TransactionContractModel
    {
        [JsonProperty(PropertyName = "data")]
        public string Data { get; set; }

        [JsonProperty(PropertyName = "txid")]
        public string Txid { get; set; }

        [JsonProperty(PropertyName = "hash")]
        public string Hash { get; set; }
        
        [JsonProperty(PropertyName = "depends", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, long> Depends { get; set; }

        [JsonProperty(PropertyName = "Fee")]
        public long Fee { get; set; }

        [DefaultValue(-1L)]
        [JsonProperty(PropertyName = "sigops", DefaultValueHandling = DefaultValueHandling.Ignore )]
        public long Sigops { get; set; }

        [JsonProperty(PropertyName = "weight")]
        public long Weight { get; set; }

        [JsonProperty(PropertyName = "required")]
        public bool Required { get; set; }

    }

    public class CoinbaseauxFlagsContractModel
    {
        [JsonProperty(PropertyName = "flags")]
        public string Flags { get; set; }
    }
}
