using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using BRhodium.Node.Base;
using BRhodium.Node.Configuration;
using BRhodium.Node.Controllers;
using BRhodium.Bitcoin.Features.Consensus.Interfaces;
using BRhodium.Bitcoin.Features.RPC.Models;
using BRhodium.Node.Interfaces;
using BRhodium.Node.Utilities.JsonContract;
using BRhodium.Node.Utilities.JsonErrors;
using System.Net;
using BRhodium.Bitcoin.Features.BlockStore;
using System.Collections.Generic;
using Newtonsoft.Json;
using BRhodium.Node;
using BRhodium.Node.Utilities.Extensions;

namespace BRhodium.Bitcoin.Features.RPC.Controllers
{
    public class ExplorerRPCController : FeatureController
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Instance of block store manager.</summary>
        private readonly BlockStoreManager BlockStoreManager;

        public ExplorerRPCController(
            ILoggerFactory loggerFactory,
            ConcurrentChain chain,
            BlockStoreManager blockStoreManager,
            IFullNode fullNode = null,
            NodeSettings nodeSettings = null,
            Network network = null,
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
            this.BlockStoreManager = blockStoreManager;
        }

        private ExplorerBlockModel ParseExplorerBlock(Block block,
            ChainedHeader chainedHeader,
            ChainedHeader chainedNextHeader = null)
        {
            var result = new ExplorerBlockModel();
            result.Size = block.GetSerializedSize();
            result.Height = chainedHeader.Height;
            result.Age = chainedHeader.Header.BlockTime;
            result.Hash = chainedHeader.HashBlock.ToString();
            result.Bits = string.Format("{0:x8}", block.Header.Bits.ToCompact());
            result.Version = block.Header.Version;
            result.Difficult = block.Header.Bits.DifficultySafe();
            result.PrevHash = block.Header.HashPrevBlock.ToString();
            result.NextHash = chainedNextHeader != null ? chainedNextHeader.Header.GetHash().ToString() : null;
            result.MerkleRoot = block.Header.HashMerkleRoot.ToString();

            if ((block.Transactions != null) && (block.Transactions.Count() > 0))
            {
                result.Transactions = new List<ExplorerTransactionModel>();

                foreach (var currentTx in block.Transactions)
                {
                    var newTx = new ExplorerTransactionModel();
                    newTx.AddressTo = new List<ExplorerAddressModel>();
                    newTx.AddressFrom = new List<ExplorerAddressModel>();

                    newTx.Hash = currentTx.GetHash().ToString();
                    newTx.Time = chainedHeader.Header.BlockTime;
                    newTx.Size = currentTx.GetSerializedSize();
                    newTx.BlockHash = block.GetHash().ToString();

                    if (currentTx.Inputs != null)
                    {
                        if (!currentTx.IsCoinBase)
                        {
                            //read prevTx from blockchain
                            var prevTxList = new List<IndexedTxOut>();

                            foreach (var itemInput in currentTx.Inputs)
                            {
                                var prevTx = this.BlockStoreManager.BlockRepository.GetTrxAsync(itemInput.PrevOut.Hash).GetAwaiter().GetResult();
                                IndexedTxOut outTx = null;
                                if (prevTx != null)
                                {
                                    if (prevTx.Outputs.Count() > itemInput.PrevOut.N)
                                    {
                                        var indexed = prevTx.Outputs.AsIndexedOutputs();
                                        outTx = indexed.First(i => i.N == itemInput.PrevOut.N);
                                        prevTxList.Add(outTx);
                                    }
                                }

                                var address = itemInput.ScriptSig.GetSignerAddress(this.Network);
                                if (address != null)
                                {
                                    var newAddress = new ExplorerAddressModel();
                                    newAddress.Address = address.ToString();

                                    if (outTx != null)
                                    {
                                        newAddress.Satoshi = outTx.TxOut.Value.ToUnit(MoneyUnit.Satoshi);
                                        newAddress.Scripts = outTx.TxOut.ScriptPubKey.ToString();
                                    }

                                    newTx.AddressFrom.Add(newAddress);
                                }
                            }

                            var totalInputs = prevTxList.Sum(i => i.TxOut.Value.ToUnit(MoneyUnit.Satoshi));
                            var fee = totalInputs - currentTx.TotalOut.ToUnit(MoneyUnit.Satoshi);
                            newTx.Fee = fee;
                            result.TransactionFees += fee;
                        }
                    }

                    if (currentTx.Outputs != null)
                    {
                        var i = 0;
                        foreach (var itemOutput in currentTx.Outputs)
                        {
                            var address = Transaction.GetPlainTxOutDestinationAddress(itemOutput.ScriptPubKey, this.Network);

                            var newAddress = new ExplorerAddressModel();
                            newAddress.Address = address;
                            newAddress.Satoshi = itemOutput.Value.Satoshi;
                            newAddress.Scripts = itemOutput.ScriptPubKey.ToString();

                            newTx.AddressTo.Add(newAddress);
                            i++;
                        }

                        if (newTx.AddressTo != null) newTx.Satoshi = newTx.AddressTo.Sum(b => b.Satoshi);
                    }

                    result.Transactions.Add(newTx);
                }

                result.TotalSatoshi = result.Transactions.Sum(a => a.Satoshi);
            }

            return result;
        }

