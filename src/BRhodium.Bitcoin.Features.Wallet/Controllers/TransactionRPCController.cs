using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using BRhodium.Bitcoin.Features.BlockStore;
using BRhodium.Bitcoin.Features.Consensus.Interfaces;
using BRhodium.Bitcoin.Features.Consensus.Models;
using BRhodium.Bitcoin.Features.RPC;
using BRhodium.Bitcoin.Features.Wallet.Broadcasting;
using BRhodium.Bitcoin.Features.Wallet.Interfaces;
using BRhodium.Bitcoin.Features.Wallet.Models;
using BRhodium.Node;
using BRhodium.Node.Base;
using BRhodium.Node.Configuration;
using BRhodium.Node.Connection;
using BRhodium.Node.Controllers;
using BRhodium.Node.Interfaces;
using BRhodium.Node.Utilities;
using BRhodium.Node.Utilities.JsonContract;
using BRhodium.Node.Utilities.JsonErrors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;

namespace BRhodium.Bitcoin.Features.Wallet.Controllers
{
    /// <summary>
    /// Class offering method working with Raw Transactions
    /// </summary>
    /// <seealso cref="BRhodium.Node.Controllers.FeatureController" />
    [Controller]
    public class TransactionRPCController : FeatureController
    {
        private readonly ILogger logger;

        public IBroadcasterManager broadcasterManager;
        private readonly IPooledTransaction pooledTransaction;
        public IWalletManager walletManager { get; set; }
        private readonly IBlockRepository blockRepository;
        public IConsensusLoop ConsensusLoop { get; private set; }
        public IWalletFeePolicy WalletFeePolicy { get; private set; }

        public TransactionRPCController(
            ILoggerFactory loggerFactory,
            IWalletManager walletManager,
            IBlockRepository blockRepository,
            IWalletFeePolicy walletFeePolicy,
            IFullNode fullNode,
            NodeSettings nodeSettings,
            Network network,
            ConcurrentChain chain,
            IConnectionManager connectionManager,
            IBroadcasterManager broadcasterManager,
            IPooledTransaction pooledTransaction, 
            IChainState chainState = null,
            IConsensusLoop consensusLoop = null) : base(fullNode, nodeSettings, network, chain, chainState, connectionManager)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.broadcasterManager = broadcasterManager;
            this.walletManager = walletManager;
            this.blockRepository = blockRepository;
            this.ConsensusLoop = consensusLoop;
            this.WalletFeePolicy = walletFeePolicy;
            this.pooledTransaction = pooledTransaction;
        }

