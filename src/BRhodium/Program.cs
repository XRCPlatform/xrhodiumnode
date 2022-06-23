using System;
using System.Threading.Tasks;
using BRhodium.Bitcoin.Features.BlockStore;
using BRhodium.Bitcoin.Features.Consensus;
using BRhodium.Bitcoin.Features.MemoryPool;
using BRhodium.Bitcoin.Features.Miner;
using BRhodium.Bitcoin.Features.RPC;
using BRhodium.Bitcoin.Features.Wallet;
using BRhodium.Node.Builder;
using BRhodium.Node.Configuration;
using BRhodium.Node.Utilities;

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
                //args = new string[] { "-testnet" };
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

                if (node != null)
                    await node.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was a problem initializing the node. Details: '{0}'", ex.Message);
            }
        }
    }
}
