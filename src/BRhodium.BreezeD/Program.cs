using System;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using BRhodium.Bitcoin;
using BRhodium.Bitcoin.Builder;
using BRhodium.Bitcoin.Configuration;
using BRhodium.Bitcoin.Features.Api;
using BRhodium.Bitcoin.Features.LightWallet;
using BRhodium.Bitcoin.Features.Notifications;
using BRhodium.Bitcoin.Utilities;

namespace BRhodium.BreezeD
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
                // Get the API uri.
                var isTestNet = args.Contains("-testnet");
                var isBRhodium = args.Contains("BRhodium");

                var agent = "Breeze";

                NodeSettings nodeSettings;

                if (isBRhodium)
                {
                    Network network = isTestNet ? Network.BRhodiumTest : Network.BRhodiumMain;
                    if (isTestNet)
                        args = args.Append("-addnode=51.141.28.47").ToArray(); // TODO: fix this temp hack

                    nodeSettings = new NodeSettings(network, ProtocolVersion.ALT_PROTOCOL_VERSION, agent, args:args, loadConfiguration:false);
                }
                else
                {
                    nodeSettings = new NodeSettings(agent: agent, args: args, loadConfiguration:false);
                }

                IFullNodeBuilder fullNodeBuilder = new FullNodeBuilder()
                    .UseNodeSettings(nodeSettings)
                    .UseLightWallet()
                    .UseBlockNotification()
                    .UseTransactionNotification()
                    .UseApi();

                IFullNode node = fullNodeBuilder.Build();

                // Start Full Node - this will also start the API.
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
