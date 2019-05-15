using System;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using BRhodium.Node.Base;
using BRhodium.Node.Builder;
using BRhodium.Node.Configuration;
using BRhodium.Node.Connection;
using BRhodium.Bitcoin.Features.BlockStore;
using BRhodium.Bitcoin.Features.Consensus;
using BRhodium.Bitcoin.Features.Consensus.Interfaces;
using BRhodium.Bitcoin.Features.Consensus.Rules;
using Xunit;
using BRhodium.Node;

namespace BRhodium.Bitcoin.Features.MemoryPool.Tests
{
    public class FullNodeBuilderTest
    {
        [Fact]
        public void CanHaveAllFullnodeServicesTest()
        {
            // This test is put in the mempool feature because the
            // mempool requires all the features to be a fullnode


            NodeSettings nodeSettings = new NodeSettings(args: new string[] {
                $"-datadir=BRhodium.Bitcoin.Features.MemoryPool.Tests/TestData/FullNodeBuilderTest/CanHaveAllServicesTest" });
            var fullNodeBuilder = new FullNodeBuilder(nodeSettings);
            IFullNode fullNode = fullNodeBuilder
                .UsePowConsensus()
                .UseBlockStore()
                .UseMempool()
                .Build();

            IServiceProvider serviceProvider = fullNode.Services.ServiceProvider;
            var network = serviceProvider.GetService<Network>();
            var settings = serviceProvider.GetService<NodeSettings>();
            var consensusLoop = serviceProvider.GetService<IConsensusLoop>() as ConsensusLoop;
            var chain = serviceProvider.GetService<NBitcoin.ConcurrentChain>();
            var chainState = serviceProvider.GetService<IChainState>() as ChainState;
            var blockStoreManager = serviceProvider.GetService<BlockStoreManager>();
            var consensusRules = serviceProvider.GetService<IConsensusRules>();
            consensusRules.Register(serviceProvider.GetService<IRuleRegistration>());
            var mempoolManager = serviceProvider.GetService<MempoolManager>();
            var connectionManager = serviceProvider.GetService<IConnectionManager>() as ConnectionManager;

            Assert.NotNull(fullNode);
            Assert.NotNull(network);
            Assert.NotNull(settings);
            Assert.NotNull(consensusLoop);
            Assert.NotNull(chain);
            Assert.NotNull(chainState);
            Assert.NotNull(blockStoreManager);
            Assert.NotNull(mempoolManager);
            Assert.NotNull(connectionManager);
        }
    }
}
