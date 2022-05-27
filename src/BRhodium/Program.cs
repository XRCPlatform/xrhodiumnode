using System;
using System.Threading.Tasks;
using NBitcoin;
using BRhodium.Node;
using BRhodium.Node.Builder;
using BRhodium.Node.Configuration;
using BRhodium.Bitcoin.Features.BlockStore;
using BRhodium.Bitcoin.Features.Consensus;
using BRhodium.Bitcoin.Features.MemoryPool;
using BRhodium.Bitcoin.Features.Miner;
using BRhodium.Bitcoin.Features.Miner.Interfaces;
using BRhodium.Bitcoin.Features.RPC;
using BRhodium.Bitcoin.Features.Wallet;
using BRhodium.Bitcoin.Features.Wallet.Interfaces;
using BRhodium.Node.Utilities;
using System.Linq;
using BRhodium.Node.Base;
using BRhodium.Node.P2P.Protocol.Behaviors;
using BRhodium.Bitcoin.Features.Consensus.Interfaces;
using BRhodium.Bitcoin.Features.MemoryPool.Interfaces;
using BRhodium.Bitcoin.Features.Miner.Controllers;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using BRhodium.Bitcoin.Features.Miner.Models;
using Microsoft.AspNetCore.Mvc;
using BRhodium.Node.Utilities.JsonContract;
using BRhodium.Bitcoin.Features.Wallet.Controllers;
using BRhodium.Bitcoin.Features.RPC.Controllers;
using System.Collections.Generic;
using BRhodium.Bitcoin.Features.RPC.Models;

namespace BRhodium
{
    public class Program
    {
        public static void Main(string[] args)
        {
            MainAsync(args).Wait();
        }

        public static async Task MainAsync(string[] args)
        {
            try
            {
                //args = new string[] { "-regtest" };
                // args = new string[] { "-testnet" };
                NodeSettings nodeSettings = new NodeSettings(args: args, loadConfiguration: false);

                var node = new FullNodeBuilder()
                    .UseNodeSettings(nodeSettings)
                    .UsePowConsensus()
                    .UseBlockStore()
                    .UseMempool()
                    .AddMining()
                    .AddRPC()
                    .UseWallet()
                    .Build();

                //BTR Generate Prime Wallet
                //Task.Delay(TimeSpan.FromSeconds(15)).ContinueWith((t) => { TestFee(node); }).GetAwaiter();
                //Task.Delay(TimeSpan.FromSeconds(15)).ContinueWith((t) => { WalletTest(node); }).GetAwaiter();
                //Task.Delay(TimeSpan.FromSeconds(15)).ContinueWith((t) => { GetBlock(node); }).GetAwaiter();
               //  Task.Delay(TimeSpan.FromSeconds(15)).ContinueWith((t) => { TestSend(node); }).GetAwaiter();
               //  Task.Delay(TimeSpan.FromSeconds(15)).ContinueWith((t) => { SubmitBlock(node); }).GetAwaiter();

                //importaddresstest
                //Task.Delay(TimeSpan.FromSeconds(15)).ContinueWith((t) => { WalletImport(node); }).GetAwaiter();
                //Task.Delay(TimeSpan.FromSeconds(15)).ContinueWith((t) => { DecodeTest(node); }).GetAwaiter();
                //Task.Delay(TimeSpan.FromSeconds(15)).ContinueWith((t) => { TestTxGeneration(node); }).GetAwaiter();

                if (node != null)
                    await node.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was a problem initializing the node. Details: '{0}'", ex.Message);
            }
        }

