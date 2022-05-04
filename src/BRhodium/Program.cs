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
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text;
using System.IO;
using BRhodium.Node.Utilities.Extensions;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;

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

                //Task.Delay(TimeSpan.FromSeconds(15)).ContinueWith((t) => { DigishieldAlgoTest(node); }).GetAwaiter();
                //Task.Delay(TimeSpan.FromSeconds(15)).ContinueWith((t) => { GetBlock(node); }).GetAwaiter();
                // Task.Delay(TimeSpan.FromSeconds(15)).ContinueWith((t) => { HashTest(); }).GetAwaiter();
                Task.Delay(TimeSpan.FromSeconds(15)).ContinueWith((t) => { GetBlockPOWTEST(node); }).GetAwaiter();

                if (node != null)
                    await node.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was a problem initializing the node. Details: '{0}'", ex.Message);
            }
        }

        public static void GetBlock(IFullNode fullNode)
        {
            var chainRepository = fullNode.NodeService<ConcurrentChain>();

            var list = new List<Tuple<double, double>>();
            try
            {
                var block = chainRepository.GetBlock(16290);

                //after your loop
                // File.WriteAllText("mainnet.csv", csv.ToString());

            }
            catch (Exception e)
            {
                var ss = true;
                //exist then nothing
            }

            var s = true;
        }

        public static void DigishieldAlgoTest(IFullNode fullNode)
        {
            var blockManager = fullNode.NodeService<BlockStoreManager>();
            var chainRepository = fullNode.NodeService<ConcurrentChain>();

            var list = new List<Tuple<double, double>>();
            try
            {
                var lastBlockHeight = blockManager.BlockRepository.HighestPersistedBlock.Height;
                var testStartBlockHeight = lastBlockHeight - 1;

                //before your loop
                var csv = new StringBuilder();
                var newLine = string.Format("{0}; {1}; {2}; {3} ; {4} ", "", "GetWorkRequired", "GetWorkRequiredV2", "GetNextWorkRequired", "GetNextWorkRequiredV2");
                csv.AppendLine(newLine);

                Target lastBlockTarget = null;

                for (int i = testStartBlockHeight; i <= lastBlockHeight; i++)
                {

                    var block = chainRepository.GetBlock(i);

                    var actual = block.GetWorkRequired(fullNode.Network.Consensus);
                    var actualV2 = actual;
                    var next = block.GetNextWorkRequired(fullNode.Network.Consensus);
                    var next2 = next;

                    if (i > 50) {
                        Console.WriteLine(i);
                        if (i == 3533)
                        {
                            var sss = true;
                        }

                        if (lastBlockTarget == null) lastBlockTarget = next2;
                    }


                    list.Add(new Tuple<double, double>(actual.DifficultySafe(), actualV2.DifficultySafe()));

                    newLine = string.Format("{0}; {1}; {2}; {3} ; {4} ", i,
                        actual.DifficultySafe().ToString(), actualV2.DifficultySafe().ToString(),
                        next.DifficultySafe().ToString(), next2.DifficultySafe().ToString());
                    csv.AppendLine(newLine);

                }

                //after your loop
                // File.WriteAllText("mainnet.csv", csv.ToString());

            }
            catch (Exception e)
            {
                var ss = true;
                //exist then nothing
            }

            var s = true;
        }

        public static void GenerateDifficultList(IFullNode fullNode)
        {
            var blockManager = fullNode.NodeService<BlockStoreManager>();
            var chainRepository = fullNode.NodeService<ConcurrentChain>();

            try
            {
                var lastBlockHeight = blockManager.BlockRepository.HighestPersistedBlock.Height;
                var testStartBlockHeight = 0;

                //before your loop
                var csv = new StringBuilder();

                Target lastBlockTarget = null;
                var type = "X13 Bitcoin Algo";

                DateTimeOffset oldSpaceDateTime = new DateTimeOffset();

                for (int i = testStartBlockHeight; i <= lastBlockHeight; i++)
                {
                    var spaceSec = 0;
                    var block = chainRepository.GetBlock(i);
                    var actualDiff = block.GetWorkRequired(fullNode.Network.Consensus);
                    if (oldSpaceDateTime != null)
                    {
                        var timeSpan = block.Header.BlockTime - oldSpaceDateTime;
                        spaceSec = (int)timeSpan.TotalSeconds;
                    }

                    if (i > 16670)
                    {
                        type = "X11 DigiShield Algo  - timespan 10min";
                    }
                    else if (i > 16393)
                    {
                        type = "X13 DigiShield Algo - timespan 10min";
                    }
                    else if (i > 16290)
                    {
                        type = "X13 DigiShield Algo - timespan 1,25min";
                    }

                    var newLine = string.Format("{0}; {1}; {2}; {3}; {4};", i,
                        actualDiff.DifficultySafe().ToString(), type,
                        block.Header.BlockTime.UtcDateTime.ToString(), spaceSec);
                    csv.AppendLine(newLine);

                    oldSpaceDateTime = block.Header.BlockTime;
                }

                //after your loop
                File.WriteAllText("AlgoReport.csv", csv.ToString());

            }
            catch (Exception e)
            {
                var ss = true;
                //exist then nothing
            }

            var s = true;
        }

        public static void HashTest()
        {
            var testA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var testB = "00000020d897b48153761f6bdd30a8200cea8957a365519aee57658c4c4f993d94c957948c62f61f4fe20cc3497da731b9882cb7e13a4edfb271eb61c715c1c09608fde17645a25c5438251a4b818bb7";
            byte[] decodedBytesA = Encoding.UTF8.GetBytes(testA);
            byte[] decodedBytesB = Encoding.UTF8.GetBytes(testB);

            Console.WriteLine("X13");
            // var resultX13Bytes = HashX13.Instance.Hash(decodedBytesA, 2);
            var resultX13BytesB = HashX13.Instance.Hash(decodedBytesB, 2);
            Console.WriteLine("X11");
            //var resultX11Bytes = HashX11.Instance.Hash(decodedBytesA);
            //var resultX11BytesB = HashX11.Instance.Hash(decodedBytesB);

        }

        public static void GetBlockPOWTEST(IFullNode fullNode)
        {
            var chainRepository = fullNode.NodeService<ConcurrentChain>();

            try
            {
                var csv = new StringBuilder();

                var block = chainRepository.GetBlock(136234);
                var blockPrev = chainRepository.GetBlock(block.Header.HashPrevBlock);
                var bytestForHash = block.Header.ToBytes(NBitcoin.Protocol.ProtocolVersion.XRC_PROTOCOL_VERSION, fullNode.Network);
                var resultX11BytesB = HashX11.Instance.Hash(bytestForHash);
                //var resultX13BytesB = HashX13.Instance.Hash(bytestForHash, 2);

                var targetPrev = block.Header.GetWorkRequired(fullNode.Network, blockPrev);
                //var target = block.Header.GetWorkRequired(fullNode.Network, block);
                var powHash = block.Header.GetPoWHash(136234,
                    fullNode.Network.Consensus.PowLimit2Height,
                    fullNode.Network.Consensus.PowDigiShieldX11Height);

                var actualBits = block.Header.Bits.ToUInt256();

            }
            catch (Exception e)
            {
                var ss = true;
                //exist then nothing
            }


            var s = true;
        }
    }
}
