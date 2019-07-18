using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using BRhodium.Bitcoin.Features.RPC.Converters;
using BRhodium.Bitcoin.Features.Wallet.Interfaces;

namespace BRhodium.Bitcoin.Features.Wallet.Models
{
    public abstract class TransactionModel
    {
        public TransactionModel(Network network = null)
        {
        }

        public TransactionModel(Transaction trx)
        {
            this.Hex = trx?.ToHex();
        }

        [JsonProperty(Order = 0, PropertyName = "hex")]
        public string Hex { get; set; }

        public override string ToString()
        {
            return this.Hex;
        }
    }

    [JsonConverter(typeof(ToStringJsonConverter))]
    public class TransactionBriefModel : TransactionModel
    {
        public TransactionBriefModel()
        {
        }

        public TransactionBriefModel(Transaction trx) : base(trx)
        {
        }
    }

    public class TransactionVerboseModel : TransactionModel
    {

        /**
        * <summary>
        * With provided parameters, generated a verbose list of
        * transactions that are related to a node wallet.
        * </summary>
        */
        public static List<TransactionVerboseModel> GenerateList(
            Transaction tx,
            List<IndexedTxOut> prevTxList,
            Block block,
            ChainedHeader chainedHeader,
            ChainedHeader chainedTip,
            long walletid,
            Network network,
            WalletManager walletManager)
        {
            int n = 0;
            var result = new List<TransactionVerboseModel>();
            var vins = tx.Inputs.Select(
                txin => new Models.Vin(txin.PrevOut, txin.Sequence, txin.ScriptSig)).ToList();
            var vouts = tx.Outputs.Select(
                txout => new Models.Vout(n++, txout, network)).ToList();
            var blockTime = Utils.DateTimeToUnixTime(block.Header.BlockTime);

            decimal fee = 0;
            if (!tx.IsCoinBase)
            {
                var totalInputs = prevTxList.Sum(i => i.TxOut.Value.ToUnit(MoneyUnit.Satoshi));
                fee = totalInputs - tx.TotalOut.ToUnit(MoneyUnit.Satoshi);
                fee = new Money(fee * -1, MoneyUnit.Satoshi).ToUnit(MoneyUnit.XRC);
            }

            foreach (var input in prevTxList)
            {
              
                WalletLinkedHdAddress walletLinkedHdAddress = null;
                walletManager.addressByScriptLookup.TryGetValue(input.TxOut.ScriptPubKey.Hash, out walletLinkedHdAddress);

                if (walletLinkedHdAddress != null && walletid == walletLinkedHdAddress.WalletId)
                {
                    var inputModel = new TransactionVerboseModel
                    {
                        Amount = input.TxOut.Value.ToDecimal(MoneyUnit.XRC) * -1,
                        Address = walletLinkedHdAddress.HdAddress.Address,
                        Category = "send",
                        TxId = tx.GetHash().ToString(),
                        Size = tx.GetSerializedSize(),
                        VOut = input.N,
                        Version = tx.Version,
                        LockTime = tx.LockTime,
                        TimeReceived = tx.Time,
                        BlockHeight = chainedHeader.Height,
                        BlockHash = chainedHeader.HashBlock.ToString(),
                        Time = blockTime,
                        BlockTime = blockTime,
                        Fee = fee,
                        Confirmations = chainedTip.Height - chainedHeader.Height + 1
                    };
                        result.Add(inputModel);
                    }
                }

                n = 0;
                foreach (var output in tx.Outputs)
                {
             
                    WalletLinkedHdAddress walletLinkedHdAddress = null;
                    walletManager.addressByScriptLookup.TryGetValue(output.ScriptPubKey.Hash, out walletLinkedHdAddress);

                    if (walletLinkedHdAddress != null && walletid == walletLinkedHdAddress.WalletId)
                    {
                        var outputModel = new TransactionVerboseModel
                        {
                            Amount = output.Value.ToDecimal(MoneyUnit.XRC),
                            Address = walletLinkedHdAddress.HdAddress.Address,
                            Category = "receive",
                            VOut = (uint)n,
                            TxId = tx.GetHash().ToString(),
                            Size = tx.GetSerializedSize(),
                            Version = tx.Version,
                            LockTime = tx.LockTime,
                            TimeReceived = tx.Time,
                            BlockHeight = chainedHeader.Height,
                            BlockHash = chainedHeader.HashBlock.ToString(),
                            Time = blockTime,
                            BlockTime = blockTime,
                            Fee = 0,
                            Confirmations = chainedTip.Height - chainedHeader.Height + 1
                        };
                        n++;

                        if (tx.IsCoinBase)
                        {
                            if (outputModel.Confirmations < 10)
                            {
                                outputModel.Category = "immature";
                            }
                            else
                            {
                                outputModel.Category = "generate";
                            }
                        }

                        result.Add(outputModel);
                    }
                }

                return result;
        }

        [JsonProperty(Order = 1, PropertyName = "txid")]
        public string TxId { get; set; }

        [JsonProperty(Order = 2, PropertyName = "size")]
        public int Size { get; set; }

        [JsonProperty(Order = 3, PropertyName = "version")]
        public uint Version { get; set; }

        [JsonProperty(Order = 4, PropertyName = "locktime")]
        public uint LockTime { get; set; }