        public static void TestFee(IFullNode fullNode)
        {
            var miningRPC = fullNode.NodeService<MiningRPCController>();

            try
            {
                var miningInfo = (JsonResult)miningRPC.GetMiningInfo();
                var exx = (ResultModel)miningInfo.Value;
                var info = (GetMiningInfo)exx.Result;

                var block = info.Blocks;

                //var miningInfoObject = JToken<>
                var sss = miningRPC.EstimateSmartFee(10, "CONSERVATIVE");
                var ss2s = miningRPC.EstimateFee(10);
            }
            catch (Exception)
            {
                //exist then nothing
            }

            var s = true;
            //if (!isExist)
            //{
            //    var password = "kokotnejapNy2=";
            //    var passphrase = "practice involve fish demise across edit wing practice excess unaware chimney tourist";

            //    // create the wallet
            //    var mnemonic = walletManager.CreateWallet(password, "mypoolcore.wallet", passphrase);
            //}
        }

        public static void WalletTest(IFullNode fullNode)
        {
            var walletRPC = fullNode.NodeService<WalletRPCController>();

            try
            {
                //   var result = walletRPC.ListTransactions("wallet", 200, 200);
                //   var resultNEw = walletRPC.ListTransactions("newwallet", 200, 200);
                // var resultx2 = walletRPC.GetWallet("c52ef6fc-285d-441f-9aca-737c0ad3f0a1");
                //    var resultx2xx = walletRPC.GetHistory("c52ef6fc-285d-441f-9aca-737c0ad3f0a1");
                 var result2 = walletRPC.ListSinceBlock("04fcf046-a32d-4790-ad36-7bc45ec0a252");
                var resultx2 = walletRPC.GetWallet("04fcf046-a32d-4790-ad36-7bc45ec0a252");
                var result2x = walletRPC.ListTransactions("04fcf046-a32d-4790-ad36-7bc45ec0a252", 100);
                // var result3 = walletRPC.ListSinceBlock("wallet", "27e56d89055b76dda1872f2b02c91cc443305e5296e67a7a208565914156859f");
                // var result4 = walletRPC.ListSinceBlock("wallet", "d98bea994e2205156fee55b08ac893c5fe5018414f2af0fc2bc3e8eabf8de6a5");
                // var result5 = walletRPC.ListSinceBlock("newwallet", "d98bea994e2205156fee55b08ac893c5fe5018414f2af0fc2bc3e8eabf8de6a5");
                //var result2 = walletRPC.ListTransactions("wallet", "");
                //var miningInfo = (JsonResult)miningRPC.GetMiningInfo();
                //var exx = (ResultModel)miningInfo.Value;
                //var info = (GetMiningInfo)exx.Result;

                //var block = info.Blocks;

                ////var miningInfoObject = JToken<>
                //var sss = miningRPC.EstimateSmartFee(10, "CONSERVATIVE");
                //var ss2s = miningRPC.EstimateFee(10);
            }
            catch (Exception)
            {
                //exist then nothing
            }

            var s = true;
            //if (!isExist)
            //{
            //    var password = "kokotnejapNy2=";
            //    var passphrase = "practice involve fish demise across edit wing practice excess unaware chimney tourist";

            //    // create the wallet
            //    var mnemonic = walletManager.CreateWallet(password, "mypoolcore.wallet", passphrase);
            //}
        }

        public static void GetBlock(IFullNode fullNode)
        {
            var blockRPC = fullNode.NodeService<BlockChainRPCController>();

            
            try
            {
                var blockX = "00000020dd07046c1084ca11ad8a1dad76d6666e1e34b05a59e6f126ea4d0c661d0c5fc2d2ee64261d69a5ad9f8ec435fdd13120308109b65711348aee4e0405445035060a000000ffff031e2dd0ba270102000000010000000000000000000000000000000000000000000000000000000000000000ffffffff2502702a062f503253482f045785de5c08810000a94f0c00007a657267706f6f6c2e636f6d00000000000180b2e60e000000001976a9147e87f39a35723c9370d3fb57312bdfe8f8552ecf88ac00000000";

                var ssss = Block.Parse(blockX, fullNode.Network);
                var miningInfo = blockRPC.GetBlock("a3cb00951a1c2b17042547b3da12723f3a02c6531317b3cf604972dcb327a5af");
                
            }
            catch (Exception)
            {
                //exist then nothing
            }

            var s = true;
            //if (!isExist)
            //{
            //    var password = "kokotnejapNy2=";
            //    var passphrase = "practice involve fish demise across edit wing practice excess unaware chimney tourist";

            //    // create the wallet
            //    var mnemonic = walletManager.CreateWallet(password, "mypoolcore.wallet", passphrase);
            //}
        }

