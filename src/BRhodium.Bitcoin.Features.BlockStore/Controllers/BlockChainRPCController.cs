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

        public BlockChainRPCController(
            ILoggerFactory loggerFactory,
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
                        var blockTemplate = new GetBlockModel<GetTransactionBlockModel>();
                        blockTemplate = FillBlockBaseData<GetTransactionBlockModel>(blockTemplate, block, chainedHeader);

                        foreach (var item in block.Transactions)
                        {
                            blockTemplate.Transactions.Add(new GetTransactionBlockModel(item.GetHash().ToString()));
                        }

                        return this.Json(ResultHelper.BuildResultResponse(blockTemplate));

                    case 2:
                        var detailBlockTemplate = new GetBlockModel<GetTransactionDateBlockModel>();
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
        private GetBlockModel<T> FillBlockBaseData<T>(GetBlockModel<T> blockTemplate, Block block, ChainedHeader chainedHeader)
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
    }
}
