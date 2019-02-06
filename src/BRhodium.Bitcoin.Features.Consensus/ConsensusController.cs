using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using BRhodium.Node.Base;
using BRhodium.Node.Controllers;
using BRhodium.Bitcoin.Features.Consensus.Interfaces;
using BRhodium.Node.Utilities;
using BRhodium.Node.Utilities.JsonContract;
using System.Net;
using BRhodium.Node.Utilities.JsonErrors;
using System;
using BRhodium.Bitcoin.Features.Consensus.Models;
using BRhodium.Bitcoin.Features.BlockStore;
using BRhodium.Node.Configuration;
using NBitcoin.RPC;
using BRhodium.Node;

namespace BRhodium.Bitcoin.Features.Consensus
{
    public class ConsensusController : FeatureController
    {
        private readonly ILogger logger;
        public IConsensusLoop ConsensusLoop { get; private set; }
        private readonly ILoggerFactory loggerFactory;
        private BlockStoreCache blockStoreCache;
        private readonly IBlockRepository blockRepository;
        private readonly NodeSettings nodeSettings;
        private readonly Network network;

        public ConsensusController(
            ILoggerFactory loggerFactory,
            IBlockRepository blockRepository,
            NodeSettings nodeSettings,
            Network network,
            IChainState chainState = null,
            IConsensusLoop consensusLoop = null,
            ConcurrentChain chain = null,
            IFullNode fullNode = null
            )
            : base(chainState: chainState, chain: chain)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.loggerFactory = loggerFactory;
            this.ConsensusLoop = consensusLoop;
            this.nodeSettings = nodeSettings;
            this.blockRepository = blockRepository;
            this.network = network;
            this.blockStoreCache = new BlockStoreCache(this.blockRepository, DateTimeProvider.Default, this.loggerFactory, this.nodeSettings);
            this.FullNode = fullNode;
        }

