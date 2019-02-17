using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace BRhodium.Bitcoin.Features.BlockStore.Models
{
    public class GetBlockChainInfoModel
    {
        [JsonProperty(PropertyName = "chain")]
        public string Chain { get; set; }

        [JsonProperty(PropertyName = "blocks")]
        public int Blocks { get; set; }

        [JsonProperty(PropertyName = "headers")]
        public int Headers { get; set; }

        [JsonProperty(PropertyName = "bestblockhash")]
        public string BestBlockHash { get; set; }

        [JsonProperty(PropertyName = "difficulty")]
        public double Difficulty { get; set; }

        [JsonProperty(PropertyName = "mediantime")]
        public double MedianTime { get; set; }

        [JsonProperty(PropertyName = "initialblockdownload")]
        public bool InitialBlockDownload { get; set; }

        [JsonProperty(PropertyName = "chainwork")]
        public string ChainWork { get; set; }

        [JsonProperty(PropertyName = "size_on_disk")]
        public int SizeOnDisk { get; set; }

        [JsonProperty(PropertyName = "pruned")]
        public bool Pruned { get; set; }

        [JsonProperty(PropertyName = "pruneheight")]
        public int? PruneHeight { get; set; }

        [JsonProperty(PropertyName = "automatic_pruning")]
        public bool AutomaticPruning { get; set; }

        [JsonProperty(PropertyName = "prune_target_size")]
        public int? PruneTargetSize { get; set; }

        [JsonProperty(PropertyName = "softforks")]
        public SoftForks SoftForks { get; set; }

        [JsonProperty(PropertyName = "bip9_softforks")]
        public BIP9SoftForks BIP9SoftForks { get; set; }
    }

    public class SoftForks
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "version")]
        public int Version { get; set; }

        [JsonProperty(PropertyName = "reject")]
        public object Reject { get; set; }
    }

    public class BIP9SoftForks
    {
        public Dictionary<string, object> BIP9SoftFork { get; set; }
    }
}
