using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace BRhodium.Bitcoin.Features.MemoryPool.Models
{
    public class GetTxOutSetInfo
    {
        [JsonProperty(PropertyName = "height")]
        public long Height { get; set; }

        [JsonProperty(PropertyName = "bestblock")]
        public string BestBlock { get; set; }

        [JsonProperty(PropertyName = "transactions")]
        public int Transactions { get; set; }

        [JsonProperty(PropertyName = "txouts")]
        public int TxOuts { get; set; }

        [JsonProperty(PropertyName = "bogosize")]
        public int BogoSize { get; set; }

        [JsonProperty(PropertyName = "hash_serialized_2")]
        public string Hash_serialized_2 { get; set; }

        [JsonProperty(PropertyName = "disk_size")]
        public long DiskSize { get; set; }

        [JsonProperty(PropertyName = "total_amount")]
        public decimal TotalAmount { get; set; }
    }
}
