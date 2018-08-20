using System;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using BRhodium.Node;
using BRhodium.Node.Builder;
using BRhodium.Node.Configuration;
using BRhodium.Bitcoin.Features.Api;
using BRhodium.Bitcoin.Features.BlockStore;
using BRhodium.Bitcoin.Features.Consensus;
using BRhodium.Bitcoin.Features.Dns;
using BRhodium.Bitcoin.Features.MemoryPool;
using BRhodium.Bitcoin.Features.Miner;
using BRhodium.Bitcoin.Features.RPC;
using BRhodium.Bitcoin.Features.Wallet;
using BRhodium.Node.Utilities;

namespace BRhodium.Dns
{
    /// <summary>
    /// Main entry point.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// The entry point for the BRhodium Dns process.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public static void Main(string[] args)
        {
            MainAsync(args).Wait();
        }

        /// <summary>
        /// The async entry point for the BRhodium Dns process.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>A task used to await the operation.</returns>
        public static async Task MainAsync(string[] args)
        {
            try
            {
                NodeSettings nodeSettings = new NodeSettings(protocolVersion:ProtocolVersion.BTR_PROTOCOL_VERSION, args:args, loadConfiguration:true);

                Action<DnsSettings> serviceTest = (s) =>
                {
                    if (string.IsNullOrWhiteSpace(s.DnsHostName) || string.IsNullOrWhiteSpace(s.DnsNameServer) || string.IsNullOrWhiteSpace(s.DnsMailBox))
                        throw new ConfigurationException("When running as a DNS Seed service, the -dnshostname, -dnsnameserver and -dnsmailbox arguments must be specified on the command line.");
                };

                // Run as a full node with DNS or just a DNS service?
                IFullNode node;
                if (nodeSettings.ConfigReader.GetOrDefault<bool>("dnsfullnode", false))
                {
                    // Build the Dns full node.
                    node = new FullNodeBuilder()
                        .UseNodeSettings(nodeSettings)
                        .UsePowConsensus()
                        .UseBlockStore()
                        .UseMempool()
                        .UseWallet()
                        .AddPowMining()
                        .UseApi()
                        .AddRPC()
                        .UseDns(serviceTest)
                        .Build();
                }
                else
                {
                    // Build the Dns node.
                    node = new FullNodeBuilder()
                        .UseNodeSettings(nodeSettings)
                        .UsePowConsensus()
                        .UseApi()
                        .AddRPC()
                        .UseDns(serviceTest)
                        .Build();
                }

                // Run node.
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