        public static void GetTransaction(IFullNode fullNode)
        {
            var blockRPC = fullNode.NodeService<TransactionRPCController>();

            try
            {
                //var miningInfo = blockRPC.GetTransaction("a3cb00951a1c2b17042547b3da12723f3a02c6531317b3cf604972dcb327a5af");

            }
            catch (Exception)
            {
                //exist then nothing
            }

            var s = true;
            //if (!isExist)
            //{
            //    var password = "kokotnejapNy2=";
            //    var passphrase = "practice involve fish demise across edit wing practice excess unaware chimney tourist";

            //    // create the wallet
            //    var mnemonic = walletManager.CreateWallet(password, "mypoolcore.wallet", passphrase);
            //}
        }

        public static void WalletImport(IFullNode fullNode)
        {
            var walletRPC = fullNode.NodeService<WalletRPCController>();

            try
            {
                // var resultx2 = walletRPC.GetWallet("04fcf046-a32d-4790-ad36-7bc45ec0a252");
                //var resultx3 = walletRPC.GetNewAddress("04fcf046-a32d-4790-ad36-7bc45ec0a252");
                //walletRPC.ImportAddress("04fcf046-a32d-4790-ad36-7bc45ec0a252", "stupidity", false);
                // walletRPC.ImportAddress("04fcf046-a32d-4790-ad36-7bc45ec0a252", "TMHzbrb79JqM37dfXnN7WrfNUuA1fEiywS", false);
                // walletRPC.ImportAddress("04fcf046-a32d-4790-ad36-7bc45ec0a252", "TBAAqzTgt7Cf3no46J7NSuf7vSZF1jxjcC", false);
                //walletRPC.ImportPubKey("04fcf046-a32d-4790-ad36-7bc45ec0a252", "03defccb82cc300dd91338e04e36594305a62b3913f3a8fe0f3113de77d0a194d5", false);

            }
            catch (Exception)
            {
                //exist then nothing
            }

            var s = true;
        }

