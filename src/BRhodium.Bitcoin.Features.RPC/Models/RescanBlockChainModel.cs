using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace BRhodium.Bitcoin.Features.RPC.Models
{
    public class RescanBlockChainModel
    {
        [JsonProperty(PropertyName = "start_height")]
        public int StartHeight { get; set; }

        [JsonProperty(PropertyName = "stop_height")]
        public int StopHeight { get; set; }
    }
}
