using NBitcoin;
using Newtonsoft.Json;
using BRhodium.Node.Utilities;

namespace BRhodium.Bitcoin.Features.MemoryPool.Models
{
    /// <summary>
    /// A model returned to an RPC gettxout request
    /// </summary>
    public class GetTxOutModel
    {
        public GetTxOutModel()
        {
        }

        public GetTxOutModel(UnspentOutputs unspentOutputs, uint vout, Network network, ChainedHeader tip)
        {
            if (unspentOutputs != null)
            {
                var output = unspentOutputs.TryGetOutput(vout);
                this.BestBlock = tip.HashBlock;
                this.Coinbase = unspentOutputs.IsCoinbase;
                this.Confirmations = NetworkExtensions.MempoolHeight == unspentOutputs.Height ? 0 : tip.Height - (int)unspentOutputs.Height + 1;
                if (output != null)
                {
                    this.Value = output.Value.ToDecimal(MoneyUnit.XRC);
                    this.ScriptPubKey = output.ScriptPubKey.ToString();
                }
            }
        }

        /// <summary>
        /// The best block in relation to the transaction.
        /// </summary>
        [JsonProperty(Order = 0, PropertyName = "bestblock")]
        public uint256 BestBlock { get; set; }

        /// <summary>
        /// Number of confirmations from the block.
        /// </summary>
        [JsonProperty(Order = 1, PropertyName = "confirmations")]
        public int Confirmations { get; set; }

        /// <summary>
        /// Value of UTXO in XRC
        /// </summary>
        [JsonProperty(Order = 2, PropertyName = "value")]
        public decimal Value { get; set; }

        /// <summary>
        /// The script pub key unserialized.
        /// </summary>
        [JsonProperty(Order = 3, PropertyName = "scriptPubKey")]
        public string ScriptPubKey { get; set; }

        /// <summary>
        /// Is tx a coinbase tx?
        /// </summary>
        [JsonProperty(Order = 4, PropertyName = "coinbase")]
        public bool Coinbase { get; set; }
    }
}
