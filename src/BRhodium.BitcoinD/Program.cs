using System;
using System.Threading.Tasks;
using NBitcoin;
using BRhodium.Bitcoin;
using BRhodium.Bitcoin.Builder;
using BRhodium.Bitcoin.Configuration;
using BRhodium.Bitcoin.Features.BlockStore;
using BRhodium.Bitcoin.Features.Consensus;
using BRhodium.Bitcoin.Features.MemoryPool;
using BRhodium.Bitcoin.Features.Miner;
using BRhodium.Bitcoin.Features.Miner.Interfaces;
using BRhodium.Bitcoin.Features.RPC;
using BRhodium.Bitcoin.Features.Wallet;
using BRhodium.Bitcoin.Features.Wallet.Interfaces;
using BRhodium.Bitcoin.Utilities;
using System.Linq;

namespace BRhodium.BitcoinD
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
                //for testing
                //args = new string[] { "-regtest" };
                args = new string[] { "-testnet" };
                NodeSettings nodeSettings = new NodeSettings(args:args, loadConfiguration:false);

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
                //Task.Delay(TimeSpan.FromSeconds(15)).ContinueWith((t) => { CreatePrimaryWallet(node); }).GetAwaiter();

                //BTR TEST MINER
                //Task.Delay(TimeSpan.FromSeconds(30)).ContinueWith((t) => { TryStartMiner(args, node); }).GetAwaiter();

                if (node != null)
                    await node.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was a problem initializing the node. Details: '{0}'", ex.Message);
            }
        }

        //BTR X13 Miner
        private static void TryStartMiner(string[] args, IFullNode fullNode)
        {
            var walletManager = fullNode.NodeService<IWalletManager>() as WalletManager;
            var wallet = walletManager.GetWalletByName("rhodium.genesis");
            
            var hdaccount = wallet.GetAccountsByCoinType(CoinType.BRhodium).ToArray().First();
            var address = hdaccount.ExternalAddresses.First();

            var pubkey = BitcoinAddress.Create(address.Address, fullNode.Network);
            fullNode.NodeService<IPowMining>().Mine(pubkey.ScriptPubKey);
        }

        //BTR Wallet TEST
        public static void CreatePrimaryWallet(IFullNode fullNode)
        {
            var walletManager = fullNode.NodeService<IWalletManager>() as WalletManager;

            var isExist = false;

            try
            {
                var wallet = walletManager.GetWalletByName("rhodium.genesis");
                isExist = true;
            }
            catch (Exception)
            {
                //exist then nothing
            }

            if (!isExist)
            {
                var password = "thisisourrootwallet";
                var passphrase = "this is my magic passphrase this is my magic passphrase this is my magic passphrase";

                // create the wallet
                var mnemonic = walletManager.CreateWallet(password, "rhodium.genesis", passphrase);
            }
        }
    }
}
