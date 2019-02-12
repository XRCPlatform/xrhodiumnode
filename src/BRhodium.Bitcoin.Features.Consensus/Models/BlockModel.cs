using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Newtonsoft.Json;

namespace BRhodium.Bitcoin.Features.Consensus.Models
{
    public class BlockModel
    {
        [JsonProperty(PropertyName = "hash")]
        public string Hash { get; set; }

        [JsonProperty(PropertyName = "confirmations")]
        public Int32 Confirmations { get; set; }

        [JsonProperty(PropertyName = "size")]
        public Int32 Size { get; set; }

        [JsonProperty(PropertyName = "height")]
        public Int32 Height { get; set; }

        [JsonProperty(PropertyName = "weight")]
        public int Weight { get; set; }

        [JsonProperty(PropertyName = "version")]
        public Int32 Version { get; set; }

        [JsonProperty(PropertyName = "versionhex")]
        public string VersionHex { get; set; }

        [JsonProperty(PropertyName = "merkleroot")]
        public string MerkleRoot { get; set; }

        [JsonProperty(PropertyName = "tx")]
        public List<string> Tx { get; set; }

        [JsonProperty(PropertyName = "time")]
        public Int32 Time { get; set; }

        [JsonProperty(PropertyName = "nonce")]
        public UInt32 Nonce { get; set; }

        [JsonProperty(PropertyName = "bits")]
        public string Bits { get; set; }

        [JsonProperty(PropertyName = "difficulty")]
        public double Difficulty { get; set; }

        [JsonProperty(PropertyName = "chainwork")]
        public string ChainWork { get; set; }

        [JsonProperty(PropertyName = "nextblockhash")]
        public string NextBlockHash { get; set; }

        [JsonProperty(PropertyName = "previousblockhash")]
        public string PreviousBlockHash { get; set; }

        [JsonProperty(PropertyName = "proofhash")]
        public uint256 ProofHash { get; set; }

        public BlockModel()
        {
            Tx = new List<string>();
        }

    }
}
