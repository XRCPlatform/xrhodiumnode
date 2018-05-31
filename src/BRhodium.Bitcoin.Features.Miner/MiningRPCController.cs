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
using BRhodium.Bitcoin.Base;
using BRhodium.Bitcoin.Controllers;
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
using BRhodium.Bitcoin.Interfaces;
using BRhodium.Bitcoin.Mining;
using BRhodium.Bitcoin.Utilities;
using BRhodium.Bitcoin.Utilities.Extensions;
using BRhodium.Bitcoin.Utilities.JsonContract;
using BRhodium.Bitcoin.Utilities.JsonErrors;

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
            try
            {
                var miningInfo = new GetMiningInfo();

                miningInfo.Chain = this.Network.Name;
                miningInfo.Difficulty = this.GetNetworkDifficulty()?.Difficulty ?? 0;
                miningInfo.PooledTx = this.txMempool.MapTx.Count();
                miningInfo.NetworkHashps = GetNetworkHashPS();
                miningInfo.Blocks = this.ChainState?.ConsensusTip?.Height ?? 0;

                ChainedHeader chainTip = this.consensusLoop.Tip;
                int nExtraNonce = 0;
                BlockTemplate pblockTemplate = this.blockProvider.BuildPowBlock(chainTip, new Script());
                nExtraNonce = this.powMining.IncrementExtraNonce(pblockTemplate.Block, chainTip, nExtraNonce);
                var block = pblockTemplate.Block;

                if (block != null)
                {
                    miningInfo.CurrentBlockSize = block.GetSerializedSize();
                    miningInfo.CurrentBlockTx = block.Transactions.Count();
                }

                return this.Json(ResultHelper.BuildResultResponse(miningInfo));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("getblocktemplate")]
        [ActionDescription("")]
        public IActionResult GetBlockTemplate(string[] args)
        {
            try
            {
                var blockTemplate = new GetBlockTemplateModel();

                //var hash = this.ChainState?.ConsensusTip?.HashBlock;
                //var block = this.consensusLoop.Tip;

                //generate template
                ChainedHeader chainTip = this.consensusLoop.Tip;
                int nExtraNonce = 0;
                BlockTemplate pblockTemplate = this.blockProvider.BuildPowBlock(chainTip, new Script());
                nExtraNonce = this.powMining.IncrementExtraNonce(pblockTemplate.Block, chainTip, nExtraNonce);
                var block = pblockTemplate.Block;
                var powCoinviewRule = this.consensusLoop.ConsensusRules.GetRule<PowCoinViewRule>();

               // var orgBlockHex = block.ToHex(this.Network);

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

                                transaction.Data = Encoders.Hex.EncodeData(item.ToBytes(ProtocolVersion.ALT_PROTOCOL_VERSION, this.Network));
                                transaction.Hash = item.GetWitHash().ToString();
                                transaction.Txid = Encoders.Hex.EncodeData(item.GetHash().ToBytes());

                                transaction.Fee = pblockTemplate.VTxFees[i];
                                transaction.Sigops = pblockTemplate.TxSigOpsCost[i];
                                transaction.Weight = item.GetSerializedSize(ProtocolVersion.ALT_PROTOCOL_VERSION);

                                //test decode
                                var s = Transaction.Load(Encoders.Hex.DecodeData(transaction.Data), this.Network);

                                blockTemplate.Transactions.Add(transaction);
                            }                            
                        }
                    }


                 //   foreach (var item in block.Transactions)
                 //   {
                 //       var transaction = new TransactionContractModel();

                 //       var txHash = item.GetHash();

                 //       transaction.Data  = Encoders.Hex.EncodeData(item.ToBytes(ProtocolVersion.ALT_PROTOCOL_VERSION, this.Network));
                 //       transaction.Hash = item.GetHash().ToString();
                 //       transaction.Txid = item.ToHex(this.Network, SerializationType.Hash);

                 //       //  var itemTransaction = item.Value.Transaction;


                 ///*       transaction.Data = block.Transactions[0].ToHex(this.Network, SerializationType.Hash);
                 //   transaction.Data = block.Transactions[0].ToHex(this.Network, SerializationType.Disk);
                 //   transaction.Data = block.Transactions[0].ToHex(this.Network, SerializationType.Network);
                 //   transaction.Hash = transaction.Txid = block.Transactions[0].GetHash().ToString();
                 //   */

                 //   //"010000000332a82e92f522deee69b09e27858ba9b87585f2a4913ef71018df40909032fdc3000000006a473044022019ca05cb880a04f0d842268b7e75ac6d2695fc544df033e3daeb29239251a8970220031f6336767f2ea617347484e1290ec0bdcc71056ea2d3084e75384905250ec50121030dd394118fb66ca288bff71d8ea762678783b005770f7f9ba4128233191e0847ffffffff086747cbd339b21b950774186091653a7b8f5751b00a906ff6f5561b3a6fcee6010000006b4830450221009ae1ba9a216d313cc592fc2c1ef08f1e0e555a32b6c1b305f685ac882d38356b0220243106bbb5bb76dde142e574cba8f30c1e2f7059e8e9161770396fbd2b50420f0121030dd394118fb66ca288bff71d8ea762678783b005770f7f9ba4128233191e0847ffffffffe2f15804b1e41c36c925c6f64f219b2bdb3c9fbff4c97a4f0e8c7f31d7e6f2af000000006b48304502200be8894fdd7f5c19be248a979c08bbf2395f606e038c3e02c0266474c03699ab022100ff5de87086e487410f5d7b68012655ca6d814f0caeb9ca42d9c425a90f68b3030121030dd394118fb66ca288bff71d8ea762678783b005770f7f9ba4128233191e0847ffffffff02a0f01900000000001976a9141c50209a1dfdf53313d237b75e9aeb553ca1dfda88ac00e1f505000000001976a914cbb9a3e7a7c1651b1006f876f08b40be85b274f588ac00000000"; // itemTransaction.ToHex();
                 //   //transaction.Txid = "dc3a80ec6c45aa489453b2c4abf6761eb6656d949e26d01793458c166640e5f3"; // itemTransaction.GetHash().ToString();
                 //   //transaction.Hash = "dc3a80ec6c45aa489453b2c4abf6761eb6656d949e26d01793458c166640e5f3"; // transaction.Txid;

                 //       transaction.Fee = 0; pblockTemplate.VTxFees
                 //       transaction.Sigops = pblockTemplate.TxSigOpsCost.First();
                 //       transaction.Weight = powCoinviewRule.GetBlockWeight(block);

                 //       blockTemplate.Transactions.Add(transaction);

                    //}

                    uint256 hashMerkleRoot2 = BlockMerkleRootRule.BlockMerkleRoot(block, out bool mutated);
                    block.Transactions[0].Inputs[0].ScriptSig = null;
                    uint256 hashMerkleRoot21 = BlockMerkleRootRule.BlockMerkleRoot(block, out bool mutated1);


                    blockTemplate.Height = chainTip.Height + 1;
                    blockTemplate.Version = block.Header.Version;
                    //blockTemplate.Coinbasetxn = pblockTemplate.Coinbasetxn;
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

                var json = ResultHelper.BuildResultResponse(blockTemplate);
                return this.Json(json);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("submitblock")]
        [ActionDescription("")]
        public IActionResult SubmitBlock(string hex)
        {
            var response = new SubmitBlockModel();
            try
            {

                //hex = "00000020ca641282f1695e1f07e4406c815c97145f4809ef1899621ef7c142190ae1cb0bf5c9a586c5d6ad7cd45ee5e524f5f98f202d8676820a3ec9b0943ebb7a84105a03bb0b5bffff7f20000ddbdd020100000003bb0b5b010000000000000000000000000000000000000000000000000000000000000000ffffffff03014800ffffffff0100f2052a010000000000000000";
                if (string.IsNullOrEmpty(hex))
                {
                    response.Code = "-1";
                    response.Message = "Empty block hex supplied";
                    return this.Json(ResultHelper.BuildResultResponse(response));
                }

                ChainedHeader chainTip2 = this.consensusLoop.Tip;
                int nExtraNonce = 0;
                BlockTemplate pblockTemplate = this.blockProvider.BuildPowBlock(chainTip2, new Script());
                nExtraNonce = this.powMining.IncrementExtraNonce(pblockTemplate.Block, chainTip2, nExtraNonce);
                var block = pblockTemplate.Block;
                var powCoinviewRule = this.consensusLoop.ConsensusRules.GetRule<PowCoinViewRule>();


                var hexBytes = Encoders.Hex.DecodeData(hex);
                //var hexBytes = Encoders.Hex.DecodeData("00000020a7c2fe86a220229e95ff4508c5aefca57474bb10b54308688f535044b24998158e238d32e6d0e16873acf46cb4756b4b7fe3f94649b0b96d6b6343b691dbecc2fdafc85affff0f1f0002be4a0201000000010000000000000000000000000000000000000000000000000000000000000000ffffffff1d53044830df5a0820000000000000000d2f6e6f64655374726174756d2f0000000002eb51b8fe000000001976a914393edc35e70f071ba3c982ad2579872c9ab9978588ac14ae4701000000001976a9145c5b3aa410fcbe1da66f8e6734035634077cb23588ac0000000001000000fdafc85a010000000000000000000000000000000000000000000000000000000000000000ffffffff025200ffffffff0180b2e60e000000001976a914824f74b33d3f8ad032e0ee3f79c5775061ddad2388ac00000000");

                //var orgHeaderHex = block.Header.ToHex(this.Network);
                var orgBlockHex = block.ToHex(this.Network);
                var hexBytes2 = Encoders.Hex.DecodeData(orgBlockHex);
                //var h = BlockHeader.Load(hexBytes, this.Network);
                //var s = Transaction.Load(hexBytes, this.Network);

                var pblock = PosBlock.Load(hexBytes, this.Network);
                //var pblock2 = PosBlock.Load(hexBytes2, this.Network);

                pblock.GetMerkleRoot();
                var chainTip = this.consensusLoop.Chain.Tip;

                var newChain = new ChainedHeader(pblock.Header, pblock.GetHash(), chainTip);

                if (newChain.ChainWork <= chainTip.ChainWork)
                    throw new Exception("Wrong chain work");

                var blockValidationContext = new BlockValidationContext { Block = pblock };

                this.consensusLoop.AcceptBlockAsync(blockValidationContext).GetAwaiter().GetResult();


                /*
                if (blockValidationContext.ChainedBlock == null)
                {
                    this.logger.LogTrace("(-)[REORG-2]");
                    return blocks;
                }

                if (blockValidationContext.Error != null)
                {
                    if (blockValidationContext.Error == ConsensusErrors.InvalidPrevTip)
                        continue;

                    this.logger.LogTrace("(-)[ACCEPT_BLOCK_ERROR]");
                    return blocks;
                }

                this.logger.LogInformation("Mined new {0} block: '{1}'.", BlockStake.IsProofOfStake(blockValidationContext.Block) ? "POS" : "POW", blockValidationContext.ChainedBlock);

                nHeight++;
                blocks.Add(pblock.GetHash());
                */

                if (blockValidationContext.Error != null)
                {
                    response.Code = blockValidationContext.Error.Code;
                    response.Message = blockValidationContext.Error.Message;
                    return this.Json(ResultHelper.BuildResultResponse(response));
                }             

                var json = this.Json(response);
                return json;
            }
            catch (Exception e)
            {
                response.Code = "-22";
                response.Message = "Block decode failed";

                //response.RaiseError(new Error("Block decode failed", -22));
                /*
                 * 
                 * {
	                    "result": null,
	                    "error": {
		                    "code": -22,
		                    "message": "Block decode failed"
	                    },
	                    "id": null
                    }
                    */

                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return this.Json(ResultHelper.BuildResultResponse(response));
                //return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        public double GetNetworkHashPS()
        {
            return GetNetworkHashPS(120, -1);
        }

        private double GetNetworkHashPS(int lookup, int height)
        {
            //CBlockIndex *pb = chainActive.Tip();
            var pb = this.consensusLoop.Chain.Tip;

            //if (height >= 0 && height < chainActive.Height())
            //    pb = chainActive[height];
            if (height >= 0 && height < this.consensusLoop.Chain.Height)
                pb = this.consensusLoop.Chain.GetBlock(height);

            //if (pb == nullptr || !pb->nHeight)
            //  return 0;
            if (pb == null) return 0;

            // If lookup is -1, then use blocks since last difficulty change.
            //if (lookup <= 0)
            //lookup = pb->nHeight % Params().GetConsensus().DifficultyAdjustmentInterval() + 1;
            if (lookup <= 0)
                lookup = pb.Height % (int)this.Network.Consensus.DifficultyAdjustmentInterval + 1;

            // If lookup is larger than chain, then set it to chain length.
            //if (lookup > pb->nHeight)
            //    lookup = pb->nHeight;
            if (lookup > pb.Height)
                lookup = pb.Height;

            /*
            CBlockIndex* pb0 = pb;
            int64_t minTime = pb0->GetBlockTime();
            int64_t maxTime = minTime;
            for (int i = 0; i < lookup; i++)
            {
                pb0 = pb0->pprev;
                int64_t time = pb0->GetBlockTime();
                minTime = std::min(time, minTime);
                maxTime = std::max(time, maxTime);
            }
            */
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

            /* arith_uint256 workDiff = pb->nChainWork - pb0->nChainWork;
            int64_t timeDiff = maxTime - minTime;
            return workDiff.getdouble() / timeDiff;*/
            //var workDiff = new uint256(pb.ChainWork, pb0.ChainWork, true);
         /*   var workDiff = pb.ChainWork - pb0.ChainWork;
            var ssss = workDiff.ToDouble();
            var timeDiff = maxTime - minTime;
            var doubleWorkDiff = pb.ChainWork.ToDouble();
            var s = pb0.ChainWork.ToDouble();
            var ss = (doubleWorkDiff - s);*/
            return 0.0002; // ss / timeDiff;
        }
    }
}