        public static void DecodeTest(IFullNode fullNode)
        {
            var  blockManager = fullNode.NodeService<BlockStoreManager>();

            try
            {
                // var rawTx = txRPC.DecodeRawTransaction("01000000010000000000000000000000000000000000000000000000000000000000000000ffffffff0402ea3200ffffffff0180b2e60e0000000023210231213ca97fed99f07b398cb5f28d2b949e64b9b08f1630498349bdec11d2b040ac00000000");

                //var txHash = new uint256("fdc2712c274509bb7e7cec1a7c403a95077b44573b68eab299f9201b59677e4f");
                var txHash = new uint256("1bf87d486d6709efea65cfe09e6d4922d9b208a6d8c61ec028500133433e2ff3");
                var currentTx = blockManager.BlockRepository.GetTrxAsync(txHash).GetAwaiter().GetResult();

                var xx = "01000000010000000000000000000000000000000000000000000000000000000000000000ffffffff0402ea3200ffffffff0180b2e60e0000000023210231213ca97fed99f07b398cb5f28d2b949e64b9b08f1630498349bdec11d2b040ac00000000";
                var yy = "01000000010000000000000000000000000000000000000000000000000000000000000000ffffffff0402ea3200ffffffff0180b2e60e0000000023210231213ca97fed99f07b398cb5f28d2b949e64b9b08f1630498349bdec11d2b040ac00000000";
                var txHex = currentTx.ToHex();
                var tx = Transaction.Load(txHex, fullNode.Network);

                var ssss = JsonConvert.DeserializeObject(tx.ToString(RawFormat.Satoshi, fullNode.Network));

                if (currentTx.Outputs != null)
                {
                    var i = 0;
                    foreach (var itemOutput in currentTx.Outputs)
                    {
                        // var txOut = new TxOut();
                        // txOut.FromBytes(itemOutput.ScriptPubKey.ToBytes(), NBitcoin.Protocol.ProtocolVersion.BTR_PROTOCOL_VERSION, fullNode.Network);

                        var destinations = new List<TxDestination>() { itemOutput.ScriptPubKey.GetDestination(fullNode.Network) };
                        var address = string.Empty;
                        if (destinations[0] == null)
                        {
                            destinations = itemOutput.ScriptPubKey.GetDestinationPublicKeys(fullNode.Network)
                                                                .Select(p => p.Hash)
                                                                .ToList<TxDestination>();
                        }
                        if (destinations.Count == 1)
                        {
                            address = destinations[0].GetAddress(fullNode.Network).ToString();
                        }
                        else
                        {
                            var multi = PayToMultiSigTemplate.Instance.ExtractScriptPubKeyParameters(fullNode.Network, itemOutput.ScriptPubKey);
                            if (multi != null)
                            {
                                if (multi.PubKeys != null && (multi.PubKeys.Count() > 0))
                                {
                                    address = multi.PubKeys[0].Hash.GetAddress(fullNode.Network).ToString();
                                }
                            }
                        }


                        //  var address = itemOutput.ScriptPubKey.GetDestinationAddress(fullNode.Network);
                        //if (address == null) address = itemOutput.ScriptPubKey.GetScriptAddress(fullNode.Network);

                        var test = itemOutput.ScriptPubKey.GetDestination(fullNode.Network);
                        var test2 = itemOutput.ScriptPubKey.GetScriptAddress(fullNode.Network);
                    }

                }

                // var resultx2 = walletRPC.GetWallet("04fcf046-a32d-4790-ad36-7bc45ec0a252");
                //var resultx3 = walletRPC.GetNewAddress("04fcf046-a32d-4790-ad36-7bc45ec0a252");
                //walletRPC.ImportAddress("04fcf046-a32d-4790-ad36-7bc45ec0a252", "stupidity", false);
                // walletRPC.ImportAddress("04fcf046-a32d-4790-ad36-7bc45ec0a252", "TMHzbrb79JqM37dfXnN7WrfNUuA1fEiywS", false);
                // walletRPC.ImportAddress("04fcf046-a32d-4790-ad36-7bc45ec0a252", "TBAAqzTgt7Cf3no46J7NSuf7vSZF1jxjcC", false);
                //walletRPC.ImportPubKey("04fcf046-a32d-4790-ad36-7bc45ec0a252", "03defccb82cc300dd91338e04e36594305a62b3913f3a8fe0f3113de77d0a194d5", false);

            }
            catch (Exception)
            {
                //exist then nothing
            }

            var s = true;
        }
		
		public static void TestSend(IFullNode fullNode)
        {
            var walletRPC = fullNode.NodeService<WalletRPCController>();

            try
            {
                walletRPC.SendMoney("account 0", "testname", "TBAAqzTgt7Cf3no46J7NSuf7vSZF1jxjcC", "Tq5=8T@5L4z,FF*vkc", 1);
                // var resultx2 = walletRPC.GetWallet("04fcf046-a32d-4790-ad36-7bc45ec0a252");
                //var resultx3 = walletRPC.GetNewAddress("04fcf046-a32d-4790-ad36-7bc45ec0a252");
                //walletRPC.ImportAddress("04fcf046-a32d-4790-ad36-7bc45ec0a252", "stupidity", false);
                // walletRPC.ImportAddress("04fcf046-a32d-4790-ad36-7bc45ec0a252", "TMHzbrb79JqM37dfXnN7WrfNUuA1fEiywS", false);
                // walletRPC.ImportAddress("04fcf046-a32d-4790-ad36-7bc45ec0a252", "TBAAqzTgt7Cf3no46J7NSuf7vSZF1jxjcC", false);
                //walletRPC.ImportPubKey("04fcf046-a32d-4790-ad36-7bc45ec0a252", "03defccb82cc300dd91338e04e36594305a62b3913f3a8fe0f3113de77d0a194d5", false);

            }
            catch (Exception)
            {
                //exist then nothing
            }

            var s = true;
        }

