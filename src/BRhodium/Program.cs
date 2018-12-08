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
using BRhodium.Bitcoin.Features.Wallet.Controllers;

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
                args = new string[] { "-testnet" };
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

                //Task.Delay(TimeSpan.FromSeconds(10)).ContinueWith((t) => { RestoreFromSeed(args, node); }).GetAwaiter();
                //Task.Delay(TimeSpan.FromSeconds(15)).ContinueWith((t) => { Rescan(args, node); }).GetAwaiter();

                if (node != null)
                    await node.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was a problem initializing the node. Details: '{0}'", ex.Message);
            }
        }

        private static void RestoreFromSeed(string[] args, IFullNode fullNode)
        {
            var walletRPC = fullNode.NodeService<WalletRPCController>();
            var wallet = walletRPC.Restore("P8/1pX?SU.4mb@5z", "TestnetPoolWallet4-2", "unlock short secret right grief spawn twelve urge glass state issue tape", 1539721963);
        }

        private static void Rescan(string[] args, IFullNode fullNode)
        {
            var walletRPC = fullNode.NodeService<WalletRPCController>();
            var rescan = walletRPC.RescanBlockChain(1, 103);
        }
    }
}
