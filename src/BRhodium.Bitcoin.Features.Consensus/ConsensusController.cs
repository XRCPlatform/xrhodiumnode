using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using BRhodium.Bitcoin.Base;
using BRhodium.Bitcoin.Controllers;
using BRhodium.Bitcoin.Features.Consensus.Interfaces;
using BRhodium.Bitcoin.Utilities;
using BRhodium.Bitcoin.Utilities.JsonContract;
using System.Net;
using BRhodium.Bitcoin.Utilities.JsonErrors;
using System;
using BRhodium.Bitcoin.Features.Consensus.Models;

namespace BRhodium.Bitcoin.Features.Consensus
{
    public class ConsensusController : FeatureController
    {
        private readonly ILogger logger;

        public IConsensusLoop ConsensusLoop { get; private set; }

        public ConsensusController(ILoggerFactory loggerFactory, IChainState chainState = null,
            IConsensusLoop consensusLoop = null, ConcurrentChain chain = null)
            : base(chainState: chainState, chain: chain)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.ConsensusLoop = consensusLoop;
        }

        [ActionName("getbestblockhash")]
        [ActionDescription("Get the hash of the block at the consensus tip.")]
        public uint256 GetBestBlockHash()
        {
            Guard.NotNull(this.ChainState, nameof(this.ChainState));
            return this.ChainState?.ConsensusTip?.HashBlock;
        }

        [ActionName("getblockhash")]
        [ActionDescription("Gets the hash of the block at the given height.")]
        public uint256 GetBlockHash(int height)
        {
            Guard.NotNull(this.ConsensusLoop, nameof(this.ConsensusLoop));
            Guard.NotNull(this.Chain, nameof(this.Chain));

            this.logger.LogDebug("RPC GetBlockHash {0}", height);

            uint256 bestBlockHash = this.ConsensusLoop.Tip?.HashBlock;
            ChainedHeader bestBlock = bestBlockHash == null ? null : this.Chain.GetBlock(bestBlockHash);
            if (bestBlock == null)
                return null;
            ChainedHeader block = this.Chain.GetBlock(height);
            return block == null || block.Height > bestBlock.Height ? null : block.HashBlock;
        }

        [ActionName("getblock")]
        [ActionDescription("Returns a block details.")]
        public IActionResult GetBlock(string[] args)
        {
            try
            {
                var blockHash = uint256.Parse(args[0]);
                if (blockHash == null)
                {
                    var response = new Utilities.JsonContract.ErrorModel();
                    response.Code = "-5";
                    response.Message = "Block not found";
                    return this.Json(ResultHelper.BuildResultResponse(response));
                }
                var currentBlock = this.ConsensusLoop.Chain.GetBlock(blockHash);
                if (currentBlock == null)
                {
                    var response = new Utilities.JsonContract.ErrorModel();
                    response.Code = "-5";
                    response.Message = "Block not found";
                    return this.Json(ResultHelper.BuildResultResponse(response));
                }

                var blockModel = new BlockModel();
                blockModel.Hash = string.Format("{0:x8}", currentBlock.HashBlock);
                blockModel.Bits = string.Format("{0:x8}", currentBlock.Header.Bits.ToCompact());
                blockModel.Confirmations = this.ConsensusLoop.Chain.Tip.Height - currentBlock.Height;
                blockModel.Version = currentBlock.Header.Version;
                blockModel.MerkleRoot = string.Format("{0:x8}", currentBlock.Header.HashMerkleRoot);
                blockModel.Nonce = currentBlock.Header.Nonce;
                blockModel.Difficulty = currentBlock.Header.Bits.Difficulty;
                blockModel.Time = (int)currentBlock.Header.Time;
                blockModel.Height = currentBlock.Height;
                if (this.ConsensusLoop.Chain.Tip.Height > currentBlock.Height)
                {
                    blockModel.NextBlockHash = string.Format("{0:x8}", this.ConsensusLoop.Chain.GetBlock(currentBlock.Height + 1));
                }
                if (currentBlock.BlockDataAvailability == BlockDataAvailabilityState.BlockAvailable && currentBlock.Block != null)
                {
                    foreach (var tx in currentBlock?.Block.Transactions)
                    {
                        blockModel.Tx.Add(string.Format("{0:x8}", tx.GetHash()));
                    }
                }

                var json = ResultHelper.BuildResultResponse(blockModel);
                return this.Json(json);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

    }
}
