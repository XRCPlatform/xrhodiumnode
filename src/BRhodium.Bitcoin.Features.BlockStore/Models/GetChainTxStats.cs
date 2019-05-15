using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace BRhodium.Bitcoin.Features.BlockStore.Models
{
    public class GetChainTxStats
    {
        [JsonProperty(PropertyName = "time")]
        public uint Time { get; set; }

        [JsonProperty(PropertyName = "txcount")]
        public int TxCount { get; set; }

        [JsonProperty(PropertyName = "window_final_block_hash")]
        public string WindowFinalBlockHash { get; set; }

        [JsonProperty(PropertyName = "window_block_count")]
        public int WindowBlockCount { get; set; }

        [JsonProperty(PropertyName = "window_tx_count")]
        public int WindowTxCount { get; set; }

        [JsonProperty(PropertyName = "window_interval")]
        public double WindowInterval { get; set; }

        [JsonProperty(PropertyName = "txrate")]
        public double TxRate { get; set; }

    }
}
