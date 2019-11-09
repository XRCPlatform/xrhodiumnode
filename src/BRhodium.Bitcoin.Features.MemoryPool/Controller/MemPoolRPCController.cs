using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using BRhodium.Bitcoin.Features.BlockStore;
using BRhodium.Bitcoin.Features.MemoryPool.Interfaces;
using BRhodium.Bitcoin.Features.MemoryPool.Models;
using BRhodium.Node;
using BRhodium.Node.Base;
using BRhodium.Node.Configuration;
using BRhodium.Node.Controllers;
using BRhodium.Node.Interfaces;
using BRhodium.Node.Utilities;
using BRhodium.Node.Utilities.JsonContract;
using BRhodium.Node.Utilities.JsonErrors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.RPC;
using static BRhodium.Bitcoin.Features.MemoryPool.TxMempool;

namespace BRhodium.Bitcoin.Features.MemoryPool.Controller
{
    /// <summary>
    /// MemPool RPCs Method
    /// </summary>
    [Controller]
    public class MemPoolRPCController : FeatureController
    {
        /// <summary>Instance logger</summary>
        private readonly ILogger logger;

        /// <summary>An interface implementation used to retrieve unspent transactions from a pooled source.</summary>
        private readonly IPooledGetUnspentTransaction pooledGetUnspentTransaction;

        /// <summary>An interface implementation used to retrieve unspent transactions.</summary>
        private readonly IGetUnspentTransaction getUnspentTransaction;

        /// <summary>
        /// The mempool manager.
        /// </summary>
        public MempoolManager MempoolManager { get; private set; }

        /// <summary>
        ///  Actual mempool.
        /// </summary>
        public ITxMempool MemPool { get; private set; }

        public MemPoolRPCController(
            ILoggerFactory loggerFactory,
            MempoolManager mempoolManager,
            ITxMempool mempool,
            IPooledGetUnspentTransaction pooledGetUnspentTransaction = null,
            IGetUnspentTransaction getUnspentTransaction = null,
            IFullNode fullNode = null,
            NodeSettings nodeSettings = null,
            Network network = null,
            ConcurrentChain chain = null,
            IChainState chainState = null,
            Node.Connection.IConnectionManager connectionManager = null)
            : base(
                  fullNode: fullNode,
                  nodeSettings: nodeSettings,
                  network: network,
                  chain: chain,
                  chainState: chainState,
                  connectionManager: connectionManager)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.pooledGetUnspentTransaction = pooledGetUnspentTransaction;
            this.getUnspentTransaction = getUnspentTransaction;
            this.MempoolManager = mempoolManager;
            this.MemPool = mempool;
        }

        private GetMemPoolEntry GetMemPoolEntryFromTx(TxMempoolEntry entry)
        {
            var resultEntry = new GetMemPoolEntry
            {
                Fee = entry.Fee.ToUnit(MoneyUnit.XRC),
                ModifiedFee = entry.ModifiedFee,
                Size = entry.GetTxSize(),
                Time = entry.Time,
                Height = entry.EntryHeight,
                WtxId = entry.TransactionHash.ToString(),
                DescendantCount = entry.CountWithDescendants,
                DescendantFees = entry.ModFeesWithDescendants.ToUnit(MoneyUnit.XRC),
                DescendantSize = entry.SizeWithDescendants,
                AncestorCount = entry.CountWithAncestors,
                AncestorFees = entry.ModFeesWithAncestors.ToUnit(MoneyUnit.XRC),
                AncestorSize = entry.SizeWithAncestors
            };

            var parents = this.MemPool.GetMemPoolParents(entry);

            if (parents != null)
            {
                resultEntry.Depends = new List<string>();
                foreach (var item in parents)
                {
                    resultEntry.Depends.Add(item.TransactionHash.ToString());
                }
            }

            return resultEntry;
        }

