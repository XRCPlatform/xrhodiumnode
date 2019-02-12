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

    }
}
