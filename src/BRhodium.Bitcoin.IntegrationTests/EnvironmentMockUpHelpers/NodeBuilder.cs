using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using NBitcoin;
using BRhodium.Bitcoin.Features.BlockStore;
using BRhodium.Bitcoin.Features.Consensus;
using BRhodium.Bitcoin.Features.Consensus.CoinViews;
using BRhodium.Bitcoin.Features.Consensus.Interfaces;
using BRhodium.Bitcoin.Features.MemoryPool;
using BRhodium.Bitcoin.Features.Wallet;
using BRhodium.Bitcoin.Features.Wallet.Interfaces;

namespace BRhodium.Node.IntegrationTests.EnvironmentMockUpHelpers
{
    internal static class FullNodeExt
    {
        public static WalletManager WalletManager(this FullNode fullNode)
        {
            return fullNode.NodeService<IWalletManager>() as WalletManager;
        }

        public static WalletTransactionHandler WalletTransactionHandler(this FullNode fullNode)
        {
            return fullNode.NodeService<IWalletTransactionHandler>() as WalletTransactionHandler;
        }

        public static ConsensusLoop ConsensusLoop(this FullNode fullNode)
        {
            return fullNode.NodeService<IConsensusLoop>() as ConsensusLoop;
        }

        public static CoinView CoinView(this FullNode fullNode)
        {
            return fullNode.NodeService<CoinView>();
        }

        public static MempoolManager MempoolManager(this FullNode fullNode)
        {
            return fullNode.NodeService<MempoolManager>();
        }

        public static BlockStoreManager BlockStoreManager(this FullNode fullNode)
        {
            return fullNode.NodeService<BlockStoreManager>();
        }

        public static ChainedHeader HighestPersistedBlock(this FullNode fullNode)
        {
            return fullNode.NodeService<IBlockRepository>().HighestPersistedBlock;
        }
    }

    public enum CoreNodeState
    {
        Stopped,
        Starting,
        Running,
        Killed
    }

    public class NodeConfigParameters : Dictionary<string, string>
    {
        public void Import(NodeConfigParameters configParameters)
        {
            foreach (var kv in configParameters)
            {
                if (!this.ContainsKey(kv.Key))
                    this.Add(kv.Key, kv.Value);
            }
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            foreach (var kv in this)
                builder.AppendLine(kv.Key + "=" + kv.Value);
            return builder.ToString();
        }
    }

    public class NodeBuilder : IDisposable
    {
        private Network network = Network.BRhodiumRegTest;
        public List<CoreNode> Nodes { get; }

        public NodeConfigParameters ConfigParameters { get; }

        private int lastDataFolderIndex;

        private string rootFolder;

        public NodeBuilder(string rootFolder)
        {
            this.lastDataFolderIndex = 0;
            this.Nodes = new List<CoreNode>();
            this.ConfigParameters = new NodeConfigParameters();
            this.rootFolder = rootFolder;
        }

        public static NodeBuilder Create([CallerMemberName] string caller = null, string version = "0.13.1")
        {
            string directoryPath = caller = Path.Combine("TestData", caller);
            if (directoryPath.Length > 80)//windows has 260 chars path limit
            {
                directoryPath =  Path.Combine("TestData", caller.GetHashCode().ToString("X2")); 
            }
            string path = CreateTestFolder(directoryPath);
            return new NodeBuilder(path);
        }

        private CoreNode CreateNode(NodeRunner runner, Network network, bool start, string configFile = "BRhodium.conf")
        {
            var node = new CoreNode(runner, this, network, configFile);
            this.Nodes.Add(node);
            if (start) node.Start();
            return node;
        }

        public CoreNode CreateBRhodiumPowNode(bool start = false)
        {
            return CreateNode(new BRhodiumNodePowRunner(this.GetNextDataFolderName()), network, start);
        }

        public CoreNode CreateBRhodiumPowMiningNode(bool start = false)
        {
            return CreateNode(new BRhodiumProofOfWorkMiningNode(this.GetNextDataFolderName()), network, start, "BRhodium.conf");
        }

        public CoreNode CloneBRhodiumNode(CoreNode cloneNode)
        {
            var node = new CoreNode(new BRhodiumNodePowRunner(cloneNode.FullNode.Settings.DataFolder.RootPath), this, network, "BRhodium.conf");
            this.Nodes.Add(node);
            this.Nodes.Remove(cloneNode);
            return node;
        }

        private string GetNextDataFolderName()
        {
            var dataFolderName = Path.Combine(this.rootFolder, this.lastDataFolderIndex.ToString());
            this.lastDataFolderIndex++;
            return dataFolderName;
        }

        public void StartAll()
        {
            foreach (var node in this.Nodes.Where(n => n.State == CoreNodeState.Stopped))
            {
                node.Start();
            }
        }

        public void Dispose()
        {
            foreach (var node in this.Nodes)
                node.Kill();
        }

        internal static string CreateTestFolder(string folderName)
        {
            if (Directory.Exists(folderName))
            {
                try
                {
                    Directory.Delete(folderName, true);
                }
                catch
                {
                    folderName = folderName + "_" + DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
                    Directory.CreateDirectory(folderName);
                    return folderName;
                }
            }

            Directory.CreateDirectory(folderName);
            return folderName;
        }

        internal static void CreateDataFolder(string dataFolder)
        {
            if (!Directory.Exists(dataFolder))
                Directory.CreateDirectory(dataFolder);
        }
    }
}