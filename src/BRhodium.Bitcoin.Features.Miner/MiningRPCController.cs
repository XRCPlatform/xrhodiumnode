using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using BRhodium.Node.Base;
using BRhodium.Node.Controllers;
using BRhodium.Bitcoin.Features.BlockStore;
using BRhodium.Bitcoin.Features.Consensus;
using BRhodium.Bitcoin.Features.Consensus.Interfaces;
using BRhodium.Bitcoin.Features.Consensus.Rules.CommonRules;
using BRhodium.Bitcoin.Features.MemoryPool.Interfaces;
using BRhodium.Bitcoin.Features.Miner.Interfaces;
using BRhodium.Bitcoin.Features.Miner.Models;
using BRhodium.Bitcoin.Features.RPC;
using BRhodium.Bitcoin.Features.Wallet;
using BRhodium.Bitcoin.Features.Wallet.Interfaces;
using BRhodium.Node.Interfaces;
using BRhodium.Node.Mining;
using BRhodium.Node.Utilities;
using BRhodium.Node.Utilities.Extensions;
using BRhodium.Node.Utilities.JsonContract;
using BRhodium.Node.Utilities.JsonErrors;
using NBitcoin.RPC;
using BRhodium.Node;

namespace BRhodium.Bitcoin.Features.Miner
{

    /// <summary>
    /// RPC controller for calls related to PoW mining
    /// </summary>
    /// <seealso cref="BRhodium.Node.Controllers.FeatureController" />
    [Controller]
    public class MiningRPCController : FeatureController
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>PoW miner.</summary>
        private readonly IPowMining powMining;

        /// <summary>Full node.</summary>
        private readonly IFullNode fullNode;

        /// <summary>Wallet manager.</summary>
        private readonly IWalletManager walletManager;

        private readonly INetworkDifficulty networkDifficulty;

        private readonly IBlockRepository blockRepository;

        private readonly ITxMempool txMempool;

        private readonly IConsensusLoop consensusLoop;

        private readonly IBlockProvider blockProvider;

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="powMining">PoW miner.</param>
        /// <param name="fullNode">Full node to offer mining RPC.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the node.</param>
        /// <param name="walletManager">The wallet manager.</param>
        public MiningRPCController(IPowMining powMining, IFullNode fullNode, ILoggerFactory loggerFactory, IWalletManager walletManager,
            INetworkDifficulty networkDifficulty = null,
            IBlockRepository blockRepository = null,
            ITxMempool txMempool = null,
            IConsensusLoop consensusLoop = null,
            IBlockProvider blockProvider = null,
            IChainState chainState = null) : base(fullNode: fullNode)
        {
            Guard.NotNull(powMining, nameof(powMining));
            Guard.NotNull(fullNode, nameof(fullNode));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(walletManager, nameof(walletManager));

            this.fullNode = fullNode;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.walletManager = walletManager;
            this.powMining = powMining;

            this.networkDifficulty = networkDifficulty;
            this.blockRepository = blockRepository;
            this.txMempool = txMempool;
            this.consensusLoop = consensusLoop;
            this.blockProvider = blockProvider;
            this.ChainState = chainState;
            this.Network = fullNode.Network;
        }

        /// <summary>
        /// Generates UTXOs to each unused addres in account used for stress testing
        /// </summary>
        /// <param name="walletName"></param>
        /// <param name="accountName"></param>
        /// <returns>uint256 list rpc format</returns>
        [ActionName("generateToAccountAddresses")]
        [ActionDescription("Tries to mine a given number of blocks and returns a list of block header hashes.")]
        public List<uint256> Generate(string walletName, string accountName)
        {
            WalletAccountReference accountReference = accountReference = new WalletAccountReference(walletName, accountName);
            List<uint256> res = new List<uint256>();
            foreach (HdAddress address in this.walletManager.GetUnusedAddresses(accountReference, 10))
            {
                res.AddRange(this.powMining.GenerateBlocks(new ReserveScript(address.Pubkey), (ulong)1, int.MaxValue));
            }
            return res;
        }

        /// <summary>
        /// Gets the network difficulty.
        /// </summary>
        /// <returns>Difficult of network</returns>
        private Target GetNetworkDifficulty()
        {
            return this.networkDifficulty?.GetNetworkDifficulty();
        }

