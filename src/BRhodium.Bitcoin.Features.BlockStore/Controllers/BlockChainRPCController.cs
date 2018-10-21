using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using BRhodium.Bitcoin.Features.BlockStore.Models;
using BRhodium.Node;
using BRhodium.Node.Base;
using BRhodium.Node.Configuration;
using BRhodium.Node.Controllers;
using BRhodium.Node.Interfaces;
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

        private readonly INetworkDifficulty networkDifficulty;

        public BlockChainRPCController(
            ILoggerFactory loggerFactory,
            INetworkDifficulty networkDifficulty = null,
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
            this.networkDifficulty = networkDifficulty;
        }

        /// <summary>
        /// Gets the block.
        /// If verbosity is 0, returns a string that is serialized, hex-encoded data for block 'hash'.
        /// If verbosity is 1, returns an Object with information about block 'hash'.
        /// If verbosity is 2, returns an Object with information about block 'hash' and information about each transaction. 
        /// </summary>
        /// <param name="hash">Hash of block.</param>
        /// <param name="verbosity">The verbosity.</param>
        /// <returns>(string or GetBlockWithTransactionModel) Return data based on verbosity.</returns>
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
        /// <param name="verbosity">The verbosity.</param>
        /// <returns>(string) Return block hash.</returns>
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
        /// <returns>(int) Block count.</returns>
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
        /// <param name="height">The height index.</param>
        /// <returns>(string) Hash of block.</returns>
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
        /// <returns>(List, GetChainTipModel) Object with informations.</returns>
        [ActionName("getchaintips")]
        [ActionDescription("Return information about all known tips in the block tree, including the main chain as well as orphaned branches.")]
        public IActionResult GetChainTips()
        {
            try
            {
                var result = new List<GetChainTipModel>();
                var chainRepository = this.FullNode.NodeService<ConcurrentChain>();

                for (int i = 0; i <= this.Chain.Height; i++)
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
        /// If verbose is false, returns a string that is serialized, hex-encoded data for blockheader 'hash'. If verbose is true, returns an Object with information about blockheader 'hash'.
        /// </summary>
        /// <param name="hash">The block hash.</param>
        /// <param name="verbose">True for a json object, false for the hex encoded data.</param>
        /// <returns>(string or GetBlockModel) Object with informations.</returns>
        [ActionName("getblockheader")]
        [ActionDescription("If verbose is false, returns a string that is serialized, hex-encoded data for blockheader 'hash'. If verbose is true, returns an Object with information about blockheader 'hash'.")]
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
                var block = this.GetBlockOrGenesisFromHeader(chainedHeader);

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
        /// <returns>(GetChainTxStats) Return object with result.</returns>
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

                    var block = this.GetBlockOrGenesisFromHeader(chainedHeader);

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
        /// Returns an object containing various state info regarding blockchain processing.
        /// </summary>
        /// <returns>(GetBlockChainInfoModel) Return object with informations.</returns>
        [ActionName("getblockchaininfo")]
        [ActionDescription("Returns an object containing various state info regarding blockchain processing.")]
        public IActionResult GetBlockChainInfo()
        {
            try
            {
                var chainRepository = this.FullNode.NodeService<ConcurrentChain>();
                var blockStoreManager = this.FullNode.NodeService<BlockStoreManager>();
                var ibdState = this.FullNode.NodeService<IInitialBlockDownloadState>();
                var storeSettings = this.FullNode.NodeService<StoreSettings>();

                var result = new GetBlockChainInfoModel();
                result.AutomaticPruning = storeSettings.Prune;
                result.BestBlockHash = chainRepository.Tip.HashBlock.ToString();
                result.Blocks = chainRepository.Height;
                result.Chain = this.Network.Name.Replace("BRhodium", string.Empty);
                result.ChainWork = this.Network.Name;

                var difficulty = this.networkDifficulty?.GetNetworkDifficulty().Difficulty;
                if (difficulty.HasValue) result.Difficulty = difficulty.Value;

                result.Headers = chainRepository.Height;
                result.InitialBlockDownload = ibdState.IsInitialBlockDownload();

                var actulHeader = chainRepository.GetBlock(this.Chain.Height);
                result.MedianTime = actulHeader.GetMedianTimePast().ToUnixTimeSeconds();
                result.Pruned = storeSettings.Prune;
                result.PruneHeight = null;
                result.PruneTargetSize = null;

                for (int i = 0; i <= this.Chain.Height; i++)
                {
                    var chainedHeader = chainRepository.GetBlock(i);
                    var block = this.GetBlockOrGenesisFromHeader(chainedHeader);
                    if (block!= null)
                    {
                        result.SizeOnDisk += block.GetSerializedSize();
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
        /// Verifies blockchain database.
        /// 
        /// 0 - Check reading blocks from chain
        /// 1 - Validate header of blocks
        /// </summary>
        /// <param name="checklevel">How thorough the block verification is.</param>
        /// <param name="nblocks">The number of blocks to check.</param>
        /// <returns>(bool) True or False.</returns>
        [ActionName("verifychain")]
        [ActionDescription("Verifies blockchain database.")]
        public IActionResult VerifyChain(int? checklevel, int? nblocks)
        {
            try
            {
                if (!checklevel.HasValue) checklevel = 1;
                if (!nblocks.HasValue) nblocks = 0;

                if (this.Chain.Tip == null) return this.Json(ResultHelper.BuildResultResponse(true));

                if (nblocks <= 0 || nblocks > this.Chain.Height) nblocks = this.Chain.Height;
                
                Console.WriteLine(string.Format("Verifying last {0} blocks at level {1}", nblocks, checklevel));

                int reportDone = 0;
                int err = 0;
                Console.WriteLine(string.Format("Verify [{0} %] done", reportDone));

                var chainRepository = this.FullNode.NodeService<ConcurrentChain>();
                var blockStoreManager = this.FullNode.NodeService<BlockStoreManager>();

                for (int i = (this.Chain.Height - nblocks.Value); i <= nblocks; i++)
                {
                    int percentageDone = (int)(((double)(100 / nblocks)) * i);
                    if (reportDone < percentageDone / 10)
                    {
                        Console.WriteLine(string.Format("Verify [{0} %] done", reportDone));
                        reportDone = percentageDone / 10;
                    }

                    var chainedHeader = chainRepository.GetBlock(i);
                    var block = this.GetBlockOrGenesisFromHeader(chainedHeader);

                    if (checklevel >= 0)
                    {
                        if (chainedHeader != null)
                        {
                            if (block == null)
                            {
                                Console.WriteLine(string.Format("*** ReadBlockFromDisk failed at {0}, hash={1}", i, chainedHeader.HashBlock.ToString()));
                                err++;
                            }
                        }
                        else
                        {
                            Console.WriteLine(string.Format("*** ReadBlockFromDisk failed at {0}", i));
                            err++;
                        }

                        if (checklevel >= 1)
                        {
                            if (!chainedHeader.Validate(this.Network))
                            {
                                Console.WriteLine(string.Format("*** found bad block at {0}, hash={1}", i, chainedHeader.HashBlock.ToString()));
                            }
                        }

                        if (checklevel >= 2)
                        {
                            //not implemented
                        }
                    }
                }

                return this.Json(ResultHelper.BuildResultResponse(err > 0 ? false : true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        private Block GetBlockOrGenesisFromHeader(ChainedHeader chainedHeader)
        {
            Block block;
            var blockStoreManager = this.FullNode.NodeService<BlockStoreManager>();

            if (chainedHeader.HashBlock == Network.GenesisHash)
            {
                block = this.Network.GetGenesis();
            }
            else
            {
                block = blockStoreManager.BlockRepository.GetAsync(chainedHeader.HashBlock).Result;
            }

            return block;
        }
    }
}