        public static void TestTxGeneration(IFullNode fullNode)
        {
            var transactionRPC = fullNode.NodeService<TransactionRPCController>();
            var walletRPC = fullNode.NodeService<WalletRPCController>();

            try
            {
                //transactionRPC.CreateRawTransaction()
                var resultx2 = walletRPC.GetWallet("testname");
                var getWallet = JsonConvert.SerializeObject(resultx2);

                var unspend = walletRPC.ListUnspent("testname");

                var txDetail = transactionRPC.GetRawTransaction("c7e196d25674d5b45f309d5c46a6d3440771813c66c0fcd840029ae175562aa4", 1, "");
                var getDetail = JsonConvert.SerializeObject(txDetail);
                
                var txIn = new Vin();
                txIn.TxId = "c7e196d25674d5b45f309d5c46a6d3440771813c66c0fcd840029ae175562aa4";
                txIn.VOut = 0;
                var inArray = new List<Vin>();
                inArray.Add(txIn);
                var inJson = JsonConvert.SerializeObject(inArray);

                var txOut = new Dictionary<string, decimal>();
                txOut.Add("TYJwz4AvZ51nMW21M7ijpGeBTbTzfdnEDs", (decimal)0.001);
                var outJson = JsonConvert.SerializeObject(txOut);

                var txCreated = (Microsoft.AspNetCore.Mvc.JsonResult)transactionRPC.CreateRawTransaction(inJson, outJson);
                var txValue = (BRhodium.Node.Utilities.JsonContract.ResultModel)txCreated.Value;

                var txSign = (Microsoft.AspNetCore.Mvc.JsonResult)transactionRPC.SignRawTransaction(txValue.Result.ToString(), null, null, "ALL", "testname/account 0", "Tq5=8T@5L4z,FF*vkc");
                var txSignValue = (BRhodium.Node.Utilities.JsonContract.ResultModel)txSign.Value;

                //  var txSend = transactionRPC.SendRawTransaction(txSignValue.Result.ToString());

                //var resultx2 = walletRPC.GetWallet("04fcf046-a32d-4790-ad36-7bc45ec0a252");
                //var resultx3 = walletRPC.GetNewAddress("04fcf046-a32d-4790-ad36-7bc45ec0a252");
                //walletRPC.ImportAddress("04fcf046-a32d-4790-ad36-7bc45ec0a252", "stupidity", false);
                // walletRPC.ImportAddress("04fcf046-a32d-4790-ad36-7bc45ec0a252", "TMHzbrb79JqM37dfXnN7WrfNUuA1fEiywS", false);
                // walletRPC.ImportAddress("04fcf046-a32d-4790-ad36-7bc45ec0a252", "TBAAqzTgt7Cf3no46J7NSuf7vSZF1jxjcC", false);
                //walletRPC.ImportPubKey("04fcf046-a32d-4790-ad36-7bc45ec0a252", "03defccb82cc300dd91338e04e36594305a62b3913f3a8fe0f3113de77d0a194d5", false);
            }
            catch (Exception e)
            {
                //exist then nothing
            }

            var s = true;
        }

