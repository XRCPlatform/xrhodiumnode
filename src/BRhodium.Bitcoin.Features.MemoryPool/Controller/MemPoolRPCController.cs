using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
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

namespace BRhodium.Bitcoin.Features.MemoryPool.Controller
{
    /// <summary>
    /// MemPool RPCs Method
    /// </summary>
    [Controller]
    public class MemPoolRPCController : FeatureController
    {
        /// <summary>
        /// Instance logger
        /// </summary>
        private readonly ILogger logger;

        /// <summary>An interface implementation used to retrieve unspent transactions from a pooled source.</summary>
        private readonly IPooledGetUnspentTransaction pooledGetUnspentTransaction;

        /// <summary>An interface implementation used to retrieve unspent transactions.</summary>
        private readonly IGetUnspentTransaction getUnspentTransaction;

        public MempoolManager MempoolManager { get; private set; }

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

        /// <summary>
        /// Returns all transaction ids in memory pool as a json array of string transaction ids. Hint: use getmempoolentry to fetch a specific transaction from the mempool.
        /// </summary>
        /// <param name="verbose">True for a json object, false for array of transaction ids</param>
        /// <returns></returns>
        [ActionName("getrawmempool")]
        [ActionDescription("Returns all transaction ids in memory pool as a json array of string transaction ids. Hint: use getmempoolentry to fetch a specific transaction from the mempool.")]
        public IActionResult GetRawMempool(string verbose)
        {
            try
            {
                var memPoolTransactions = this.MempoolManager.GetMempoolAsync().Result;

                switch (verbose)
                {
                    case "true":

                        var result = new List<GetMemPoolEntry>();

                        foreach (var itemTxId in memPoolTransactions)
                        {
                            var entry = this.MemPool.GetEntry(itemTxId);
                            var resultEntry = new GetMemPoolEntry();
                            resultEntry.Fee = entry.Fee.ToUnit(MoneyUnit.BTR);
                            resultEntry.ModifiedFee = entry.ModifiedFee;
                            resultEntry.Size = entry.GetTxSize();
                            resultEntry.Time = entry.Time;
                            resultEntry.Height = entry.EntryHeight;
                            resultEntry.WtxId = entry.TransactionHash.ToString();
                            resultEntry.DescendantCount = entry.CountWithDescendants;
                            resultEntry.DescendantFees = entry.ModFeesWithDescendants.ToUnit(MoneyUnit.BTR);
                            resultEntry.DescendantSize = entry.SizeWithDescendants;
                            resultEntry.AncestorCount = entry.CountWithAncestors;
                            resultEntry.AncestorFees = entry.ModFeesWithAncestors.ToUnit(MoneyUnit.BTR);
                            resultEntry.AncestorSize = entry.SizeWithAncestors;

                            var parents = this.MemPool.GetMemPoolParents(entry);

                            if (parents != null)
                            {
                                foreach (var item in parents)
                                {
                                    resultEntry.Depends.Add(item.TransactionHash.ToString());
                                }
                            }
                            
                            result.Add(resultEntry);
                        }

                        return this.Json(ResultHelper.BuildResultResponse(result));

                    default:
                        return this.Json(ResultHelper.BuildResultResponse(memPoolTransactions));
                }
                
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Returns mempool data for given transaction
        /// </summary>
        /// <param name="txid">The transaction id (must be in mempool)</param>
        /// <returns>Return GetMemPoolEntry model</returns>
        [ActionName("getmempoolentry")]
        [ActionDescription("Returns mempool data for given transaction")]
        public IActionResult GetMempoolEntry(string txid)
        {
            try
            {
                if (string.IsNullOrEmpty(txid))
                {
                    throw new ArgumentNullException("txid");
                }

                var entry = this.MemPool.GetEntry(new uint256(txid));
                var resultEntry = new GetMemPoolEntry();
                resultEntry.Fee = entry.Fee.ToUnit(MoneyUnit.BTR);
                resultEntry.ModifiedFee = entry.ModifiedFee;
                resultEntry.Size = entry.GetTxSize();
                resultEntry.Time = entry.Time;
                resultEntry.Height = entry.EntryHeight;
                resultEntry.WtxId = entry.TransactionHash.ToString();
                resultEntry.DescendantCount = entry.CountWithDescendants;
                resultEntry.DescendantFees = entry.ModFeesWithDescendants.ToUnit(MoneyUnit.BTR);
                resultEntry.DescendantSize = entry.SizeWithDescendants;
                resultEntry.AncestorCount = entry.CountWithAncestors;
                resultEntry.AncestorFees = entry.ModFeesWithAncestors.ToUnit(MoneyUnit.BTR);
                resultEntry.AncestorSize = entry.SizeWithAncestors;

                var parents = this.MemPool.GetMemPoolParents(entry);

                if (parents != null)
                {
                    foreach (var item in parents)
                    {
                        resultEntry.Depends.Add(item.TransactionHash.ToString());
                    }
                }
                
                return this.Json(ResultHelper.BuildResultResponse(resultEntry));
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
        /// <param name="txid">The transaction id</param>
        /// <param name="n">vout number</param>
        /// <param name="includeMemPool">Whether to include the mempool. Default: true. Note that an unspent output that is spent in the mempool won't appear.</param>
        /// <returns>Result GetTxOutModel</returns>
        [ActionName("gettxout")]
        [ActionDescription("Returns details about an unspent transaction output.")]
        public IActionResult GetTxOut(string txid, uint n, bool? includeMemPool)
        {
            try
            {
                uint256 trxid;
                if (!uint256.TryParse(txid, out trxid))
                    throw new ArgumentException(nameof(txid));

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
                    return null;

                var result = new GetTxOutModel(unspentOutputs, n, this.Network, this.Chain.Tip);

                return this.Json(ResultHelper.BuildResultResponse(result));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("gettxoutproof")]
        [ActionDescription("Returns a hex - encoded proof that \"txid\" was included in a block.")]
        public IActionResult GetTxOutProf(string txids, string blockhash)
        {
            try
            {
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("gettxoutsetinfo")]
        [ActionDescription("Returns statistics about the unspent transaction output set. Note this call may take some time.")]
        public IActionResult GetTxOutSetInfo()
        {
            try
            {
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("verifytxoutproof")]
        [ActionDescription("Verifies that a proof points to a transaction in a block, returning the transaction it commits to and throwing an RPC error if the block is not in our best chain")]
        public IActionResult VerifyTxOutProof(string proof)
        {
            try
            {
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("getmempoolancestors")]
        [ActionDescription("If txid is in the mempool, returns all in-mempool ancestors.")]
        public IActionResult GetMempoolAncestors(string txid, string verbose)
        {
            try
            {

                //var it = this.MapTx.TryGet(hash);

                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("getmempooldescendants")]
        [ActionDescription("If txid is in the mempool, returns all in-mempool descendants.")]
        public IActionResult GetMempoolDescendants(string txid, string verbose)
        {
            try
            {
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("getmempoolinfo")]
        [ActionDescription("Returns details on the active state of the TX memory pool.")]
        public IActionResult GetMempoolInfo()
        {
            try
            {
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("savemempool")]
        [ActionDescription("Dumps the mempool to disk.")]
        public IActionResult SaveMemPool(int height)
        {
            try
            {
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
