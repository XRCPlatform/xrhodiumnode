using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Transactions;
using Newtonsoft.Json;

namespace BRhodium.Bitcoin.Features.BlockStore.Models
{
    public class GetBlockModel<T>
    {
        [JsonProperty(PropertyName = "hash")]
        public string Hash { get; set; }

        [JsonProperty(PropertyName = "size")]
        public int Size { get; set; }

        [JsonProperty(PropertyName = "strippedsize")]
        public int Strippedsize { get; set; }

        [JsonProperty(PropertyName = "height")]
        public int Height { get; set; }

        [JsonProperty(PropertyName = "weight")]
        public int Weight { get; set; }

        [JsonProperty(PropertyName = "version")]
        public int Version { get; set; }

        [JsonProperty(PropertyName = "versionhex")]
        public string VersionHex { get; set; }

        [JsonProperty(PropertyName = "merkleroot")]
        public string Merkleroot { get; set; }

        [JsonProperty(PropertyName = "mediantime")]
        public long Mediantime { get; set; }

        [JsonProperty(PropertyName = "nonce")]
        public uint Nonce { get; set; }

        [JsonProperty(PropertyName = "bits")]
        public string Bits { get; set; }

        [JsonProperty(PropertyName = "difficulty")]
        public double Difficulty { get; set; }

        [JsonProperty(PropertyName = "chainwork")]
        public string Chainwork { get; set; }

        [JsonProperty(PropertyName = "previousblockhash")]
        public string PreviousBlockHash { get; set; }

        [JsonProperty(PropertyName = "nextblockhash")]
        public string NextBlockHash { get; set; }

        [JsonProperty(PropertyName = "tx")]
        public List<T> Transactions { get; set; }
    }

    public class GetTransactionBlockModel
    {
        public GetTransactionBlockModel(string transactionId)
        {
            this.Transactionid = transactionId;
        }

        [JsonProperty(PropertyName = "transactionid")]
        public string Transactionid { get; set; }
    }

    public class GetTransactionDateBlockModel
    {
        public GetTransactionDateBlockModel(string data)
        {
            this.Data = data;
        }

        [JsonProperty(PropertyName = "data")]
        public string Data { get; set; }
    }
}