        public static void SubmitBlock(IFullNode fullNode)
        {
            var blockRPC = fullNode.NodeService<MiningRPCController>();

            try
            {
                var hex = "0000002072e9bed352305cd7404f5428ead3a08b74466835164a75e1cd911a7a5dc2be88e775ea48ba87ae5991727e967b74999b1731298b6a33ad40789e65b5fb3c6de3d55fa55ecdad131a708e2d4c0501000000010000000000000000000000000000000000000000000000000000000000000000ffffffff2803df3001062f503253482f04d55fa55e08810002a8f404000062736f642e736f6c6f2e312e343300000000000180b2e60e000000001976a91488ac000000000100000001d9c221680482ab7a5f707c16dc80009f7ccbc1f1167feebe9d4216b508b84836010000006b483045022100f57d337d85c0a1f96527bae4634db884f7b0844c748fc7505c9620e6c7d4b72302207107e69afb34904e02404fb9e94501ab7c1dffa6bf542b41d4a2df5108af43df012102810ec0654f8a2fe9848138ce892a9191bb4addeba9ed14fd86d18d0ad36bbbd1ffffffff01aaa59a3b000000001976a914eee4533c0ab6929425a16ac7a8fa51d5a87b4f5288ac000000000100000004ac6dd558f9ef99aa8dcd7026a88a7764979ea807141c4f0c7570f385481be300010000006b483045022100864ff192a835746849816ccb01aa83dfbb678c0dbb61107a3c8de350cc00f2ac02202662553bcc7c4acac5defaa4ca8ae9862589621dff2491a62f9b0dbcf228a653012103ea13bef69d5c1d28972690d815bb3dd70366ab87a9e024e79945790ff625c77dffffffffa133170ed36c6405105313a9d027917356a4cf18ea6defb06e1fa722dcdab86b000000006b483045022100e960915f626233ca64b6486fab77afccfd2ec4907d3c3ba2bd9b847c7701636202207a5326657cc89481304b8b9f6a92398001e73ad6760ad657be6c7b979bd82b0501210391f2622569ab7cea997349889e444e2eea5a709f74143da9c219e7d34046da50fffffffff351d9c4117ed571c7614c35f0ed2a11a098148c58f5e997433b94b2968d2d84000000006a47304402202272dc44c3c16b213290ad160ede5a1bd61178254514c6d12e151d7fb54f91a902207037e7652fc0cd2120d17a5243a4ce7c58cda493ef0b38e5021cd4af218149990121033cd27e2af62f26b4ea0d4d8207c27f0d340f7c60b986f3338ed4ec1dfa2f6961ffffffff63f2e8796417fa182177cbc6315fd1626dfb5d63385941da356174d8a7b3e6e2010000006a4730440220694440ba859e0a28ab7bb241ee2bc96e0c58d61c53f5a98208bee1c2959d9ec0022043fc75ce5956834b55028b30cd08b3f3e5eebe705c5ee36ed0997241820b9921012103ea13bef69d5c1d28972690d815bb3dd70366ab87a9e024e79945790ff625c77dffffffff0284e69311000000001976a914b12ba087f8aa1f3956dfd78aa80bfe2d5a1da70b88ac80ba953e000000001976a914b2349ed03101e2f50a6f703b30f283a1b4ff603c88ac0000000001000000051ff33a96e96ce68ed218fc9d7f57da535c64fab7d6d1391d4a92524f38b12eb1000000006a4730440220741a23591d9ef777b0f4de247e39cb3e68b11554a9675760055858365731bfd302203e2fa3f8de1e3c33f50b4a30663c47a7fec96f7f34e7c9f9c440ab430f6e409a0121021fcae94dc70ca63760918cc733cd3406988d150682d307584566f036c707fb88ffffffffe735a29d7c948d39532e6fd07840f75f4208f2c473f164c24211a52aefb1af38000000006b483045022100bc9ee2b3c447641245558bfae8b415186176ce75e8b491b5852121350dc480f302204369eb62b0110a13336acddfeb24236b057137517c2a25557ffae27c72384d7b0121038ec21d3e7668939156600171339179a1eb53c3ce51b96a20ba4d540db5969fdcffffffff3d3ffb481bea48e1e9f042899adda2e937f38699cf4e502683777a7cb35564d6000000006b483045022100d4bb7fce9bde4a9712e1b72d2845305b5376c932720c1e6427fd6531aaacad6e022064ec7f42572a8aa1c5049ae57ce53fe8db228e909ed0c3a73e8916c78080a3ac0121038a4b48f235f95a53a7dd3b6ce85d085d84f49a27bad3b23397011c22ef5fef78ffffffff464e527224c94f484e353f89c197c1ec966fcad18bb4d105679f6cbac42849fe000000006b483045022100f1e04b1e0cf37a0333743de2c7b0120c077fd326dc686eff32fe03db46d02998022078f339f5ade06d77a6c86d285b491ca6035fd96cec288fe90a39fbf55122e378012103a5d27388c3ddd20df39c43167c0902c6433115767127340a389316f1c9496d76ffffffffd67295b01aec6b8f6becf2a70acf128c751ed900de6b821cdc4ef82fb0cb82d5000000006a47304402201873a0822593d4a16508b005ee6f21904fb421ba7619433bdae633c1c115749b022015e16833ba1cdf2f508e03b7abe038cc68f16596cabc4b57085e0863d2fb3c99012103bde80ec160be8361523b5d10404b5fd762d2a01ccd22e888800b65cb1d422b08ffffffff02dbeb247b000000001976a914bdf3c94762617e8740936df9a1d0906e84db435688ac400b2ee5000000001976a914ab6876a14e7c76daf56c8d526e6e637b11c4faea88ac000000000100000003d67295b01aec6b8f6becf2a70acf128c751ed900de6b821cdc4ef82fb0cb82d5010000006b4830450221008066afac3a6d7ec7d49388ce8c3960b9012af5c0fea1c87493eae8806d1b86d602203a206cd1dbe09b7b946df17e2a0601582cf361e947543510f8aac1279ab9ab70012103f6e3606b73adc6e7bee4c0250b563459f2b25448df07d47602a7d774674f268dffffffff501953f6680e01e331dd19f02b052a7941e054a7d88d85d25eb63ad3e808bcc6010000006b483045022100a23c0f836a8cabb74d7fa5614c90f901de07798240f68cc5fc9175525ab603ee022002b8e301f06cd8f1400569c4a7a5d98e193182d51aa88e6fee15c0cac36a9879012102b6ffbe03c826c45fb1631e6dc62d638e870d0464aeb42e8095a21ff2bc24637dfffffffff6336983ac80f39234c554f4b81d3f3e4d18f263789db13832df6d1a2a225065000000006a47304402201c77cf4f0d5ba32ce178cdeeba6a37acac9fb71ec016853bd58cce29cf3a746402207952da5a92eb31a9b73196af4d8174f85e573fb569c933d936496b1ba163f36a0121031858d8ff87803d0ecfd9346eb64a97779ee01cff45711f21f1320c16d6444910ffffffff02e887cc71020000001976a91460234ff91f7f1500ee0da9a026c341c69952e5ce88ac40f41fed090000001976a9147b7d2a2b30342c63c19d161099b2379a4626b94988ac00000000";

                blockRPC.SubmitBlock(hex);

                var sxxx = true;
            }
            catch (Exception e )
            {
                var sccc = true;
                //exist then nothing
            }

            var s = true;
            //if (!isExist)
            //{
            //    var password = "kokotnejapNy2=";
            //    var passphrase = "practice involve fish demise across edit wing practice excess unaware chimney tourist";

            //    // create the wallet
            //    var mnemonic = walletManager.CreateWallet(password, "mypoolcore.wallet", passphrase);
            //}
        }
    }
}
