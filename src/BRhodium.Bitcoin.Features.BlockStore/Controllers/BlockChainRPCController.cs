using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using BRhodium.Bitcoin.Features.BlockStore.Models;
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

namespace BRhodium.Bitcoin.Features.BlockStore.Controllers
{
    /// <summary>
    /// BlockChain RPCs method
    /// </summary>
    /// <seealso cref="BRhodium.Node.Controllers.FeatureController" />
    [Controller]
    public class BlockChainRPCController : FeatureController
    {
        /// <summary>
        /// Instance logger
        /// </summary>
        private readonly ILogger logger;

        /// <summary>An interface implementation used to retrieve unspent transactions from a pooled source.</summary>
        private readonly IPooledGetUnspentTransaction pooledGetUnspentTransaction;

        /// <summary>An interface implementation used to retrieve unspent transactions.</summary>
        private readonly IGetUnspentTransaction getUnspentTransaction;

        public BlockChainRPCController(
            ILoggerFactory loggerFactory,
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
        }

        /// <summary>
        /// Gets the block.
        /// If verbosity is 0, returns a string that is serialized, hex-encoded data for block 'hash'.
        /// If verbosity is 1, returns an Object with information about block<hash>.
        /// If verbosity is 2, returns an Object with information about block<hash> and information about each transaction. 
        /// </summary>
        /// <paramref name="hash">Hash of block.</param>
        /// <param name="verbosity">The verbosity.</param>
        /// <returns>Return data based on verbosity</returns>
        [ActionName("getblock")]
        [ActionDescription("Gets the block.")]
        public IActionResult GetBlock(string hash, int verbosity)
        {
            try
            {
                if (string.IsNullOrEmpty(hash))
                {
                    throw new ArgumentNullException("hash");
                }

                var chainRepository = this.FullNode.NodeService<ConcurrentChain>();
                var blockStoreManager = this.FullNode.NodeService<BlockStoreManager>();

                var chainedHeader = chainRepository.GetBlock(new uint256(hash));
                var block = blockStoreManager.BlockRepository.GetAsync(chainedHeader.HashBlock).Result;

                switch (verbosity)
                {
                    case 1:
                        var blockTemplate = new GetBlockWithTransactionModel<GetTransactionBlockModel>();
                        blockTemplate = FillBlockBaseData<GetTransactionBlockModel>(blockTemplate, block, chainedHeader);

                        foreach (var item in block.Transactions)
                        {
                            blockTemplate.Transactions.Add(new GetTransactionBlockModel(item.GetHash().ToString()));
                        }

                        return this.Json(ResultHelper.BuildResultResponse(blockTemplate));

                    case 2:
                        var detailBlockTemplate = new GetBlockWithTransactionModel<GetTransactionDateBlockModel>();
                        detailBlockTemplate = FillBlockBaseData<GetTransactionDateBlockModel>(detailBlockTemplate, block, chainedHeader);

                        foreach (var item in block.Transactions)
                        {
                            detailBlockTemplate.Transactions.Add(new GetTransactionDateBlockModel(item.ToHex()));
                        }

                        return this.Json(ResultHelper.BuildResultResponse(detailBlockTemplate));

                    case 0:
                    default:
                        var hex = block.ToHex(this.Network);
                        return this.Json(ResultHelper.BuildResultResponse(hex));
                }
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Helper for GetBlock
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="blockTemplate"></param>
        /// <param name="block"></param>
        /// <param name="chainedHeader"></param>
        /// <returns>Filled block</returns>
        private GetBlockWithTransactionModel<T> FillBlockBaseData<T>(GetBlockWithTransactionModel<T> blockTemplate, Block block, ChainedHeader chainedHeader)
        {
            blockTemplate.Hash = chainedHeader.HashBlock.ToString();
            blockTemplate.Size = blockTemplate.Weight = blockTemplate.Strippedsize = block.GetSerializedSize();
            blockTemplate.Bits = string.Format("{0:x8}", block.Header.Bits.ToCompact());
            blockTemplate.PreviousBlockHash = block.Header.HashPrevBlock.ToString();
            blockTemplate.Difficulty = block.Header.Bits.Difficulty;
            blockTemplate.Nonce = block.Header.Nonce;
            blockTemplate.Merkleroot = block.Header.HashMerkleRoot.ToString();
            blockTemplate.Version = block.Header.Version;
            blockTemplate.VersionHex = string.Format("{0:x8}", block.Header.Version);
            blockTemplate.Mediantime = block.Header.BlockTime.ToUnixTimeSeconds();
            blockTemplate.Height = chainedHeader.Height;
            blockTemplate.Chainwork = chainedHeader.ChainWork.ToString();
            blockTemplate.TransactionsCount = block.Transactions != null ? block.Transactions.Count() : 0;
            if ((chainedHeader.Next != null) && (chainedHeader.Next.Count > 0))
            {
                blockTemplate.NextBlockHash = chainedHeader.Next.First().ToString();
            }

            return blockTemplate;
        }

        /// <summary>
        /// Gets the best blockhash.
        /// </summary>
        /// <param name="hash">The hash.</param>
        /// <param name="verbosity">The verbosity.</param>
        /// <returns></returns>
        [ActionName("getbestblockhash")]
        [ActionDescription("Gets the block.")]
        public IActionResult GetBestBlockhash(int verbosity)
        {
            try
            {
                var chainRepository = this.FullNode.NodeService<ConcurrentChain>();

                var blockResult = GetBlock(chainRepository.Tip.HashBlock.ToString(), verbosity);
                return this.Json(ResultHelper.BuildResultResponse(blockResult));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Returns the number of blocks in the longest blockchain.
        /// </summary>
        /// <returns>Number</returns>
        [ActionName("getblockcount")]
        [ActionDescription("Returns the number of blocks in the longest blockchain.")]
        public IActionResult GetBlockCount()
        {
            try
            {
                var chainRepository = this.FullNode.NodeService<ConcurrentChain>();
                return this.Json(ResultHelper.BuildResultResponse(chainRepository.Tip.Height));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Returns hash of block in best-block-chain at height provided.
        /// </summary>
        /// <param name="height">The height index</param>
        /// <returns>Hash</returns>
        [ActionName("getblockhash")]
        [ActionDescription("Returns hash of block in best-block-chain at height provided.")]
        public IActionResult GetBlockHash(int height)
        {
            try
            {
                var chainRepository = this.FullNode.NodeService<ConcurrentChain>();
                var chainedHeader = chainRepository.GetBlock(height);
                return this.Json(ResultHelper.BuildResultResponse(chainedHeader.HashBlock.ToString()));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Return information about all known tips in the block tree, including the main chain as well as orphaned branches.
        /// </summary>
        /// <returns>List of GetChainTipModel</returns>
        [ActionName("getchaintips")]
        [ActionDescription("Return information about all known tips in the block tree, including the main chain as well as orphaned branches.")]
        public IActionResult GetChainTips()
        {
            try
            {
                var result = new List<GetChainTipModel>();
                var chainRepository = this.FullNode.NodeService<ConcurrentChain>();

                for (int i = 0; i <= this.Chain.Height; i--)
                {
                    var chainedHeader = chainRepository.GetBlock(i);

                    var chainTip = new GetChainTipModel();
                    chainTip.Height = i;
                    chainTip.Hash = chainedHeader.HashBlock.ToString();
                    var hasheshtoSearch = new List<uint256>();
                    hasheshtoSearch.Add(chainedHeader.HashBlock);

                    chainTip.BranchLen = chainedHeader.Height - chainRepository.FindFork(hasheshtoSearch).Height;

                    if (chainRepository.Contains(chainedHeader.HashBlock))
                    {
                        // This block is part of the currently active chain.
                        chainTip.Status = "active";
                    }
                    else if (!chainedHeader.Validate(this.Network))
                    {
                        // This block or one of its ancestors is invalid.
                        chainTip.Status = "invalid";
                    }
                    else if (chainedHeader.BlockDataAvailability == BlockDataAvailabilityState.HeaderOnly)
                {
                        // This block cannot be connected because full block data for it or one of its parents is missing.
                        chainTip.Status = "headers-only";
                    }
                    else if (chainedHeader.BlockValidationState == ValidationState.FullyValidated)
                    {
                        // This block is fully validated, but no longer part of the active chain. It was probably the active block once, but was reorganized.
                        chainTip.Status = "valid-fork";
                    }
                    else if (chainedHeader.BlockValidationState == ValidationState.HeaderValidated)
                    {
                        // The headers for this block are valid, but it has not been validated. It was probably never part of the most-work chain.
                        chainTip.Status = "valid-headers";
                    }
                    else
                    {
                        // No clue.
                        chainTip.Status = "unknown";
                    }
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
        /// If verbose is false, returns a string that is serialized, hex-encoded data for blockheader 'hash'. If verbose is true, returns an Object with information about blockheader<hash>.
        /// </summary>
        /// <param name="hash">The block hash</param>
        /// <param name="verbose">True for a json object, false for the hex encoded data</param>
        /// <returns></returns>
        [ActionName("getblockheader")]
        [ActionDescription("Return information about all known tips in the block tree, including the main chain as well as orphaned branches.")]
        public IActionResult GetBlockHeader(string hash, string verbose)
        {
            try
            {
                if (string.IsNullOrEmpty(hash))
                {
                    throw new ArgumentNullException("hash");
                }

                var chainRepository = this.FullNode.NodeService<ConcurrentChain>();
                var blockStoreManager = this.FullNode.NodeService<BlockStoreManager>();

                var chainedHeader = chainRepository.GetBlock(new uint256(hash));
                var block = blockStoreManager.BlockRepository.GetAsync(chainedHeader.HashBlock).Result;

                switch (verbose)
                {
                    case "true":
                        var blockTemplate = new GetBlockModel();

                        blockTemplate.Hash = chainedHeader.HashBlock.ToString();
                        blockTemplate.Size = blockTemplate.Weight = blockTemplate.Strippedsize = block.GetSerializedSize();
                        blockTemplate.Bits = string.Format("{0:x8}", block.Header.Bits.ToCompact());
                        blockTemplate.PreviousBlockHash = block.Header.HashPrevBlock.ToString();
                        blockTemplate.Difficulty = block.Header.Bits.Difficulty;
                        blockTemplate.Nonce = block.Header.Nonce;
                        blockTemplate.Merkleroot = block.Header.HashMerkleRoot.ToString();
                        blockTemplate.Version = block.Header.Version;
                        blockTemplate.VersionHex = string.Format("{0:x8}", block.Header.Version);
                        blockTemplate.Mediantime = block.Header.BlockTime.ToUnixTimeSeconds();
                        blockTemplate.Height = chainedHeader.Height;
                        blockTemplate.Chainwork = chainedHeader.ChainWork.ToString();
                        blockTemplate.TransactionsCount = block.Transactions != null ? block.Transactions.Count() : 0;

                        return this.Json(ResultHelper.BuildResultResponse(blockTemplate));

                    default:
                        var hex = block.ToHex(this.Network);
                        return this.Json(ResultHelper.BuildResultResponse(hex));
                }
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Compute statistics about the total number and rate of transactions in the chain.
        /// </summary>
        /// <param name="nblocks">Size of the window in number of blocks (default: one month).</param>
        /// <param name="blockhash">The hash of the block that ends the window.</param>
        /// <returns>Return result as GetChainTxStats</returns>
        [ActionName("getchaintxstats")]
        [ActionDescription("Compute statistics about the total number and rate of transactions in the chain.")]
        public IActionResult GetChainTxStatus(int? nblocks, string blockhash)
        {
            try
            {
                var result = new GetChainTxStats();
                var chainRepository = this.FullNode.NodeService<ConcurrentChain>();
                var blockStoreManager = this.FullNode.NodeService<BlockStoreManager>();
                var blockCount = 30 * 24 * 60 * 60 / this.Network.Consensus.PowTargetSpacing.TotalSeconds;
                ChainedHeader lastChainedHeader = null;

                if (!string.IsNullOrEmpty(blockhash))
                {
                    var chain = chainRepository.GetBlock(new uint256(blockhash));
                    if (chain == null)
                    {
                        throw new ArgumentNullException("Block not found");
                    }
                    else
                    {
                        lastChainedHeader = chain;
                    }
                }
                else
                {
                    var chain = chainRepository.GetBlock(this.Chain.Height);
                    lastChainedHeader = chain;
                }

                if (!nblocks.HasValue)
                {
                    if (lastChainedHeader.Height <= blockCount)
                    {
                        blockCount = lastChainedHeader.Height;
                    }
                }
                else
                {
                    blockCount = nblocks.Value;

                    if (blockCount < 0 || (blockCount > 0 && blockCount >= lastChainedHeader.Height))
                    {
                        throw new ArgumentNullException("Invalid block count: should be between 0 and the block's height - 1");
                    }
                }

                var startHeight = lastChainedHeader.Height - (int)blockCount;
                var txCount = 0;
                var windowsTxCount = 0;
                ChainedHeader firstChainedHeader = null;
                for (int i = 0; i <= lastChainedHeader.Height; i++)
                {
                    var chainedHeader = chainRepository.GetBlock(i);
                    if (firstChainedHeader == null) firstChainedHeader = chainedHeader;
                    var block = blockStoreManager.BlockRepository.GetAsync(chainedHeader.HashBlock).Result;

                    if (block.Transactions != null)
                    {
                        txCount += block.Transactions.Count;
                        if (i >= startHeight) windowsTxCount += block.Transactions.Count;
                    }
                }

                var nTimeDiff = (firstChainedHeader.GetMedianTimePast() - lastChainedHeader.GetMedianTimePast()).TotalSeconds;

                result.Time = lastChainedHeader.Header.Time;
                result.WindowFinalBlockHash = lastChainedHeader.HashBlock.ToString();
                result.TxCount = txCount;
                result.WindowBlockCount = (int)blockCount;

                if (blockCount > 0)
                {
                    result.WindowTxCount = windowsTxCount;
                    result.WindowInterval = nTimeDiff;
                    if (nTimeDiff > 0)
                    {
                        result.TxRate = (((double)windowsTxCount) / nTimeDiff);
                    }
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

        [ActionName("getblockchaininfo")]
        [ActionDescription("Returns an object containing various state info regarding blockchain processing.")]
        public IActionResult GetBlockChainInfo()
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

        [ActionName("getmempoolentry")]
        [ActionDescription("Returns mempool data for given transaction")]
        public IActionResult GetMempoolEntry(string txid)
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

        [ActionName("getrawmempool")]
        [ActionDescription("Returns all transaction ids in memory pool as a json array of string transaction ids. Hint: use getmempoolentry to fetch a specific transaction from the mempool.")]
        public IActionResult GetRawMempool(string verbose)
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

        [ActionName("preciousblock")]
        [ActionDescription("Treats a block as if it were received before others with the same work. A later preciousblock call can override the effect of an earlier one. The effects of preciousblock are not retained across restarts.")]
        public IActionResult PreciousBlock(string blockhash)
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

        [ActionName("pruneblockchain")]
        [ActionDescription("The block height to prune up to. May be set to a discrete height, or a unix timestamp to prune blocks whose block time is at least 2 hours older than the provided timestamp.")]
        public IActionResult PruneBlockChain(int height)
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

        [ActionName("verifychain")]
        [ActionDescription("Verifies blockchain database.")]
        public IActionResult VerifyChain(int height)
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

        [ActionName("verifytxoutproof ")]
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
    }
}
