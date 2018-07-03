using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Newtonsoft.Json.Linq;
using BRhodium.Bitcoin.Base;
using BRhodium.Bitcoin.Configuration;
using BRhodium.Bitcoin.Controllers;
using BRhodium.Bitcoin.Features.Consensus.Interfaces;
using BRhodium.Bitcoin.Features.RPC.Models;
using BRhodium.Bitcoin.Interfaces;
using BRhodium.Bitcoin.Utilities;
using BRhodium.Bitcoin.Utilities.Extensions;
using BRhodium.Bitcoin.Utilities.JsonContract;
using BRhodium.Bitcoin.Utilities.JsonErrors;
using System.Net;
using BRhodium.Bitcoin.Features.BlockStore;
using System.Collections.Generic;

namespace BRhodium.Bitcoin.Features.RPC.Controllers
{
    public class ExplorerController : FeatureController
    {

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private readonly IPooledTransaction pooledTransaction;

        /// <summary>An interface implementation used to retrieve unspent transactions from a pooled source.</summary>
        private readonly IPooledGetUnspentTransaction pooledGetUnspentTransaction;

        /// <summary>An interface implementation used to retrieve unspent transactions.</summary>
        private readonly IGetUnspentTransaction getUnspentTransaction;

        private readonly INetworkDifficulty networkDifficulty;

        /// <summary>Manager of the longest fully validated chain of blocks.</summary>
        private readonly IConsensusLoop consensusLoop;

        public ExplorerController(
            ILoggerFactory loggerFactory,
            IPooledTransaction pooledTransaction = null,
            IPooledGetUnspentTransaction pooledGetUnspentTransaction = null,
            IGetUnspentTransaction getUnspentTransaction = null,
            INetworkDifficulty networkDifficulty = null,
            IConsensusLoop consensusLoop = null,
            IFullNode fullNode = null,
            NodeSettings nodeSettings = null,
            Network network = null,
            ConcurrentChain chain = null,
            IChainState chainState = null,
            Connection.IConnectionManager connectionManager = null)
            : base(
                  fullNode: fullNode,
                  nodeSettings: nodeSettings,
                  network: network,
                  chain: chain,
                  chainState: chainState,
                  connectionManager: connectionManager)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.pooledTransaction = pooledTransaction;
            this.pooledGetUnspentTransaction = pooledGetUnspentTransaction;
            this.getUnspentTransaction = getUnspentTransaction;
            this.networkDifficulty = networkDifficulty;
            this.consensusLoop = consensusLoop;
        }

        [ActionName("getexplorerlatestblocks")]
        public IActionResult GetExplorerLatestBlocks(int limit)
        {
            try
            {
                var chainRepository = this.FullNode.NodeService<ConcurrentChain>();
                var blockStoreManager = this.FullNode.NodeService<BlockStoreManager>();

                var result = new List<ExplorerBlockModel>();

                for (int i = 0; i < limit; i++)
                {
                    var height = chainRepository.Height;
                    if ((height - i) >= 0)
                    {
                        var chainedHeader = chainRepository.GetBlock(height - i);
                        var block = blockStoreManager.BlockRepository.GetAsync(chainedHeader.HashBlock).Result;

                        var newBlock = new ExplorerBlockModel();
                        newBlock.Size = block.GetSerializedSize();
                        newBlock.Height = chainedHeader.Height;
                        newBlock.Age = chainedHeader.Header.BlockTime;
                        newBlock.Hash = chainedHeader.HashBlock.ToString();

                        if ((block.Transactions != null) && (block.Transactions.Count() > 0))
                        {
                            newBlock.Transactions = new List<ExplorerTransactionModel>();

                            foreach (var itemTransaction in block.Transactions)
                            {
                                var newTransaction = new ExplorerTransactionModel();
                                newTransaction.Hash = itemTransaction.GetHash().ToString();
                                newTransaction.Satoshi = itemTransaction.TotalOut.Satoshi;

                                newBlock.Transactions.Add(newTransaction);
                            }

                            newBlock.TotalSatoshi = newBlock.Transactions.Sum(a => a.Satoshi);
                        }

                        result.Add(newBlock);
                    }
                    else
                    {
                        break;
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

        [ActionName("getexplorerblock")]
        public IActionResult GetExplorerBlock(string hash)
        {
            try
            {
                var blockStoreManager = this.FullNode.NodeService<BlockStoreManager>();
                var chainRepository = this.FullNode.NodeService<ConcurrentChain>();

                var result = new ExplorerBlockModel();

                var block = blockStoreManager.BlockRepository.GetAsync(new uint256(hash)).Result;
                var chainedHeader = chainRepository.GetBlock(new uint256(hash));

                var chainedNextHeader = chainRepository.GetBlock(chainedHeader.Height + 1);

                if (block != null)
                {
                    result.Size = block.GetSerializedSize();
                    result.Height = chainedHeader.Height;
                    result.Age = chainedHeader.Header.BlockTime;
                    result.Hash = chainedHeader.HashBlock.ToString();
                    result.Bits = string.Format("{0:x8}", block.Header.Bits.ToCompact());
                    result.Version = block.Header.Version;
                    result.Difficult = block.Header.Bits.Difficulty;
                    result.PrevHash = block.Header.HashPrevBlock.ToString();
                    result.NextHash = chainedNextHeader != null ? chainedNextHeader.Header.GetHash().ToString() : null;
                    result.MerkleRoot = block.Header.HashMerkleRoot.ToString();

                    if ((block.Transactions != null) && (block.Transactions.Count() > 0))
                    {
                        result.Transactions = new List<ExplorerTransactionModel>();

                        foreach (var itemTransaction in block.Transactions)
                        {
                            var newTransaction = new ExplorerTransactionModel();
                            newTransaction.Hash = itemTransaction.GetHash().ToString();
                            newTransaction.Satoshi = itemTransaction.TotalOut.Satoshi;
                            newTransaction.Time = DateTimeOffset.FromUnixTimeSeconds(itemTransaction.Time);
                            newTransaction.Size = itemTransaction.GetSerializedSize();
                            result.Transactions.Add(newTransaction);

                            if (itemTransaction.Inputs != null)
                            {
                                foreach (var itemInput in itemTransaction.Inputs)
                                {
                                    var address = itemInput.ScriptSig.GetScriptAddress(this.Network);

                                    var newAddress = new ExplorerAddressModel();
                                    newAddress.Address = address.ToString();
                                }
                            }

                            if (itemTransaction.Outputs != null)
                            {
                                foreach (var itemOutput in itemTransaction.Outputs)
                                {
                                    var address = itemOutput.ScriptPubKey.GetDestinationAddress(this.Network);

                                    var newAddress = new ExplorerAddressModel();
                                    newAddress.Address = address.ToString();
                                    newAddress.Satoshi = itemOutput.Value.Satoshi;
                                    newAddress.Scripts = itemOutput.ScriptPubKey.ToString();
                                }
                            }

                            var s = new TxOutList(itemTransaction);

                            //TODO: fee
                            //var e = s.AsCoins().ToArray();
                            //var ss = itemTransaction.GetFee(e);
                        }

                        result.TotalSatoshi = result.Transactions.Sum(a => a.Satoshi);
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
    }
}