        [ActionName("getbestblockhash")]
        [ActionDescription("Get the hash of the block at the consensus tip.")]
        public uint256 GetBestBlockHash()
        {
            Guard.NotNull(this.ChainState, nameof(this.ChainState));
            return this.ChainState?.ConsensusTip?.HashBlock;
        }
        [ActionName("invalidateblock")]
        [ActionDescription("Get the hash of the block at the consensus tip.")]
        public uint256 InvalidateBlockHash(string[] args)
        {
            Guard.NotNull(this.ChainState, nameof(this.ChainState));
            var blockHash = uint256.Parse(args[0]);
            if (blockHash == null)
            {
                throw new RPCException(RPCErrorCode.RPC_INVALID_ADDRESS_OR_KEY, "Block not found", null, false);
            }
            this.ChainState.MarkBlockInvalid(blockHash);
            //this.blockRepository.DeleteAsync(blockHash).GetAwaiter().GetResult();

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
        //[ActionName("gettransaction")]
        //[ActionDescription("Returns a transaction details.")]
        //public IActionResult GetTransaction(string[] args)
        //{
        //    try
        //    {
        //        var reqTransactionId = uint256.Parse(args[0]);
        //        if (reqTransactionId == null)
        //        {
        //            var response = new Utilities.JsonContract.ErrorModel();
        //            response.Code = "-5";
        //            response.Message = "Invalid or non-wallet transaction id";
        //            return this.Json(ResultHelper.BuildResultResponse(response));
        //        }
        //        var block = this.blockRepository.GetTrxBlockIdAsync(reqTransactionId).Result; //this brings block hash for given transaction
        //        var currentTransaction = this.blockRepository.GetTrxAsync(reqTransactionId).Result;
        //        if (currentTransaction == null)
        //        {
        //            var response = new Utilities.JsonContract.ErrorModel();
        //            response.Code = "-5";
        //            response.Message = "Invalid or non-wallet transaction id";
        //            return this.Json(ResultHelper.BuildResultResponse(response));
        //        }

        //        var transactionResponse = new TransactionModel();
        //        transactionResponse.NormTxId = string.Format("{0:x8}", currentTransaction.GetHash());
        //        transactionResponse.TxId = string.Format("{0:x8}", currentTransaction.GetHash());
        //        transactionResponse.Confirmations = - 1;//extract from coinbase script sig
        //        transactionResponse.BlockHash = string.Format("{0:x8}", block);
        //        //transactionResponse.BlockIndex = The index of the transaction in the block that includes it
        //        transactionResponse.Details = new System.Collections.Generic.List<TransactionDetail>();
        //        foreach (var item in currentTransaction.Outputs)
        //        {
        //            var detail = new TransactionDetail();
        //            detail.Account = item.ScriptPubKey.GetSignerAddress(this.network).ToString();
        //            detail.Category = "receive";
        //            detail.Amount = (double)item.Value.Satoshi / 100000000;
        //        }



        //        var json = ResultHelper.BuildResultResponse(transactionResponse);
        //        return this.Json(json);
        //    }
        //    catch (Exception e)
        //    {
        //        this.logger.LogError("Exception occurred: {0}", e.ToString());
        //        return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
        //    }
        //}
        [ActionName("getblock")]
        [ActionDescription("Returns a block details.")]
        public IActionResult GetBlock(string blockHashHex, int verbosity = 1)
        {
            // exceptions correctly handled and formated at RPCMiddleware layer
            switch (verbosity)
            {
                case 1:
                case 2:
                default:
                    var blockModel = this.GetBlockVerbose(blockHashHex, verbosity);
                    return this.Json(ResultHelper.BuildResultResponse(blockModel));
                case 0:
                    var blockModelHex = GetBlockHex(blockHashHex);
                    return this.Json(ResultHelper.BuildResultResponse(blockModelHex));
            }
        }

        public BlockModel GetBlockVerbose(string blockHashHex, int verbosity)
        {
            return GetBlockVerbose(this.GetChainedHeader(blockHashHex), verbosity);
        }

        public BlockModel GetBlockVerbose(ChainedHeader currentBlock, int verbosity)
        {
            var blockModel = new BlockModel();
            var blockStoreManager = this.FullNode.NodeService<BlockStoreManager>();
            var block = blockStoreManager.BlockRepository.GetAsync(currentBlock.HashBlock).Result;

            blockModel.Hash = string.Format("{0:x8}", currentBlock.HashBlock);
            blockModel.Bits = string.Format("{0:x8}", currentBlock.Header.Bits.ToCompact());
            blockModel.Confirmations = this.ConsensusLoop.Chain.Tip.Height - currentBlock.Height;
            blockModel.Version = currentBlock.Header.Version;
            blockModel.VersionHex = currentBlock.Header.Version.ToString("X");
            blockModel.MerkleRoot = string.Format("{0:x8}", currentBlock.Header.HashMerkleRoot);
            blockModel.Difficulty = currentBlock.Header.Bits.Difficulty;
            blockModel.Time = (int)currentBlock.Header.Time;
            blockModel.Height = currentBlock.Height;
            blockModel.ChainWork = currentBlock.ChainWork.ToString();

            blockModel.Weight = block.GetSerializedSize(this.Chain.Network, TransactionOptions.None) *
                (this.Chain.Network.Consensus.Option<PowConsensusOptions>().WitnessScaleFactor - 1) +
                block.GetSerializedSize(this.Chain.Network, TransactionOptions.Witness);

            blockModel.ProofHash = currentBlock.Header.GetPoWHash(currentBlock.Height, Network.Main.Consensus.PowLimit2Height);
            if (this.ConsensusLoop.Chain.Tip.Height > currentBlock.Height)
            {
                blockModel.NextBlockHash = string.Format("{0:x8}", this.ConsensusLoop.Chain.GetBlock(currentBlock.Height + 1).Header.GetHash());
            }
            //CachedCoinView cachedCoinView = this.ConsensusLoop.UTXOSet as CachedCoinView;
            //blockRepo.GetBlockHashAsync().GetAwaiter().GetResult();
            blockModel.Nonce = currentBlock.Header.Nonce; //fullBlock.Header.Nonce; nonce is 0 here as well ist it important for this?

            if (blockModel.Height > 0)
            {
                blockModel.PreviousBlockHash = string.Format("{0:x8}", this.ConsensusLoop.Chain.GetBlock(currentBlock.Height - 1).Header.GetHash());
                Block fullBlock = this.blockStoreCache.GetBlockAsync(currentBlock.HashBlock).GetAwaiter().GetResult();
                if (fullBlock == null)
                {
                    throw new Exception("Failed to load block transactions");// this is for diagnostic purposes to see how often this happens
                }

                foreach (var tx in fullBlock.Transactions)
                {
                    blockModel.Tx.Add(string.Format("{0:x8}", tx.GetHash()));
                }
            }

            return blockModel;
        }

        public string GetBlockHex(string blockHashHex)
        {
            return GetBlockHex(GetChainedHeader(blockHashHex));
        }

        public string GetBlockHex(ChainedHeader currentBlock)
        {
            var blockStoreManager = this.FullNode.NodeService<BlockStoreManager>();
            var block = blockStoreManager.BlockRepository.GetAsync(currentBlock.HashBlock).Result;
            var blockAsHex = block.ToHex(this.Chain.Network);
            return blockAsHex;
        }

        private ChainedHeader GetChainedHeader(string blockHashHex)
        {
            var blockHash = uint256.Parse(blockHashHex);
            if (blockHash == null)
            {
                throw new RPCException(RPCErrorCode.RPC_INVALID_ADDRESS_OR_KEY, "Block not found", null, false);
            }
            var currentBlock = this.ConsensusLoop.Chain.GetBlock(blockHash);
            if (currentBlock == null)
            {
                throw new RPCException(RPCErrorCode.RPC_INVALID_ADDRESS_OR_KEY, "Block not found", null,false);
            }

            return currentBlock;
        }

    }
}
