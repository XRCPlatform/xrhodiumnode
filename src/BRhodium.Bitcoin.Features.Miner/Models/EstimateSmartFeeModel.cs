using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace BRhodium.Bitcoin.Features.Miner.Models
{
    public class EstimateSmartFeeModel
    {
        [JsonProperty(PropertyName = "feerate")]
        public decimal FeeRate { get; set; }

        [JsonProperty(PropertyName = "blocks")]
        public int Blocks { get; set; }
    }
}
