using System;
using System.Collections.Generic;
using System.Linq;
using BRhodium.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;

namespace BRhodium.Bitcoin.IntegrationTests.Builders
{
    public class NodeGroupBuilder : IDisposable
    {
        private readonly NodeBuilder nodeBuilder;
        private readonly Dictionary<string, CoreNode> nodes;

        public NodeGroupBuilder(string testFolder)
        {
            this.nodeBuilder = NodeBuilder.Create(caller: testFolder);
            this.nodes = new Dictionary<string, CoreNode>();
        }

        public void Dispose()
        {
            this.nodeBuilder.Dispose();
        }

        public IDictionary<string, CoreNode> Build()
        {
            return this.nodes;
        }

        public NodeGroupBuilder BRhodiumPowNode(string nodeName)
        {
            this.nodes.Add(nodeName, this.nodeBuilder.CreateBRhodiumPowNode());
            return this;
        }

        public NodeGroupBuilder CreateBRhodiumPowMiningNode(string nodeName)
        {
            this.nodes.Add(nodeName, this.nodeBuilder.CreateBRhodiumPowMiningNode());
            return this;
        }

        public NodeGroupBuilder CreateBRhodiumPosNode(string nodeName)
        {
            this.nodes.Add(nodeName, this.nodeBuilder.CreateBRhodiumPosNode());
            return this;
        }

        public NodeGroupBuilder CreateBRhodiumPosApiNode(string nodeName)
        {
            this.nodes.Add(nodeName, this.nodeBuilder.CreateBRhodiumPosApiNode());
            return this;
        }

        public NodeGroupBuilder NotInIBD()
        {
            this.nodes.Last().Value.NotInIBD();
            return this;
        }

        public NodeGroupBuilder WithWallet(string walletName, string walletPassword)
        {
            this.nodes.Last().Value.FullNode.WalletManager().CreateWallet(walletPassword, walletName);
            return this;
        }

        public NodeGroupBuilder Start()
        {
            this.nodes.Last().Value.Start();
            return this;
        }

        public NodeConnectionBuilder WithConnections()
        {
            return new NodeConnectionBuilder(this).With(this.nodes);
        }
    }
}