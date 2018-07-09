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
using Newtonsoft.Json;

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

        private ExplorerBlockModel ParseExplorerBlock(Block block, ChainedHeader chainedHeader, ChainedHeader chainedNextHeader = null)
        {
            var result = new ExplorerBlockModel();
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
                    newTransaction.AddressTo = new List<ExplorerAddressModel>();
                    newTransaction.AddressFrom = new List<ExplorerAddressModel>();

                    newTransaction.Hash = itemTransaction.GetHash().ToString();
                    newTransaction.Satoshi = itemTransaction.TotalOut.Satoshi;
                    newTransaction.Time = DateTimeOffset.FromUnixTimeSeconds(itemTransaction.Time);
                    newTransaction.Size = itemTransaction.GetSerializedSize();
                    newTransaction.BlockHash = block.GetHash().ToString();

                    if (itemTransaction.Inputs != null)
                    {
                        foreach (var itemInput in itemTransaction.Inputs)
                        {
                            var address = itemInput.ScriptSig.GetScriptAddress(this.Network);

                            var newAddress = new ExplorerAddressModel();
                            newAddress.Address = address.ToString();

                            newTransaction.AddressFrom.Add(newAddress);
                        }
                    }

                    if (itemTransaction.Outputs != null)
                    {
                        foreach (var itemOutput in itemTransaction.Outputs)
                        {
                            var address = itemOutput.ScriptPubKey.GetDestinationAddress(this.Network);
                            if (address == null) address = itemOutput.ScriptPubKey.GetScriptAddress(this.Network);

                            var newAddress = new ExplorerAddressModel();
                            newAddress.Address = address.ToString();
                            newAddress.Satoshi = itemOutput.Value.Satoshi;
                            newAddress.Scripts = itemOutput.ScriptPubKey.ToString();

                            newTransaction.AddressTo.Add(newAddress);
                        }
                    }

                    result.Transactions.Add(newTransaction);
                }

                result.TotalSatoshi = result.Transactions.Sum(a => a.Satoshi);
            }

            return result;
        }

        [ActionName("getexplorerlatestblocks")]
        public IActionResult GetExplorerLatestBlocks(int limit)
        {
            try
            {
                var chainRepository = this.FullNode.NodeService<ConcurrentChain>();
                var blockStoreManager = this.FullNode.NodeService<BlockStoreManager>();

                var result = new List<ExplorerBlockModel>();

                ChainedHeader chainedNextHeader = null;

                for (int i = 0; i < limit; i++)
                {
                    var height = chainRepository.Height;
                    if ((height - i) >= 0)
                    {
                        var chainedHeader = chainRepository.GetBlock(height - i);
                        var block = blockStoreManager.BlockRepository.GetAsync(chainedHeader.HashBlock).Result;

                        var newBlock = ParseExplorerBlock(block, chainedHeader, chainedNextHeader);

                        chainedNextHeader = chainedHeader;

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

                if (chainedHeader != null)
                {
                    if (block == null) block = new PowBlock(chainedHeader.Header);
                    result = ParseExplorerBlock(block, chainedHeader, chainedNextHeader);
                }

                return this.Json(ResultHelper.BuildResultResponse(result));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("getexplorertransaction")]
        public IActionResult GetExplorerTransaction(string hash)
        {
            try
            {
                var blockStoreManager = this.FullNode.NodeService<BlockStoreManager>();
                var chainRepository = this.FullNode.NodeService<ConcurrentChain>();

                var result = new ExplorerBlockModel();
                ChainedHeader chainedNextHeader = null;

                for (int i = this.Chain.Height; i >= 0; i--)
                {
                    var chainedHeader = chainRepository.GetBlock(i);
                    var block = blockStoreManager.BlockRepository.GetAsync(chainedHeader.HashBlock).Result;

                    if ((block.Transactions != null) && (block.Transactions.Count() > 0))
                    {
                        foreach (var itemTransaction in block.Transactions)
                        {
                            if (itemTransaction.GetHash().ToString() == hash)
                            {
                                result = ParseExplorerBlock(block, chainedHeader, chainedNextHeader);
                                chainedNextHeader = chainedHeader;
                                break;
                            }
                        }
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

        [ActionName("getexploreraddress")]
        public IActionResult GetExplorerAddress(long offset, string ignoreJson, string address)
        {
            try
            {
                var blockStoreManager = this.FullNode.NodeService<BlockStoreManager>();
                var chainRepository = this.FullNode.NodeService<ConcurrentChain>();

                var result = new List<ExplorerBlockModel>();
                ChainedHeader chainedNextHeader = null;

                var ignoreArray = JsonConvert.DeserializeObject<List<int>>(ignoreJson);

                for (int i = this.Chain.Height; i > offset; i--)
                {
                    if (ignoreArray.Contains(i))
                    {
                        continue;
                    }

                    var chainedHeader = chainRepository.GetBlock(i);
                    var block = blockStoreManager.BlockRepository.GetAsync(chainedHeader.HashBlock).Result;

                    try
                    {
                        if ((block.Transactions != null) && (block.Transactions.Count() > 0))
                        {                                    
                            var newBlock = ParseExplorerBlock(block, chainedHeader, chainedNextHeader);

                            if (newBlock.Transactions != null)
                            {
                                foreach (var itemTx in newBlock.Transactions)
                                {
                                    if (itemTx.AddressFrom != null)
                                    {
                                        if (itemTx.AddressFrom.Exists(a => a.Address == address))
                                        {
                                            result.Add(newBlock);
                                            continue;
                                        }

                                        if (itemTx.AddressTo.Exists(a => a.Address == address))
                                        {
                                            result.Add(newBlock);
                                            continue;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {

                        var s = e; 
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


        [ActionName("getexploreraddressbyheight")]
        public IActionResult GetExplorerAddressByHeight(string heightsJson)
        {
            try
            {
                var blockStoreManager = this.FullNode.NodeService<BlockStoreManager>();
                var chainRepository = this.FullNode.NodeService<ConcurrentChain>();

                var result = new List<ExplorerBlockModel>();
                ChainedHeader chainedNextHeader = null;

                var heightsArray = JsonConvert.DeserializeObject<List<int>>(heightsJson);

                foreach (var itemHeight in heightsArray)
                {
                    var chainedHeader = chainRepository.GetBlock(itemHeight);
                    var block = blockStoreManager.BlockRepository.GetAsync(chainedHeader.HashBlock).Result;

                    try
                    {
                        if ((block.Transactions != null) && (block.Transactions.Count() > 0))
                        {
                            var newBlock = ParseExplorerBlock(block, chainedHeader, chainedNextHeader);

                            result.Add(newBlock);
                        }
                    }
                    catch (Exception e)
                    {

                        var s = e;
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
