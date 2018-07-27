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
    /// RPC controller for calls related to PoW mining and PoS minting.
    /// </summary>
    [Controller]
    public class MiningRPCController : FeatureController
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>PoW miner.</summary>
        private readonly IPowMining powMining;

        /// <summary>PoS staker.</summary>
        private readonly IPosMinting posMinting;

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
        /// <param name="posMinting">PoS staker or null if PoS staking is not enabled.</param>
        public MiningRPCController(IPowMining powMining, IFullNode fullNode, ILoggerFactory loggerFactory, IWalletManager walletManager,
            INetworkDifficulty networkDifficulty = null,
            IBlockRepository blockRepository = null,
            ITxMempool txMempool = null,
            IConsensusLoop consensusLoop = null,
            IBlockProvider blockProvider = null,
            IChainState chainState = null,
            IPosMinting posMinting = null) : base(fullNode: fullNode)
        {
            Guard.NotNull(powMining, nameof(powMining));
            Guard.NotNull(fullNode, nameof(fullNode));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(walletManager, nameof(walletManager));

            this.fullNode = fullNode;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.walletManager = walletManager;
            this.powMining = powMining;
            this.posMinting = posMinting;

            this.networkDifficulty = networkDifficulty;
            this.blockRepository = blockRepository;
            this.txMempool = txMempool;
            this.consensusLoop = consensusLoop;
            this.blockProvider = blockProvider;
            this.ChainState = chainState;
            this.Network = fullNode.Network;
        }

        /// <summary>
        /// Tries to mine one or more blocks.
        /// </summary>
        /// <param name="blockCount">Number of blocks to mine.</param>
        /// <returns>List of block header hashes of newly mined blocks.</returns>
        /// <remarks>It is possible that less than the required number of blocks will be mined because the generating function only
        /// tries all possible header nonces values.</remarks>
        [ActionName("generate")]
        [ActionDescription("Tries to mine a given number of blocks and returns a list of block header hashes.")]
        public List<uint256> Generate(int blockCount)
        {
            this.logger.LogTrace("({0}:{1})", nameof(blockCount), blockCount);
            if (blockCount <= 0)
            {
                throw new RPCServerException(NBitcoin.RPC.RPCErrorCode.RPC_INVALID_REQUEST, "The number of blocks to mine must be higher than zero.");
            }

            WalletAccountReference accountReference = this.GetAccount();
            HdAddress address = this.walletManager.GetUnusedAddress(accountReference);

            List<uint256> res = this.powMining.GenerateBlocks(new ReserveScript(address.Pubkey), (ulong)blockCount, int.MaxValue);

            this.logger.LogTrace("(-):*.{0}={1}", nameof(res.Count), res.Count);
            return res;
        }

        /// <summary>
        /// Starts staking a wallet.
        /// </summary>
        /// <param name="walletName">The name of the wallet.</param>
        /// <param name="walletPassword">The password of the wallet.</param>
        /// <returns></returns>
        [ActionName("startstaking")]
        [ActionDescription("Starts staking a wallet.")]
        public bool StartStaking(string walletName, string walletPassword)
        {
            Guard.NotEmpty(walletName, nameof(walletName));
            Guard.NotEmpty(walletPassword, nameof(walletPassword));

            this.logger.LogTrace("({0}:{1})", nameof(walletName), walletName);

            Wallet.Wallet wallet = this.walletManager.GetWallet(walletName);

            // Check the password
            try
            {
                Key.Parse(wallet.EncryptedSeed, walletPassword, wallet.Network);
            }
            catch (Exception ex)
            {
                throw new SecurityException(ex.Message);
            }

            this.fullNode.NodeFeature<MiningFeature>(true).StartStaking(walletName, walletPassword);

            return true;
        }

        /// <summary>
        /// Implements "getstakinginfo" RPC call.
        /// </summary>
        /// <param name="isJsonFormat">Indicates whether to provide data in JSON or binary format.</param>
        /// <returns>Staking information RPC response.</returns>
        [ActionName("getstakinginfo")]
        [ActionDescription("Gets the staking information.")]
        public GetStakingInfoModel GetStakingInfo(bool isJsonFormat = true)
        {
            this.logger.LogTrace("({0}:{1})", nameof(isJsonFormat), isJsonFormat);

            if (!isJsonFormat)
            {
                this.logger.LogError("Binary serialization is not supported for RPC '{0}'.", nameof(this.GetStakingInfo));
                throw new NotImplementedException();
            }

            GetStakingInfoModel model = this.posMinting != null ? this.posMinting.GetGetStakingInfoModel() : new GetStakingInfoModel();

            this.logger.LogTrace("(-):{0}", model);
            return model;
        }

        /// <summary>
        /// Finds first available wallet and its account.
        /// </summary>
        /// <returns>Reference to wallet account.</returns>
        private WalletAccountReference GetAccount()
        {
            this.logger.LogTrace("()");

            string walletName = this.walletManager.GetWalletsNames().FirstOrDefault();
            if (walletName == null)
                throw new RPCServerException(NBitcoin.RPC.RPCErrorCode.RPC_INVALID_REQUEST, "No wallet found");

            HdAccount account = this.walletManager.GetAccounts(walletName).FirstOrDefault();
            if (account == null)
                throw new RPCServerException(NBitcoin.RPC.RPCErrorCode.RPC_INVALID_REQUEST, "No account found on wallet");

            var res = new WalletAccountReference(walletName, account.Name);

            this.logger.LogTrace("(-):'{0}'", res);
            return res;
        }

        private Target GetNetworkDifficulty()
        {
            return this.networkDifficulty?.GetNetworkDifficulty();
        }

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

        public double GetNetworkHashPS()
        {
            return GetNetworkHashPS(120, -1);
        }

        private double GetNetworkHashPS(int lookup, int height)
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
