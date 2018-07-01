using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace BRhodium.Bitcoin.Features.RPC.Models
{
    public class ChainBlockModel
    {
        [JsonProperty(Order = 0, PropertyName = "height")]
        public int Height { get; set; }

        [JsonProperty(Order = 1, PropertyName = "age")]
        public DateTimeOffset Age { get; set; }

        [JsonProperty(Order = 2, PropertyName = "transactions")]
        public List<TransactionChainBlockModel> Transactions { get; set; }

        [JsonProperty(Order = 3, PropertyName = "totalsatoshi")]
        public double TotalSatoshi { get; set; }

        [JsonProperty(Order = 4, PropertyName = "size")]
        public int Size { get; set; }

        [JsonProperty(Order = 5, PropertyName = "hash")]
        public string Hash { get; set; }
    }

    public class TransactionChainBlockModel
    {
        [JsonProperty(Order = 0, PropertyName = "hash")]
        public string Hash { get; set; }

        [JsonProperty(Order = 1, PropertyName = "satoshi")]
        public double Satoshi { get; set; }
    }
}
