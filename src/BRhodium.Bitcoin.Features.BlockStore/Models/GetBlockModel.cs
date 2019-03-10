using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Transactions;
using NBitcoin;
using Newtonsoft.Json;

namespace BRhodium.Bitcoin.Features.BlockStore.Models
{
    /// <summary>
    ///   Representing the full result of the RPC function "getblock"
    /// </summary>
    public class GetBlockModel
    {
        /// <summary>
        ///   Number of transactions
        /// </summary>
        [JsonProperty(PropertyName = "ntx")]
        public int TransactionsCount { get; set; }

        /// <summary>
        ///   The Hash of the block.
        /// </summary>
        [JsonProperty(PropertyName = "hash")]
        public string Hash { get; set; }

        /// <summary>
        ///   The number of confirmations for the current block.
        /// </summary>
        [JsonProperty(PropertyName = "confirmations")]
        public Int32 Confirmations { get; set; }

        /// <summary>
        ///   The size, in bytes.
        /// </summary>
        [JsonProperty(PropertyName = "size")]
        public int Size { get; set; }

        /// <summary>
        ///   The serialized size.
        /// </summary>
        [JsonProperty(PropertyName = "strippedsize")]
        public int StrippedSize { get; set; }

        /// <summary>
        ///   The block height or index
        /// </summary>
        [JsonProperty(PropertyName = "height")]
        public int Height { get; set; }

        /// <summary>
        ///   The block weight as defined in BIP 141
        /// </summary>
        [JsonProperty(PropertyName = "weight")]
        public int Weight { get; set; }

        /// <summary>
        ///   The block version
        /// </summary>
        [JsonProperty(PropertyName = "version")]
        public int Version { get; set; }

        /// <summary>
        ///   The block version formatted in hexadecimal
        /// </summary>
        [JsonProperty(PropertyName = "versionhex")]
        public string VersionHex { get; set; }

        /// <summary>
        ///   The merkle root
        /// </summary>
        [JsonProperty(PropertyName = "merkleroot")]
        public string Merkleroot { get; set; }

        /// <summary>
        ///   The transaction ids
        /// </summary>
        [JsonProperty(PropertyName = "tx")]
        public List<string> Tx { get; set; }

        /// <summary>
        ///   The block time in seconds since epoch (Jan 1 1970 GMT)
        /// </summary>
        [JsonProperty(PropertyName = "time")]
        public int Time { get; set; }

        /// <summary>
        ///
        /// </summary>
        [JsonProperty(PropertyName = "mediantime")]
        public long Mediantime { get; set; }

        /// <summary>
        ///   The nonce
        /// </summary>
        [JsonProperty(PropertyName = "nonce")]
        public uint Nonce { get; set; }

        /// <summary>
        ///   The bits
        /// </summary>
        [JsonProperty(PropertyName = "bits")]
        public string Bits { get; set; }

        /// <summary>
        ///   The difficulty
        /// </summary>
        [JsonProperty(PropertyName = "difficulty")]
        public double Difficulty { get; set; }

        /// <summary>
        ///    Expected number of hashes required to produce the chain up to this block (in hex)
        /// </summary>
        [JsonProperty(PropertyName = "chainwork")]
        public string Chainwork { get; set; }

        /// <summary>
        ///   The hash of the next block
        /// </summary>
        [JsonProperty(PropertyName = "nextblockhash")]
        public string NextBlockHash { get; set; }

        /// <summary>
        ///   The hash of the previous block
        /// </summary>
        [JsonProperty(PropertyName = "previousblockhash")]
        public string PreviousBlockHash { get; set; }

        /// <summary>
        ///   Hash of nonce + block to demonstrate PoW in X13
        /// </summary>

        [JsonProperty(PropertyName = "proofhash")]
        public uint256 ProofHash { get; set; }

    }

    /// <summary>
    ///   Model with list of transaction information.
    /// </summary>
    public class GetBlockWithTransactionModel<T> : GetBlockModel
    {
        /// <summary>
        ///
        /// </summary>
        [JsonProperty(PropertyName = "tx")]
        public List<T> Transactions { get; set; }
    }

    /// <summary>
    ///   Model with just transaction Ids
    /// </summary>
    public class GetTransactionBlockModel
    {
        /// <summary>
        ///
        /// </summary>
        public GetTransactionBlockModel(string transactionId)
        {
            this.Transactionid = transactionId;
        }

        /// <summary>
        ///   Transaction id
        /// </summary>
        [JsonProperty(PropertyName = "transactionid")]
        public string Transactionid { get; set; }
    }

    /// <summary>
    ///   Model with transaction data.
    /// </summary>
    public class GetTransactionDateBlockModel
    {
        /// <summary>
        ///
        /// </summary>
        public GetTransactionDateBlockModel(string data)
        {
            this.Data = data;
        }

        /// <summary>
        ///
        /// </summary>
        [JsonProperty(PropertyName = "data")]
        public string Data { get; set; }
    }
}