        /// <summary>
        /// Gets the explorer latest blocks.
        /// </summary>
        /// <param name="limit">The limit.</param>
        /// <returns>(List, ExplorerBlockModel) Object with information.</returns>
        [ActionName("getexplorerlatestblocks")]
        public IActionResult GetExplorerLatestBlocks(int limit)
        {
            try
            {
                var result = new List<ExplorerBlockModel>();

                ChainedHeader chainedNextHeader = null;

                for (int i = 0; i < limit; i++)
                {
                    var height = this.Chain.Height;
                    if ((height - i) >= 0)
                    {
                        var chainedHeader = this.Chain.GetBlock(height - i);
                        var block = this.BlockStoreManager.BlockRepository.GetAsync(chainedHeader.HashBlock).Result;

                        if (block == null) block = new Block(chainedHeader.Header);
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

        /// <summary>
        /// Gets the explorer block.
        /// </summary>
        /// <param name="hash">The hash.</param>
        /// <returns>(ExplorerBlockModel) Object with information.</returns>
        [ActionName("getexplorerblock")]
        public IActionResult GetExplorerBlock(string hash)
        {
            try
            {
                var result = new ExplorerBlockModel();
                var block = this.BlockStoreManager.BlockRepository.GetAsync(new uint256(hash)).Result;
                var chainedHeader = this.Chain.GetBlock(new uint256(hash));
                var chainedNextHeader = this.Chain.GetBlock(chainedHeader.Height + 1);

                if (chainedHeader != null)
                {
                    if (block == null) block = new Block(chainedHeader.Header);
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

        /// <summary>
        /// Gets the explorer block by his height.
        /// </summary>
        /// <param name="height">The height.</param>
        /// <returns>(ExplorerBlockModel) Object with information.</returns>
        [ActionName("getexplorerblockbyheight")]
        public IActionResult GetExplorerBlockByHeight(int height)
        {
            try
            {
                var result = new ExplorerBlockModel();
                var chainedHeader = this.Chain.GetBlock(height);
                var chainedNextHeader = this.Chain.GetBlock(chainedHeader.Height + 1);

                if (chainedHeader != null)
                {
                    var block = this.BlockStoreManager.BlockRepository.GetAsync(chainedHeader.HashBlock).Result;
                    if (block == null) block = new Block(chainedHeader.Header);
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

        /// <summary>
        /// Gets the explorer transaction.
        /// </summary>
        /// <param name="hash">The hash.</param>
        /// <returns>(ExplorerBlockModel) Object with information.</returns>
        [ActionName("getexplorertransaction")]
        public IActionResult GetExplorerTransaction(string hash)
        {
            try
            {
                var result = new ExplorerBlockModel();

                var txHash = new uint256(hash);
                var blockHash = this.BlockStoreManager.BlockRepository.GetTrxBlockIdAsync(txHash).GetAwaiter().GetResult();
                var chainedHeader = this.Chain.GetBlock(new uint256(blockHash));
                var chainedNextHeader = this.Chain.GetBlock(chainedHeader.Height + 1);
                var block = this.BlockStoreManager.BlockRepository.GetAsync(chainedHeader.HashBlock).Result;

                if ((block != null) && (block.Transactions != null) && (block.Transactions.Count() > 0))
                {
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

        /// <summary>
        /// Gets the explorer address.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <param name="ignoreJson">Ignored height of blocks json.</param>
        /// <param name="address">The address to find.</param>
        /// <returns>(List, ExplorerBlockModel) Object with information.</returns>
        [ActionName("getexploreraddress")]
        public IActionResult GetExplorerAddress(long offset, string ignoreJson, string address)
        {
            try
            {
                var result = new List<ExplorerBlockModel>();
                var ignoreArray = JsonConvert.DeserializeObject<List<int>>(ignoreJson);

                for (int i = this.Chain.Height; i >= offset; i--)
                {
                    if (ignoreArray.Contains(i))
                    {
                        continue;
                    }

                    var chainedHeader = this.Chain.GetBlock(i);
                    var block = this.BlockStoreManager.BlockRepository.GetAsync(chainedHeader.HashBlock).Result;

                    try
                    {
                        if ((block != null) && (block.Transactions != null) && (block.Transactions.Count() > 0))
                        {
                            var isForAdd = false;

                            foreach (var currentTx in block.Transactions)
                            {
                                foreach (var itemOutput in currentTx.Outputs)
                                {
                                    var txOutAddress = Transaction.GetPlainTxOutDestinationAddress(itemOutput.ScriptPubKey, this.Network);

                                    if ((txOutAddress != null) && (txOutAddress.ToString() == address))
                                    {
                                        isForAdd = true;
                                        break;
                                    }
                                }

                                if (isForAdd)
                                {
                                    break;
                                }
                            }

                            if (isForAdd)
                            {
                                var chainedNextHeader = this.Chain.GetBlock(chainedHeader.Height + 1);
                                var explorerBlock = ParseExplorerBlock(block, chainedHeader, chainedNextHeader);
                                result.Add(explorerBlock);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        //be quite
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
        /// Gets the blocks by heights.
        /// </summary>
        /// <param name="heightsJson">The heights json.</param>
        /// <returns>(List, ExplorerBlockModel) Object with information.</returns>
        [ActionName("getexploreraddressbyheight")]
        public IActionResult GetExplorerAddressByHeight(string heightsJson)
        {
            try
            {
                var result = new List<ExplorerBlockModel>();
                var heightsArray = JsonConvert.DeserializeObject<List<int>>(heightsJson);

                foreach (var itemHeight in heightsArray)
                {
                    var chainedHeader = this.Chain.GetBlock(itemHeight);
                    if (chainedHeader == null) continue;

                    var block = this.BlockStoreManager.BlockRepository.GetAsync(chainedHeader.HashBlock).Result;
                    var chainedNextHeader = this.Chain.GetBlock(chainedHeader.Height + 1);

                    try
                    {
                        if (block == null) block = new Block(chainedHeader.Header);

                        var newBlock = ParseExplorerBlock(block, chainedHeader, chainedNextHeader);

                        result.Add(newBlock);
                    }
                    catch (Exception e)
                    {
                        //be quite
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