        /// <summary>
        /// Combine multiple partially signed transactions into one transaction. The combined transaction may be another partially signed transaction or a fully signed transaction.
        /// </summary>
        /// <param name="txs">A json array of hex strings of partially signed transactions.</param>
        /// <returns>The hex-encoded raw transaction with signature(s)</returns>
        [ActionName("combinerawtransaction")]
        [ActionDescription("Combine multiple partially signed transactions into one transaction. The combined transaction may be another partially signed transaction or a fully signed transaction.")]
        public IActionResult CombineRawTransaction(string[] txs)
        {
            try
            {
                string txHex = string.Empty;
                if ((txs != null) && (txs.Length > 0))
                {
                    if (txs.Length == 1) {
                        txHex = txs[0];
                    }
                    else
                    {
                        var builder = new TransactionBuilder(this.Network);
                        var parentTx = Transaction.Load(txs[0], this.Network);

                        for (int i = 1; i < txs.Length; i++) //skip first
                        {
                            var tx = Transaction.Load(txs[i], this.Network);
                            parentTx = builder.CombineSignatures(parentTx, tx);
                        }

                        txHex = parentTx.ToHex(this.Network);
                    }
                }
                else
                {
                    throw new ArgumentNullException("txs");
                }

                return this.Json(ResultHelper.BuildResultResponse(txHex));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Create a transaction spending the given inputs and creating new outputs. Outputs can be addresses or data. Returns hex - encoded raw transaction. Note that the transaction's inputs are not signed, and it is not stored in the wallet or transmitted to the network.
        /// </summary>
        /// <param name="inputs">A json array of json objects</param>
        /// <param name="outputs">A json object with outputs</param>
        /// <returns>Hex string of the transaction</returns>
        [ActionName("createrawtransaction")]
        [ActionDescription("Create a transaction spending the given inputs and creating new outputs. Outputs can be addresses or data. Returns hex - encoded raw transaction. Note that the transaction's inputs are not signed, and it is not stored in the wallet or transmitted to the network.")]
        public IActionResult CreateRawTransaction(string inputs, string outputs)
        {
            try
            {
                if (string.IsNullOrEmpty(inputs))
                {
                    throw new ArgumentNullException("inputs");
                }
                if (string.IsNullOrEmpty(outputs))
                {
                    throw new ArgumentNullException("outputs");
                }

                TxInList txIns = JsonConvert.DeserializeObject<TxInList>(inputs);
                Dictionary<string, decimal> parsedOutputs = JsonConvert.DeserializeObject<Dictionary<string, decimal>>(outputs);

                Transaction transaction = new Transaction();
                foreach (var input in txIns)
                {
                    transaction.AddInput(input);
                }

                foreach (KeyValuePair<string, decimal> entry in parsedOutputs)
                {
                    var destination = BitcoinAddress.Create(entry.Key, this.Network).ScriptPubKey;
                    transaction.AddOutput(new TxOut(new Money(entry.Value, MoneyUnit.MilliBTR), destination));
                }

                var txHex = transaction.ToHex();
                return this.Json(ResultHelper.BuildResultResponse(txHex));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        ///  Return a JSON object representing the serialized, hex-encoded transaction.
        /// </summary>
        /// <param name="hex">The transaction hex string</param>
        /// <returns>Result transaction object</returns>
        [ActionName("decoderawtransaction")]
        [ActionDescription("Return a JSON object representing the serialized, hex-encoded transaction.")]
        public IActionResult DecodeRawTransaction(string hex)
        {
            try
            {
                if (string.IsNullOrEmpty(hex))
                {
                    throw new ArgumentNullException("hex");
                }

                var tx = Transaction.Load(hex, this.Network);

                return this.Json(ResultHelper.BuildResultResponse(tx));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Decode a hex-encoded script.
        /// </summary>
        /// <param name="hex">The hex encoded script.</param>
        /// <returns>Result is decoded hex</returns>
        [ActionName("decodescript")]
        [ActionDescription("Decode a hex-encoded script.")]
        public IActionResult DecodeScript(string hex)
        {
            try
            {
                if (string.IsNullOrEmpty(hex))
                {
                    throw new ArgumentNullException("hex");
                }

                var script = new NBitcoin.Script(Encoders.Hex.DecodeData(hex));
                var scriptPubKey = new ScriptPubKey(script, this.Network);

                return this.Json(ResultHelper.BuildResultResponse(scriptPubKey));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Add inputs to a transaction until it has enough in value to meet its out value. This will not modify existing inputs, and will add at most one change output to the outputs. No existing outputs will be modified unless \"subtractFeeFromOutputs\" is specified. Note that inputs which were signed may need to be resigned after completion since in/ outputs have been added. The inputs added will not be signed, use signrawtransaction for that. Note that all existing inputs must have their previous output transaction be in the wallet. Note that all inputs selected must be of standard form and P2SH scripts must be in the wallet using importaddress or addmultisigaddress(to calculate fees). You can see whether this is the case by checking the \"solvable\" field in the listunspent output. Only pay-to-pubkey, multisig, and P2SH versions thereof are currently supported for watch-only
        /// </summary>
        /// <param name="hex">The hex string of the raw transaction</param>
        /// <returns></returns>
        [ActionName("fundrawtransaction")]
        [ActionDescription("Add inputs to a transaction until it has enough in value to meet its out value. This will not modify existing inputs, and will add at most one change output to the outputs. No existing outputs will be modified unless \"subtractFeeFromOutputs\" is specified. Note that inputs which were signed may need to be resigned after completion since in/ outputs have been added. The inputs added will not be signed, use signrawtransaction for that. Note that all existing inputs must have their previous output transaction be in the wallet. Note that all inputs selected must be of standard form and P2SH scripts must be in the wallet using importaddress or addmultisigaddress(to calculate fees). You can see whether this is the case by checking the \"solvable\" field in the listunspent output. Only pay-to-pubkey, multisig, and P2SH versions thereof are currently supported for watch-only")]
        public IActionResult FundRawTransaction(string hex)
        {
            try
            {
                var result = new FundRawTransactionModel();
                var walletTransactionHandler = this.FullNode.NodeService<IWalletTransactionHandler>() as WalletTransactionHandler;
                var fundTransaction = Transaction.Load(hex, this.Network);

                var feeRate = new FeeRate(this.Settings.MinTxFeeRate.FeePerK);

                var fundContext = new TransactionBuildContext(null, new List<Recipient>())
                {
                    MinConfirmations = 0,
                    FeeType = FeeType.Low,
                };
                
                walletTransactionHandler.FundTransaction(fundContext, fundTransaction);
                var fee = feeRate.GetFee(fundTransaction);

                result.Hex = fundTransaction.ToHex();
                result.Fee = fee.ToUnit(MoneyUnit.BTR);
                result.ChangePos = -1;

                return this.Json(ResultHelper.BuildResultResponse(result));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Return the raw transaction data. If verbose is 'true', returns an Object with information about 'txid'. If verbose is 'false' or omitted, returns a string that is serialized, hex - encoded data for 'txid'.
        /// </summary>
        /// <param name="txid">The transaction id</param>
        /// <param name="verbose">If false, return a string, otherwise return a json object</param>
        /// <param name="blockhash">The block in which to look for the transaction</param>
        /// <returns>The serialized, hex-encoded data for 'txid'</returns>
        [ActionName("getrawtransaction")]
        [ActionDescription("Return the raw transaction data. If verbose is 'true', returns an Object with information about 'txid'. If verbose is 'false' or omitted, returns a string that is serialized, hex - encoded data for 'txid'.")]
        public IActionResult GetRawTransaction(string txid, bool verbose, string blockhash)
        {
            try
            {
                if (string.IsNullOrEmpty(txid))
                {
                    throw new ArgumentNullException("txid");
                }

                uint256 trxid;
                if (!uint256.TryParse(txid, out trxid))
                    throw new ArgumentException(nameof(txid));

                var trx = this.pooledTransaction != null ? this.pooledTransaction.GetTransaction(trxid).Result : null;

                if (trx == null)
                {
                    var blockStore = this.FullNode.NodeFeature<IBlockStore>();
                    trx = blockStore != null ? blockStore.GetTrxAsync(trxid).Result : null;
                }

                if (trx == null)
                    return null;

                var block = this.GetTransactionBlockAsync(trxid).Result;

                if (!string.IsNullOrEmpty(blockhash))
                {
                    if (block.HashBlock.ToString() != blockhash)
                    {
                        throw new RPCServerException(NBitcoin.RPC.RPCErrorCode.RPC_INVALID_REQUEST, "Block with specified hash doesnt contain transaction");
                    }
                } 

                var model = new RPC.Models.TransactionVerboseModel(trx, this.Network, block, this.ChainState?.ConsensusTip);

                if (verbose)
                {
                    return this.Json(ResultHelper.BuildResultResponse(model));
                }
                else
                {
                    return this.Json(ResultHelper.BuildResultResponse(model.ToString()));
                }
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Submits raw transaction (serialized, hex-encoded) to local node and network. Also see createrawtransaction and signrawtransaction calls.
        /// </summary>
        /// <param name="hex">The hex string of the raw transaction</param>
        /// <returns>The transaction hash in hex</returns>
        [ActionName("sendrawtransaction")]
        [ActionDescription("Submits raw transaction (serialized, hex-encoded) to local node and network. Also see createrawtransaction and signrawtransaction calls.")]
        public IActionResult SendRawTransaction(string hex)
        {
            try
            {
                if (string.IsNullOrEmpty(hex))
                {
                    throw new ArgumentNullException("hex");
                }

                if (!this.ConnectionManager.ConnectedPeers.Any())
                {
                    throw new WalletException("Can't send transaction: sending transaction requires at least on connection.");
                }

                var transaction = Transaction.Load(hex, this.Network);
                var controller = this.FullNode.NodeService<WalletController>();

                var transactionRequest = new SendTransactionRequest(transaction.ToHex());

                this.broadcasterManager.BroadcastTransactionAsync(transaction).GetAwaiter().GetResult();
                TransactionBroadcastEntry entry = this.broadcasterManager.GetTransaction(transaction.GetHash());

                if (!string.IsNullOrEmpty(entry?.ErrorMessage))
                {
                    this.logger.LogError("Exception occurred: {0}", entry.ErrorMessage);
                    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, entry.ErrorMessage, "Transaction Exception");
                }

                return this.Json(ResultHelper.BuildResultResponse(transaction.GetHash().ToString()));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Sign inputs for raw transaction (serialized, hex-encoded). The second optional argument(may be null) is an array of previous transaction outputs 
        /// that this transaction depends on but may not yet be in the block chain. The third optional argument(may be null) is an array of base58 - encoded 
        /// private keys that, if given, will be the only keys used to sign the transaction.
        /// </summary>
        /// <param name="hex">The transaction hex string</param>
        /// <param name="privkeys">A json array of base58-encoded private keys for signing</param>
        /// <param name="prevtxs">An json array of previous dependent transaction outputs</param> 
        /// <param name="sighashtype">The signature hash type. Default is ALL. Must be one of "ALL", "NONE", "SINGLE", "ALL|ANYONECANPAY", "NONE|ANYONECANPAY", "SINGLE|ANYONECANPAY"</param>
        /// <returns>Result is sign object of transaction</returns>
        [ActionName("signrawtransaction")]
        [ActionDescription("Sign inputs for raw transaction (serialized, hex-encoded). The second optional argument(may be null) is an array of previous transaction outputs that this transaction depends on but may not yet be in the block chain. The third optional argument(may be null) is an array of base58 - encoded private keys that, if given, will be the only keys used to sign the transaction.")]
        public IActionResult SignRawTransaction(string hex, string[] privkeys, string[] prevtxs, string sighashtype)
        {
            try
            {
                var result = new SignRawTransactionModel();

                if (string.IsNullOrEmpty(hex))
                {
                    throw new ArgumentNullException("hex");
                }

                var txBuilderContext = new TransactionBuildContext(null, new List<Recipient>())
                {
                    MinConfirmations = 0,
                    FeeType = FeeType.Low
                };

                var txBuilder = txBuilderContext.TransactionBuilder;

                var transaction = Transaction.Load(hex, this.Network);

                var actualFlag = SigHash.All;
                switch (sighashtype)
                {
                    case "SINGLE":
                        actualFlag = SigHash.Single;
                        break;
                    case "NONE":
                        actualFlag = SigHash.None;
                        break;
                    case "ALL|ANYONECANPAY":
                        actualFlag = SigHash.All | SigHash.AnyoneCanPay;
                        break;
                    case "NONE|ANYONECANPAY":
                        actualFlag = SigHash.None | SigHash.AnyoneCanPay;
                        break;
                    case "SINGLE|ANYONECANPAY":
                        actualFlag = SigHash.Single | SigHash.AnyoneCanPay;
                        break;
                    case "ALL":
                    default:
                        actualFlag = SigHash.All;
                        break;
                }

                List<Key> keys = null;

                if (privkeys != null)
                {
                    foreach (var itemKey in privkeys)
                    {
                        var secret = this.Network.CreateBitcoinSecret(itemKey);
                        keys.Add(secret.PrivateKey);
                    }
                }

                var tx = transaction.Clone(network: this.Network);
                var signedTx = txBuilder.SignTransactionInPlace(tx, actualFlag, keys);

                result.Hex = signedTx.ToHex();
                result.Complete = true;

                return this.Json(ResultHelper.BuildResultResponse(result));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        private async Task<ChainedHeader> GetTransactionBlockAsync(uint256 trxid)
        {
            ChainedHeader block = null;
            var blockStore = this.FullNode.NodeFeature<IBlockStore>();

            uint256 blockid = blockStore != null ? await blockStore.GetTrxBlockIdAsync(trxid) : null;
            if (blockid != null)
                block = this.Chain?.GetBlock(blockid);

            return block;
        }

        /// <summary>
        /// Gets the transaction.
        /// </summary>
        /// <param name="args">The arguments.</param>
        /// <returns>TransactionDetail rpc format</returns>
        [ActionName("gettransaction")]
        [ActionDescription("Returns a wallet (only local transactions) transaction detail.")]
        public IActionResult GetTransaction(string[] args)
        {
            try
            {
                var reqTransactionId = uint256.Parse(args[0]);
                if (reqTransactionId == null)
                {
                    var response = new Node.Utilities.JsonContract.ErrorModel();
                    response.Code = "-5";
                    response.Message = "Invalid or non-wallet transaction id";
                    return this.Json(ResultHelper.BuildResultResponse(response));
                }

                Block block = null;
                ChainedHeader chainedHeader = null;
                var blockHash = this.blockRepository.GetTrxBlockIdAsync(reqTransactionId).GetAwaiter().GetResult(); //this brings block hash for given transaction
                if (blockHash != null)
                {
                    block = this.blockRepository.GetAsync(blockHash).GetAwaiter().GetResult();
                    chainedHeader = this.ConsensusLoop.Chain.GetBlock(blockHash);
                }

                var currentTransaction = this.blockRepository.GetTrxAsync(reqTransactionId).GetAwaiter().GetResult();
                if (currentTransaction == null)
                {
                    var response = new Node.Utilities.JsonContract.ErrorModel();
                    response.Code = "-5";
                    response.Message = "Invalid or non-wallet transaction id";
                    return this.Json(ResultHelper.BuildResultResponse(response));
                }

                var transactionResponse = new Consensus.Models.TransactionModel();
                var transactionHash = currentTransaction.GetHash();
                transactionResponse.NormTxId = string.Format("{0:x8}", transactionHash);
                transactionResponse.TxId = string.Format("{0:x8}", transactionHash);
                if (block != null && chainedHeader != null)
                {
                    transactionResponse.Confirmations = this.ConsensusLoop.Chain.Tip.Height - chainedHeader.Height; // ExtractBlockHeight(block.Transactions.First().Inputs.First().ScriptSig);
                    transactionResponse.BlockTime = block.Header.BlockTime.ToUnixTimeSeconds();
                }

                transactionResponse.BlockHash = string.Format("{0:x8}", blockHash);


                transactionResponse.Time = currentTransaction.Time;
                transactionResponse.TimeReceived = currentTransaction.Time;
                //transactionResponse.BlockIndex = currentTransaction.Inputs.Transaction.;//The index of the transaction in the block that includes it

                transactionResponse.Details = new List<TransactionDetail>();
                foreach (var item in currentTransaction.Outputs)
                {
                    var detail = new TransactionDetail();
                    var address = this.walletManager.GetAddressByPubKeyHash(item.ScriptPubKey);

                    if (address == null)
                    {
                        var response = new Node.Utilities.JsonContract.ErrorModel();
                        response.Code = "-5";
                        response.Message = "Invalid or non-wallet transaction id";
                        return this.Json(ResultHelper.BuildResultResponse(response));
                    }

                    detail.Account = address.Address;
                    detail.Address = address.Address;

                    if (transactionResponse.Confirmations < 10)
                    {
                        detail.Category = "receive";
                    }
                    else
                    {
                        detail.Category = "generate";
                    }


                    detail.Amount = (double)item.Value.Satoshi / 100000000;
                    transactionResponse.Details.Add(detail);
                }


                transactionResponse.Hex = currentTransaction.ToHex();


                var json = ResultHelper.BuildResultResponse(transactionResponse);
                return this.Json(json);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Mark in-wallet transaction txid as abandoned This will mark this transaction and all its in-wallet descendants as abandoned which will allow for their
        /// inputs to be respent.It can be used to replace \"stuck\" or evicted transactions. It only works on transactions which are not included in a block and are
        /// not currently in the mempool. It has no effect on transactions which are already conflicted or abandoned.
        /// </summary>
        /// <param name="txid">The transaction id</param>
        /// <returns>True/False</returns>
        [ActionName("abandontransaction")]
        [ActionDescription("Mark in-wallet transaction txid as abandoned This will mark this transaction and all its in-wallet descendants as abandoned which will allow for their inputs to be respent.It can be used to replace \"stuck\" or evicted transactions. It only works on transactions which are not included in a block and are not currently in the mempool. It has no effect on transactions which are already conflicted or abandoned.")]
        public IActionResult AbandonTransaction(string txid)
        {
            try
            {
                var result = true;
                if (string.IsNullOrEmpty(txid))
                {
                    throw new ArgumentNullException("txid");
                }

                var hashTxId = new uint256(txid);
                var tx = this.broadcasterManager.GetTransaction(hashTxId);

                if ((tx != null) && (tx.State == State.ToBroadcast || tx.State == State.CantBroadcast))
                {
                    result = this.broadcasterManager.RemoveTransaction(tx);
                }
                else
                {
                    result = false;
                }

                return this.Json(ResultHelper.BuildResultResponse(result));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Set the transaction fee per kB for this wallet. Overrides the global -paytxfee command line parameter.
        /// </summary>
        /// <param name="amount">The transaction fee in BTR/kB</param>
        /// <returns>True or False</returns>
        [ActionName("settxfee")]
        [ActionDescription("Set the transaction fee per kB for this wallet. Overrides the global -paytxfee command line parameter.")]
        public IActionResult SetTxFee(string amount)
        {
            try
            {
                var result = true;
                if (string.IsNullOrEmpty(amount))
                {
                    throw new ArgumentNullException("amount");
                }

                decimal fee;
                result = decimal.TryParse(amount, out fee);

                if (result) this.WalletFeePolicy.SetPayTxFee(new Money(fee, MoneyUnit.BTR));

                return this.Json(ResultHelper.BuildResultResponse(result));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }
    }
}