        /// <summary>
        /// Returns all transaction ids in memory pool as a json array of string transaction ids. Hint: use getmempoolentry to fetch a specific transaction from the mempool.
        /// </summary>
        /// <param name="verbose">True for a json object, false for array of transaction ids.</param>
        /// <returns>(List, GetMemPoolEntry or List, string) Object with informations.</returns>
        [ActionName("getrawmempool")]
        [ActionDescription("Returns all transaction ids in memory pool as a json array of string transaction ids. Hint: use getmempoolentry to fetch a specific transaction from the mempool.")]
        public IActionResult GetRawMempool(bool verbose)
        {
            try
            {
                var memPoolTransactions = this.MempoolManager.GetMempoolAsync().Result;

                if (verbose)
                {
                    var result = new Dictionary<string, GetMemPoolEntry>();

                    foreach (var itemTxId in memPoolTransactions)
                    {
                        var entry = this.MemPool.GetEntry(itemTxId);
                        result.Add(itemTxId.ToString(), GetMemPoolEntryFromTx(entry));
                    }

                    return this.Json(ResultHelper.BuildResultResponse(result));
                }

                return this.Json(ResultHelper.BuildResultResponse(memPoolTransactions));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Returns mempool data for given transaction.
        /// </summary>
        /// <param name="txid">The transaction id (must be in mempool).</param>
        /// <returns>(GetMemPoolEntry) Return object with informations.</returns>
        [ActionName("getmempoolentry")]
        [ActionDescription("Returns mempool data for given transaction.")]
        public IActionResult GetMempoolEntry(string txid)
        {
            try
            {
                Guard.NotEmpty(txid, "txid");
                var entry = this.MemPool.GetEntry(new uint256(txid));
                return this.Json(ResultHelper.BuildResultResponse(GetMemPoolEntryFromTx(entry)));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Returns details about an unspent transaction output.
        /// </summary>
        /// <param name="txid">The transaction id.</param>
        /// <param name="n">Vout number.</param>
        /// <param name="includeMemPool">Whether to include the mempool. Default: true. Note that an unspent output that is spent in the mempool won't appear.</param>
        /// <returns>(GetTxOutModel) Result object with informations.</returns>
        [ActionName("gettxout")]
        [ActionDescription("Returns details about an unspent transaction output.")]
        public IActionResult GetTxOut(string txid, uint n, bool? includeMemPool)
        {
            try
            {
                uint256 trxid;
                if (!uint256.TryParse(txid, out trxid))
                {
                    throw new ArgumentException(nameof(txid));
                }

                UnspentOutputs unspentOutputs = null;
                if (includeMemPool.HasValue && includeMemPool.Value)
                {
                    unspentOutputs = this.pooledGetUnspentTransaction != null ? this.pooledGetUnspentTransaction.GetUnspentTransactionAsync(trxid).Result : null;
                }
                else
                {
                    unspentOutputs = this.getUnspentTransaction != null ? this.getUnspentTransaction.GetUnspentTransactionAsync(trxid).Result : null;
                }

                if (unspentOutputs == null)
                    return this.Json(ResultHelper.BuildResultResponse(null));

                var result = new GetTxOutModel(unspentOutputs, n, this.Network, this.Chain.Tip);

                return this.Json(ResultHelper.BuildResultResponse(result));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Returns statistics about the unspent transaction output set. Note this call may take some time.
        /// </summary>
        /// <returns>(GetTxOutSetInfo) Object with informations.</returns>
        [ActionName("gettxoutsetinfo")]
        [ActionDescription("Returns statistics about the unspent transaction output set. Note this call may take some time.")]
        public IActionResult GetTxOutSetInfo()
        {
            try
            {
                var result = new GetTxOutSetInfo();

                var chainRepository = this.FullNode.NodeService<ConcurrentChain>();
                var blockStoreManager = this.FullNode.NodeService<BlockStoreManager>();

                result.BestBlock = chainRepository.Tip.HashBlock.ToString();
                result.Height = chainRepository.Height;

                var txsMemPool = this.MempoolManager.InfoAllAsync().Result;
                result.Transactions = txsMemPool.Count;

                foreach (var itemMemPool in txsMemPool)
                {
                    var scriptPubSize = itemMemPool.Trx.Outputs != null ? itemMemPool.Trx.Outputs.First().ScriptPubKey.Length : 0;

                    result.TxOuts += itemMemPool.Trx.Outputs.Count;
                    result.TotalAmount += itemMemPool.Trx.TotalOut.ToUnit(MoneyUnit.XRC);
                    result.BogoSize += 32 /* txid */ + 4 /* vout index */ + 4 /* height + coinbase */ + 8 /* amount */ + 2 /* scriptPubKey len */ + scriptPubSize /* scriptPubKey */;
                }

                var shiftHash = chainRepository.Tip.HashBlock << (0);
                result.Hash_serialized_2 = shiftHash.AsBitcoinSerializable().ToHex(this.Network, SerializationType.Hash);

                for (int height = 0; height <= this.Chain.Height; height++)
                {
                    var chainedHeader = chainRepository.GetBlock(height);
                    var block = height == 0 ? this.Network.GetGenesis() : blockStoreManager.BlockRepository.GetAsync(chainedHeader.HashBlock).Result;
                    result.DiskSize += block.GetSerializedSize();
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
        /// Returns a hex-encoded proof that "txid" was included in a block. NOTE: By default this function only works sometimes.This is when there is an
        /// unspent output in the utxo for this transaction.To make it always work, you need to maintain a transaction index, using the -txindex command line option or
        /// specify the block in which the transaction is included manually(by blockhash).
        /// </summary>
        /// <param name="txids">A json array of txids to filter.</param>
        /// <param name="blockhash">If specified, looks for txid in the block with this hash.</param>
        /// <returns>(string) A string that is a serialized, hex-encoded data for the proof.</returns>
        [ActionName("gettxoutproof")]
        [ActionDescription("Returns a hex - encoded proof that \"txid\" was included in a block.")]
        public IActionResult GetTxOutProf(string txids, string blockhash)
        {
            try
            {
                var chainRepository = this.FullNode.NodeService<ConcurrentChain>();
                var blockStoreManager = this.FullNode.NodeService<BlockStoreManager>();

                Block block = null;
                var txIds = txids.Split(',');

                if (!string.IsNullOrEmpty(blockhash))
                {
                    var chainedHeader = chainRepository.GetBlock(new uint256(blockhash));
                    block = blockStoreManager.BlockRepository.GetAsync(chainedHeader.HashBlock).Result;
                }
                else
                {
                    for (int height = this.Chain.Height; height >= 0; height--)
                    {
                        var chainedHeader = chainRepository.GetBlock(height);
                        block = height == 0 ? this.Network.GetGenesis() : blockStoreManager.BlockRepository.GetAsync(chainedHeader.HashBlock).Result;

                        if ((block != null) && (block.Transactions != null) && (block.Transactions.Count() > 0))
                        {
                            foreach (var itemTransaction in block.Transactions)
                            {
                                if (txIds.Contains(itemTransaction.GetHash().ToString()))
                                {
                                    break;
                                }
                            }
                        }
                    }
                }

                var txHashes = txIds.Select(a => new uint256(a)).ToArray();

                var mBlock = new MerkleBlock(block, txHashes);
                var mBlockData = mBlock.ToHex(this.Network);

                return this.Json(ResultHelper.BuildResultResponse(mBlockData));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Verifies that a proof points to a transaction in a block, returning the transaction it commits to and throwing an RPC error if the block is not in our best chain.
        /// </summary>
        /// <param name="proof">The hex-encoded proof generated by gettxoutproof.</param>
        /// <returns>(List, string) The txid(s) which the proof commits to, or empty array if the proof is invalid.</returns>
        [ActionName("verifytxoutproof")]
        [ActionDescription("Verifies that a proof points to a transaction in a block, returning the transaction it commits to and throwing an RPC error if the block is not in our best chain.")]
        public IActionResult VerifyTxOutProof(string proof)
        {
            try
            {
                Guard.NotEmpty(proof, nameof(proof));

                var bytesProof = NBitcoin.DataEncoders.Encoders.Hex.DecodeData(proof);

                var mBlock = new MerkleBlock();
                mBlock.FromBytes(bytesProof, NBitcoin.Protocol.ProtocolVersion.XRC_PROTOCOL_VERSION, this.Network);

                var hashes = mBlock.PartialMerkleTree.Hashes;

                if ((hashes != null) && (hashes.Count > 0))
                {
                    return this.Json(ResultHelper.BuildResultResponse(hashes.ToArray()));
                }
                else
                {
                    throw new RPCException(RPCErrorCode.RPC_MISC_ERROR, "Empty Merkle Block", null, false);
                }
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// If txid is in the mempool, returns all in-mempool ancestors.
        /// </summary>
        /// <param name="txid">The transaction id (must be in mempool).</param>
        /// <param name="verbose">True for a json object, false for array of transaction ids</param>
        /// <returns>(List, GetMemPoolEntry or List, string) Return object with informations.</returns>
        [ActionName("getmempoolancestors")]
        [ActionDescription("If txid is in the mempool, returns all in-mempool ancestors.")]
        public IActionResult GetMempoolAncestors(string txid, bool verbose)
        {
            try
            {
                Guard.NotEmpty(txid, nameof(txid));

                var entryTx = this.MemPool.GetEntry(new uint256(txid));
                Guard.NotNull(entryTx, "entryTx does not exist.");

                var setAncestors = new SetEntries();
                string dummy = string.Empty;
                long nNoLimit = long.MaxValue;
                this.MemPool.CalculateMemPoolAncestors(entryTx, setAncestors, nNoLimit, nNoLimit, nNoLimit, nNoLimit, out dummy, false);

                if (verbose)
                {
                    var result = new List<GetMemPoolEntry>();

                    if (setAncestors != null)
                    {
                        foreach (var entry in setAncestors)
                        {
                            result.Add(GetMemPoolEntryFromTx(entry));
                        }
                    }
                    return this.Json(ResultHelper.BuildResultResponse(result));
                }

                var listTxHash = new List<string>();
                if (setAncestors != null)
                {
                    foreach (var entry in setAncestors)
                    {
                        listTxHash.Add(entry.TransactionHash.ToString());
                    }
                }

                return this.Json(ResultHelper.BuildResultResponse(listTxHash));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// If txid is in the mempool, returns all in-mempool descendants.
        /// </summary>
        /// <param name="txid">The transaction id (must be in mempool).</param>
        /// <param name="verbose">True for a json object, false for array of transaction ids.</param>
        /// <returns>(List, GetMemPoolEntry or List, string) Return object with informations.</returns>
        [ActionName("getmempooldescendants")]
        [ActionDescription("If txid is in the mempool, returns all in-mempool descendants.")]
        public IActionResult GetMempoolDescendants(string txid, bool verbose)
        {
            try
            {
                Guard.NotEmpty(txid, nameof(txid));
                var entryTx = this.MemPool.GetEntry(new uint256(txid));
                var setDescendants = new SetEntries();
                this.MemPool.CalculateDescendants(entryTx, setDescendants);

                if (verbose)
                {
                    var result = new List<GetMemPoolEntry>();

                    if (setDescendants != null)
                    {
                        foreach (var entry in setDescendants)
                        {
                            result.Add(GetMemPoolEntryFromTx(entry));
                        }
                    }

                    return this.Json(ResultHelper.BuildResultResponse(result));
                }

                var listTxHash = new List<string>();

                if (setDescendants != null)
                {
                    foreach (var entry in setDescendants)
                    {
                        listTxHash.Add(entry.TransactionHash.ToString());
                    }
                }

                return this.Json(ResultHelper.BuildResultResponse(listTxHash));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Returns details on the active state of the TX memory pool.
        /// </summary>
        /// <returns>(GetMemPoolInfo) Return object with informations.</returns>
        [ActionName("getmempoolinfo")]
        [ActionDescription("Returns details on the active state of the TX memory pool.")]
        public IActionResult GetMempoolInfo()
        {
            try
            {
                var maxmem = this.MempoolManager.mempoolSettings.MaxMempool * 1000000;
                var result = new GetMemPoolInfo
                {
                    Size = this.MemPool.Size,
                    Usage = this.MemPool.DynamicMemoryUsage(),
                    Bytes = this.MempoolManager.MempoolSize().Result,
                    Maxmempool = maxmem,
                    MempoolMinFee = this.MemPool.GetMinFee(maxmem).FeePerK.ToUnit(MoneyUnit.XRC),
                    MinRelayTxFee = this.Settings?.MinRelayTxFeeRate?.FeePerK?.ToUnit(MoneyUnit.XRC)
                };

                return this.Json(ResultHelper.BuildResultResponse(result));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Dumps the mempool to disk.
        /// </summary>
        /// <returns>(bool) True if all ok.</returns>
        [ActionName("savemempool")]
        [ActionDescription("Dumps the mempool to disk.")]
        public IActionResult SaveMemPool()
        {
            try
            {
                this.MempoolManager.SavePool();
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Remove tx from mempool
        /// </summary>
        /// <param name="txid">Id hash of transaction.</param>
        /// <returns>(bool) True if all ok.</returns>
        [ActionName("removetxfrommempool")]
        [ActionDescription("Remove transaction from node mempool.")]
        public IActionResult RemoveTxFromMemPool(string txid)
        {
            try
            {
                Guard.NotEmpty(txid, nameof(txid));
                var task = this.MempoolManager.RemoveTransactionFromMempool(new uint256(txid));
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }
    }
}
