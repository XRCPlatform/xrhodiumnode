using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace BRhodium.Bitcoin.Features.Miner.Models
{
    public class GetMiningInfo : ICloneable
    {
        /// <summary>The current block.</summary>
        [JsonProperty(PropertyName = "blocks")]
        public long Blocks { get; set; }

        /// <summary>Size of the next block the node wants to mine in bytes.</summary>
        [JsonProperty(PropertyName = "currentblocksize")]
        public long CurrentBlockSize { get; set; }

        /// <summary>Gets or sets the current block weight.</summary>
        [JsonProperty(PropertyName = "currentblockweight")]
        public long CurrentBlockWeight { get; set; }

        /// <summary>Number of transactions the node wants to put in the next block.</summary>
        [JsonProperty(PropertyName = "currentblocktx")]
        public long CurrentBlockTx { get; set; }

        /// <summary>Target difficulty that the next block must meet.</summary>
        [JsonProperty(PropertyName = "difficulty")]
        public double Difficulty { get; set; }

        /// <summary>The network hashes per second.</summary>
        [JsonProperty(PropertyName = "networkhashps")]
        public double NetworkHashps { get; set; }

        /// <summary>The size of the mempool.</summary>
        [JsonProperty(PropertyName = "pooledtx")]
        public long PooledTx { get; set; }

        /// <summary>Current network name as defined in BIP70 (main, test, regtest).</summary>
        [JsonProperty(PropertyName = "chain")]
        public string Chain { get; set; }

        /// <summary>Any network and blockchain warnings.</summary>
        [JsonProperty(PropertyName = "warnings")]
        public string Warnings { get; set; }

        public object Clone()
        {
            GetMiningInfo res = new GetMiningInfo
            {
                Blocks = this.Blocks,
                CurrentBlockSize = this.CurrentBlockSize,
                CurrentBlockTx = this.CurrentBlockTx,
                PooledTx = this.PooledTx,
                Difficulty = this.Difficulty,
                NetworkHashps = this.NetworkHashps,
                Chain = this.Chain,
                Warnings = this.Warnings
            };

            return res;
        }
    }
}
