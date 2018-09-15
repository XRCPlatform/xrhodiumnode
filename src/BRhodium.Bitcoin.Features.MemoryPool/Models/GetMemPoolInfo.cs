using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace BRhodium.Bitcoin.Features.MemoryPool.Models
{
    public class GetMemPoolInfo
    {
        [JsonProperty(PropertyName = "size")]
        public long Size { get; set; }

        [JsonProperty(PropertyName = "bytes")]
        public long Bytes { get; set; }

        [JsonProperty(PropertyName = "usage")]
        public long Usage { get; set; }

        [JsonProperty(PropertyName = "maxmempool")]
        public long Maxmempool { get; set; }

        [JsonProperty(PropertyName = "mempoolminfee")]
        public decimal MempoolMinFee { get; set; }

        [JsonProperty(PropertyName = "minrelaytxfee")]
        public decimal? MinRelayTxFee { get; set; }
    }
}