        /// <summary>
        /// Gets the difficulty.
        /// </summary>
        /// <param name="hash">The hash.</param>
        /// <returns>Difficult of network</returns>
        [ActionName("getdifficulty")]
        [ActionDescription("Result—the current difficulty.")]
        public IActionResult GetDifficulty(string hash)
        {
            try
            {
                var difficulty = this.GetNetworkDifficulty().Difficulty;

                return this.Json(ResultHelper.BuildResultResponse(difficulty));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets the mining information.
        /// </summary>
        /// <returns>GetMiningInfo RPC format</returns>
        [ActionName("getmininginfo")]
        [ActionDescription("")]
        public IActionResult GetMiningInfo()
        {
            var miningInfo = new GetMiningInfo();

            miningInfo.Chain = this.Network.Name;
            miningInfo.Difficulty = this.GetNetworkDifficulty()?.Difficulty ?? 0;
            miningInfo.PooledTx = this.txMempool.MapTx.Count();
            miningInfo.NetworkHashps = GetNetworkHashPS();
            miningInfo.Blocks = this.ChainState?.ConsensusTip?.Height ?? 0;

            ChainedHeader chainTip = this.consensusLoop.Tip;
            int nExtraNonce = 0;
            lock (lockGetBlockTemplate)
            {
                BlockTemplate pblockTemplate = this.blockProvider.BuildPowBlock(chainTip, new Script());
                nExtraNonce = this.powMining.IncrementExtraNonce(pblockTemplate.Block, chainTip, nExtraNonce);
                var block = pblockTemplate.Block;

                if (block != null)
                {
                    miningInfo.CurrentBlockSize = block.GetSerializedSize();
                    miningInfo.CurrentBlockTx = block.Transactions.Count();
                }
            }
            return this.Json(ResultHelper.BuildResultResponse(miningInfo));
        }

        private static Object lockGetBlockTemplate = new Object();
        private static Object lockSubmitBlock = new Object();
        /// <summary>
        /// Gets the block template.
        /// </summary>
        /// <param name="args">The arguments.</param>
        /// <returns>GetBlockTemplateModel rpc format</returns>
        [ActionName("getblocktemplate")]
        [ActionDescription("")]
        public IActionResult GetBlockTemplate(string[] args)
        {
            var blockTemplate = new GetBlockTemplateModel();

            //generate template
            ChainedHeader chainTip = this.consensusLoop.Tip;
            int nExtraNonce = 0;
            BlockTemplate pblockTemplate;
            lock (lockGetBlockTemplate)
            {
                pblockTemplate = this.blockProvider.BuildPowBlock(chainTip, new Script());
                nExtraNonce = this.powMining.IncrementExtraNonce(pblockTemplate.Block, chainTip, nExtraNonce);

                var block = pblockTemplate.Block;
                var powCoinviewRule = this.consensusLoop.ConsensusRules.GetRule<PowCoinViewRule>();

                if (block != null)
                {
                    TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);

                    blockTemplate.Bits = string.Format("{0:x8}", block.Header.Bits.ToCompact());
                    blockTemplate.Curtime = DateTime.UtcNow.ToUnixTimestamp().ToString();
                    blockTemplate.PreviousBlockHash = block.Header.HashPrevBlock.ToString();
                    blockTemplate.Target = block.Header.Bits.ToString();

                    blockTemplate.Transactions = new List<TransactionContractModel>();

                    if (block.Transactions != null)
                    {
                        for (int i = 0; i < block.Transactions.Count; i++)
                        {
                            var item = block.Transactions[i];
                            if (!item.IsCoinBase)
                            {
                                var transaction = new TransactionContractModel();

                                transaction.Data = Encoders.Hex.EncodeData(item.ToBytes(ProtocolVersion.BTR_PROTOCOL_VERSION, this.Network));
                                transaction.Hash = item.GetWitHash().ToString();
                                transaction.Txid = Encoders.Hex.EncodeData(item.GetHash().ToBytes());

                                transaction.Fee = pblockTemplate.VTxFees[i];
                                transaction.Sigops = pblockTemplate.TxSigOpsCost[i];
                                transaction.Weight = item.GetSerializedSize(ProtocolVersion.BTR_PROTOCOL_VERSION);

                                //test decode
                                //var s = Transaction.Load(Encoders.Hex.DecodeData(transaction.Data), this.Network);

                                blockTemplate.Transactions.Add(transaction);
                            }
                        }
                    }

                    blockTemplate.Height = chainTip.Height + 1;
                    blockTemplate.Version = block.Header.Version;

                    blockTemplate.Coinbaseaux = new CoinbaseauxFlagsContractModel();
                    blockTemplate.Coinbaseaux.Flags = "062f503253482f";//"2f503253482f"
                    blockTemplate.CoinbaseValue = powCoinviewRule.GetProofOfWorkReward(blockTemplate.Height).Satoshi;

                    var mutable = new List<string>();
                    mutable.Add("nonces");
                    mutable.Add("time");
                    mutable.Add("time/decrement");
                    mutable.Add("time/increment");
                    mutable.Add("transactions");
                    mutable.Add("coinbase");
                    mutable.Add("coinbase/create");
                    mutable.Add("coinbase/append");
                    mutable.Add("generation");
                    mutable.Add("version/reduce");
                    mutable.Add("prevblock");
                    blockTemplate.Mutable = mutable;
                    blockTemplate.NonceRange = "00000000ffffffff";

                    var rules = new List<string>();
                    rules.Add("csv");
                    blockTemplate.Rules = rules;

                    var capabilities = new List<string>();
                    //capabilities.Add("proposal");
                    blockTemplate.Capabilities = capabilities;

                    blockTemplate.Vbavailable = new List<string>();
                    blockTemplate.Vbrequired = 0;
                    blockTemplate.Weightlimit = this.Network.Consensus.Option<PowConsensusOptions>().MaxBlockWeight;
                    blockTemplate.Sigoplimit = this.Network.Consensus.Option<PowConsensusOptions>().MaxBlockSigopsCost;
                    blockTemplate.Sizelimit = this.Network.Consensus.Option<PowConsensusOptions>().MaxBlockSerializedSize;

                    blockTemplate.Mintime = chainTip.GetMedianTimePast().AddHours(2).ToUnixTimeSeconds();//+two hour rule
                }
            }
            var json = ResultHelper.BuildResultResponse(blockTemplate);
            return this.Json(json);
        }

        /// <summary>
        /// Submits the block.
        /// </summary>
        /// <param name="hex">The hexadecimal.</param>
        /// <returns>SubmitBlockModel rpc format</returns>
        /// <exception cref="RPCException">
        /// Empty block hex supplied - null - false
        /// or
        /// Empty block hex supplied - null - false
        /// or
        /// Wrong chain work - null - false
        /// </exception>
        [ActionName("submitblock")]
        [ActionDescription("")]
        public IActionResult SubmitBlock(string hex)
        {
            var response = new SubmitBlockModel();
            
            lock (lockSubmitBlock)
            {
                if (string.IsNullOrEmpty(hex))
                {
                    throw new RPCException(RPCErrorCode.RPC_MISC_ERROR, "Empty block hex supplied", null, false);
                }

                var hexBytes = Encoders.Hex.DecodeData(hex);
                var pblock = PowBlock.Load(hexBytes, this.Network);

                if (pblock == null)
                {
                    throw new RPCException(RPCErrorCode.RPC_DESERIALIZATION_ERROR, "Empty block hex supplied", null, false);
                }

                var chainTip = this.consensusLoop.Chain.Tip;

                var newChain = new ChainedHeader(pblock.Header, pblock.GetHash(), chainTip);

                if (newChain.ChainWork <= chainTip.ChainWork)
                {
                    throw new RPCException(RPCErrorCode.RPC_MISC_ERROR, "Wrong chain work", null, false);
                }
                    
                var blockValidationContext = new BlockValidationContext { Block = pblock };

                this.consensusLoop.AcceptBlockAsync(blockValidationContext).GetAwaiter().GetResult();

                if (blockValidationContext.Error != null)
                {
                    blockValidationContext.Error.Throw();// not sure if consesus error should have non 200 status code
                    //response.Code = blockValidationContext.Error.Code;
                    //response.Message = blockValidationContext.Error.Message;                   
                    //return this.Json(ResultHelper.BuildResultResponse(response));
                }
                 
                var json = this.Json(ResultHelper.BuildResultResponse(""));// if block is successfuly accepted return null
                return json;
            }
           
        }

        /// <summary>
        /// Gets the network hash ps.
        /// </summary>
        /// <returns>Return double value od ps</returns>
        public double GetNetworkHashPS()
        {
            return GetNetworkHash(120, -1);
        }

        /// <summary>
        /// Gets the network hash ps.
        /// </summary>
        /// <param name="lookup">The lookup.</param>
        /// <param name="height">The height.</param>
        /// <returns></returns>
        private double GetNetworkHash(int lookup, int height)
        {
            var pb = this.consensusLoop.Chain.Tip;

            if (height >= 0 && height < this.consensusLoop.Chain.Height)
                pb = this.consensusLoop.Chain.GetBlock(height);

            if (pb == null) return 0;

            if (lookup <= 0)
                lookup = pb.Height % (int)this.Network.Consensus.DifficultyAdjustmentInterval + 1;

            if (lookup > pb.Height)
                lookup = pb.Height;

            var pb0 = pb;
            var minTime = pb0.Header.Time;
            var maxTime = minTime;
            for (int i = 0; i < lookup; i++)
            {
                pb0 = pb0.Previous;
                var time = pb0.Header.Time;
                minTime = Math.Min(time, minTime);
                maxTime = Math.Max(time, maxTime);
            }

            // In case there's a situation where minTime == maxTime, we don't want a divide by zero exception.
            if (minTime == maxTime)
                return 0;

            var workDiff = pb.ChainWork - pb0.ChainWork;
            var doubleWorkDiff = workDiff.ToDouble();
            var timeDiff = maxTime - minTime;

            return doubleWorkDiff / timeDiff;
        }
    }
}
