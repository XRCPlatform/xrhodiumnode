using System.Diagnostics;
using System.IO;
using NBitcoin;
using NBitcoin.Protocol;
using BRhodium.Node.Builder;
using BRhodium.Node.Configuration;
using BRhodium.Bitcoin.Features.Api;
using BRhodium.Bitcoin.Features.BlockStore;
using BRhodium.Bitcoin.Features.Consensus;
using BRhodium.Bitcoin.Features.MemoryPool;
using BRhodium.Bitcoin.Features.Miner;
using BRhodium.Bitcoin.Features.RPC;
using BRhodium.Bitcoin.Features.Wallet;

namespace BRhodium.Node.IntegrationTests.EnvironmentMockUpHelpers
{
    public class BitcoinCoreRunner : NodeRunner
    {
        private string bitcoinD;

        public BitcoinCoreRunner(string dataDir, string bitcoinD)
            : base(dataDir)
        {
            this.bitcoinD = bitcoinD;
        }

        private Process process;

        public new bool IsDisposed
        {
            get { return this.process == null && this.process.HasExited; }
        }

        public new void Kill()
        {
            if (!this.IsDisposed)
            {
                this.process.Kill();
                this.process.WaitForExit();
            }
        }

        public override void OnStart()
        {
            this.process = Process.Start(new FileInfo(this.bitcoinD).FullName, $"-conf=BRhodium.conf -datadir={this.DataFolder} -debug=net");
        }

        public override void BuildNode()
        {
        }
    }

    public abstract class NodeRunner
    {
        public readonly string DataFolder;
        public bool IsDisposed => this.FullNode.State == FullNodeState.Disposed;
        public FullNode FullNode { get; set; }

        protected NodeRunner(string dataDir)
        {
            this.DataFolder = dataDir;
        }

        public abstract void BuildNode();
        public abstract void OnStart();

        public void Kill()
        {
            this.FullNode?.Dispose();
        }

        public void Start()
        {
            BuildNode();
            OnStart();
        }
    }

    public sealed class BRhodiumNodePowRunner : NodeRunner
    {
        public BRhodiumNodePowRunner(string dataDir)
            : base(dataDir)
        {
        }

        public override void BuildNode()
        {
            var settings = new NodeSettings(args: new string[] { "-conf=BRhodium.conf", "-datadir=" + this.DataFolder }, loadConfiguration: false);

            this.FullNode = (FullNode)new FullNodeBuilder()
                            .UseNodeSettings(settings)
                            .UsePowConsensus()
                            .UseBlockStore()
                            .UseMempool()
                            .AddMining()
                            .UseWallet()
                            .AddRPC()
                            .MockIBD()
                            .Build();
        }

        public override void OnStart()
        {
            this.FullNode.Start();
        }
    }

    public sealed class BRhodiumProofOfWorkMiningNode : NodeRunner
    {
        public BRhodiumProofOfWorkMiningNode(string dataDir)
            : base(dataDir)
        {
        }

        public override void BuildNode()
        {
            var settings = new NodeSettings(Network.BRhodiumRegTest, ProtocolVersion.XRC_PROTOCOL_VERSION, args: new string[] { "-conf=BRhodium.conf", "-datadir=" + this.DataFolder }, loadConfiguration: false);

            this.FullNode = (FullNode)new FullNodeBuilder()
                            .UseNodeSettings(settings)
                            .UsePowConsensus()
                            .UseBlockStore()
                            .UseMempool()
                            .AddMining()
                            .UseWallet()
                            .AddRPC()
                            .MockIBD()
                            .SubstituteDateTimeProviderFor<MiningFeature>()
                            .Build();
        }

        public override void OnStart()
        {
            this.FullNode.Start();
        }
    }
}