        [JsonProperty(Order = 5, PropertyName = "vout")]
        public uint VOut { get; set; }

        [JsonProperty(Order = 6, PropertyName = "blockhash", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string BlockHash { get; set; }

        [JsonProperty(Order = 7, PropertyName = "confirmations", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? Confirmations { get; set; }

        [JsonProperty(Order = 8, PropertyName = "time", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public uint? Time { get; set; }

        [JsonProperty(Order = 9, PropertyName = "blocktime", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public uint? BlockTime { get; set; }


        [JsonProperty(Order = 10, PropertyName = "category", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Category { get; set; }

        [JsonProperty(Order = 11, PropertyName = "amount", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public decimal Amount { get; set; }

        [JsonProperty(Order = 12, PropertyName = "fee", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public decimal Fee { get; set; }

        [JsonProperty(Order = 13, PropertyName = "blockindex", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int BlockIndex { get; set; }

        [JsonProperty(Order = 14, PropertyName = "blockheight", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int BlockHeight { get; set; }

        [JsonProperty(Order = 15, PropertyName = "timereceived", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public uint? TimeReceived { get; set; }

        [JsonProperty(Order = 16, PropertyName = "address", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Address { get; set; }
    }

    public class Vin
    {
        public Vin()
        {
        }

        public Vin(OutPoint prevOut, Sequence sequence, NBitcoin.Script scriptSig)
        {
            if (prevOut.Hash == uint256.Zero)
            {
                this.Coinbase = Encoders.Hex.EncodeData(scriptSig.ToBytes());
            }
            else
            {
                this.TxId = prevOut.Hash.ToString();
                this.VOut = prevOut.N;
                this.ScriptSig = new Script(scriptSig);
            }
            this.Sequence = (uint)sequence;
        }

        [JsonProperty(Order = 0, PropertyName = "coinbase", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Coinbase { get; set; }

        [JsonProperty(Order = 1, PropertyName = "txid", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string TxId { get; set; }

        [JsonProperty(Order = 2, PropertyName = "vout", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public uint? VOut { get; set; }

        [JsonProperty(Order = 3, PropertyName = "scriptSig", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Script ScriptSig { get; set; }

        [JsonProperty(Order = 4, PropertyName = "sequence")]
        public uint Sequence { get; set; }
    }

    public class Vout
    {
        public Vout()
        {
        }

        public Vout(int N, TxOut txout, Network network)
        {
            this.N = N;
            this.Value = txout.Value.ToDecimal(MoneyUnit.XRC);
            this.ScriptPubKey = new ScriptPubKey(txout.ScriptPubKey, network);
        }

        [JsonConverter(typeof(BtcDecimalJsonConverter))]
        [JsonProperty(Order = 0, PropertyName = "value")]
        public decimal Value { get; set; }

        [JsonProperty(Order = 1, PropertyName = "n")]
        public int N { get; set; }

        [JsonProperty(Order = 2, PropertyName = "scriptPubKey")]
        public ScriptPubKey ScriptPubKey { get; set; }
    }

    public class Script
    {
        public Script()
        {
        }

        public Script(NBitcoin.Script script)
        {
            this.Asm = script.ToString();
            this.Hex = Encoders.Hex.EncodeData(script.ToBytes());
        }

        [JsonProperty(Order = 0, PropertyName = "asm")]
        public string Asm { get; set; }

        [JsonProperty(Order = 1, PropertyName = "hex")]
        public string Hex { get; set; }
    }

    public class ScriptPubKey : Script
    {
        public ScriptPubKey()
        {
        }

        public ScriptPubKey(NBitcoin.Script script, Network network) : base(script)
        {
            var destinations = new List<TxDestination> { script.GetDestination(network) };
            this.Type = this.GetScriptType(script.FindTemplate(network));
            if (destinations[0] == null)
            {
                destinations = script.GetDestinationPublicKeys(network)
                                    .Select(p => p.Hash)
                                    .ToList<TxDestination>();
            }
            else
            {
                if (destinations.Count == 1)
                {
                    this.ReqSigs = 1;
                    this.Addresses = new List<string> { destinations[0].GetAddress(network).ToString() };
                }
                else
                {
                    PayToMultiSigTemplateParameters multi = PayToMultiSigTemplate.Instance.ExtractScriptPubKeyParameters(network, script);
                    this.ReqSigs = multi.SignatureCount;
                    this.Addresses = multi.PubKeys.Select(m => m.GetAddress(network).ToString()).ToList();
                }
            }
        }

        [JsonProperty(Order = 2, PropertyName = "reqSigs", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? ReqSigs { get; set; }

        [JsonProperty(Order = 3, PropertyName = "type", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Type { get; set; }

        [JsonProperty(Order = 4, PropertyName = "addresses", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public List<string> Addresses { get; set; }

        protected string GetScriptType(ScriptTemplate template)
        {
            if (template == null)
                return "nonstandard";
            switch (template.Type)
            {
                case TxOutType.TX_PUBKEY:
                    return "pubkey";

                case TxOutType.TX_PUBKEYHASH:
                    return "pubkeyhash";

                case TxOutType.TX_SCRIPTHASH:
                    return "scripthash";

                case TxOutType.TX_MULTISIG:
                    return "multisig";

                case TxOutType.TX_NULL_DATA:
                    return "nulldata";
            }
            return "nonstandard";
        }
    }
}
