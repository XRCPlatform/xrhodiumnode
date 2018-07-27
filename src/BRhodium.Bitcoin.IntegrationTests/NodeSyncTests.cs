using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using BRhodium.Node.Connection;
using BRhodium.Node.IntegrationTests.EnvironmentMockUpHelpers;
using Xunit;

namespace BRhodium.Node.IntegrationTests
{
    public class NodeSyncTests
    {
        [Fact]
        public void NodesCanConnectToEachOthers()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var node1 = builder.CreateBRhodiumPowNode();
                var node2 = builder.CreateBRhodiumPowNode();
                builder.StartAll();
                Assert.Empty(node1.FullNode.ConnectionManager.ConnectedPeers);
                Assert.Empty(node2.FullNode.ConnectionManager.ConnectedPeers);
                var rpc1 = node1.CreateRPCClient();
                rpc1.AddNode(node2.Endpoint, true);
                Assert.Single(node1.FullNode.ConnectionManager.ConnectedPeers);
                Assert.Single(node2.FullNode.ConnectionManager.ConnectedPeers);

                var behavior = node1.FullNode.ConnectionManager.ConnectedPeers.First().Behaviors.Find<ConnectionManagerBehavior>();
                Assert.False(behavior.Inbound);
                Assert.True(behavior.OneTry);
                behavior = node2.FullNode.ConnectionManager.ConnectedPeers.First().Behaviors.Find<ConnectionManagerBehavior>();
                Assert.True(behavior.Inbound);
                Assert.False(behavior.OneTry);
            }
        }

        [Fact]
        public void CanBRhodiumSyncFromCore()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var BRhodiumNode = builder.CreateBRhodiumPowNode();
                var coreNode = builder.CreateBitcoinCoreNode();
                builder.StartAll();

                BRhodiumNode.NotInIBD();

                var tip = coreNode.FindBlock(10).Last();
                BRhodiumNode.CreateRPCClient().AddNode(coreNode.Endpoint, true);
                TestHelper.WaitLoop(() => BRhodiumNode.CreateRPCClient().GetBestBlockHash() == coreNode.CreateRPCClient().GetBestBlockHash());
                var bestBlockHash = BRhodiumNode.CreateRPCClient().GetBestBlockHash();
                Assert.Equal(tip.GetHash(), bestBlockHash);

                //Now check if Core connect to BRhodium
                BRhodiumNode.CreateRPCClient().RemoveNode(coreNode.Endpoint);
                TestHelper.WaitLoop(() => coreNode.CreateRPCClient().GetPeersInfo().Length == 0);

                tip = coreNode.FindBlock(10).Last();
                coreNode.CreateRPCClient().AddNode(BRhodiumNode.Endpoint, true);
                TestHelper.WaitLoop(() => BRhodiumNode.CreateRPCClient().GetBestBlockHash() == coreNode.CreateRPCClient().GetBestBlockHash());
                bestBlockHash = BRhodiumNode.CreateRPCClient().GetBestBlockHash();
                Assert.Equal(tip.GetHash(), bestBlockHash);
            }
        }

        [Fact]
        public void CanBRhodiumSyncFromBRhodium()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var BRhodiumNode = builder.CreateBRhodiumPowNode();
                var BRhodiumNodeSync = builder.CreateBRhodiumPowNode();
                var coreCreateNode = builder.CreateBitcoinCoreNode();
                builder.StartAll();

                BRhodiumNode.NotInIBD();
                BRhodiumNodeSync.NotInIBD();

                // first seed a core node with blocks and sync them to a BRhodium node
                // and wait till the BRhodium node is fully synced
                var tip = coreCreateNode.FindBlock(5).Last();
                BRhodiumNode.CreateRPCClient().AddNode(coreCreateNode.Endpoint, true);
                TestHelper.WaitLoop(() => BRhodiumNode.CreateRPCClient().GetBestBlockHash() == coreCreateNode.CreateRPCClient().GetBestBlockHash());
                var bestBlockHash = BRhodiumNode.CreateRPCClient().GetBestBlockHash();
                Assert.Equal(tip.GetHash(), bestBlockHash);

                // add a new BRhodium node which will download
                // the blocks using the GetData payload
                BRhodiumNodeSync.CreateRPCClient().AddNode(BRhodiumNode.Endpoint, true);

                // wait for download and assert
                TestHelper.WaitLoop(() => BRhodiumNode.CreateRPCClient().GetBestBlockHash() == BRhodiumNodeSync.CreateRPCClient().GetBestBlockHash());
                bestBlockHash = BRhodiumNodeSync.CreateRPCClient().GetBestBlockHash();
                Assert.Equal(tip.GetHash(), bestBlockHash);
            }
        }

        [Fact]
        public void CanCoreSyncFromBRhodium()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var BRhodiumNode = builder.CreateBRhodiumPowNode();
                var coreNodeSync = builder.CreateBitcoinCoreNode();
                var coreCreateNode = builder.CreateBitcoinCoreNode();
                builder.StartAll();

                BRhodiumNode.NotInIBD();

                // first seed a core node with blocks and sync them to a BRhodium node
                // and wait till the BRhodium node is fully synced
                var tip = coreCreateNode.FindBlock(5).Last();
                BRhodiumNode.CreateRPCClient().AddNode(coreCreateNode.Endpoint, true);
                TestHelper.WaitLoop(() => BRhodiumNode.CreateRPCClient().GetBestBlockHash() == coreCreateNode.CreateRPCClient().GetBestBlockHash());
                TestHelper.WaitLoop(() => BRhodiumNode.FullNode.HighestPersistedBlock().HashBlock == BRhodiumNode.FullNode.Chain.Tip.HashBlock);

                var bestBlockHash = BRhodiumNode.CreateRPCClient().GetBestBlockHash();
                Assert.Equal(tip.GetHash(), bestBlockHash);

                // add a new BRhodium node which will download
                // the blocks using the GetData payload
                coreNodeSync.CreateRPCClient().AddNode(BRhodiumNode.Endpoint, true);

                // wait for download and assert
                TestHelper.WaitLoop(() => BRhodiumNode.CreateRPCClient().GetBestBlockHash() == coreNodeSync.CreateRPCClient().GetBestBlockHash());
                bestBlockHash = coreNodeSync.CreateRPCClient().GetBestBlockHash();
                Assert.Equal(tip.GetHash(), bestBlockHash);
            }
        }

        [Fact]
        public void Given_NodesAreSynced_When_ABigReorgHappens_Then_TheReorgIsIgnored()
        {
            // Temporary fix so the Network static initialize will not break.
            var m = Network.Main;
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var BRhodiumMiner = builder.CreateBRhodiumPosNode();
                var BRhodiumSyncer = builder.CreateBRhodiumPosNode();
                var BRhodiumReorg = builder.CreateBRhodiumPosNode();

                builder.StartAll();
                BRhodiumMiner.NotInIBD();
                BRhodiumSyncer.NotInIBD();
                BRhodiumReorg.NotInIBD();

                // TODO: set the max allowed reorg threshold here
                // assume a reorg of 10 blocks is not allowed.
                BRhodiumMiner.FullNode.ChainBehaviorState.MaxReorgLength = 10;
                BRhodiumSyncer.FullNode.ChainBehaviorState.MaxReorgLength = 10;
                BRhodiumReorg.FullNode.ChainBehaviorState.MaxReorgLength = 10;

                BRhodiumMiner.SetDummyMinerSecret(new BitcoinSecret(new Key(), BRhodiumMiner.FullNode.Network));
                BRhodiumReorg.SetDummyMinerSecret(new BitcoinSecret(new Key(), BRhodiumReorg.FullNode.Network));

                BRhodiumMiner.GenerateBRhodiumWithMiner(1);

                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(BRhodiumMiner));
                BRhodiumMiner.CreateRPCClient().AddNode(BRhodiumReorg.Endpoint, true);
                BRhodiumMiner.CreateRPCClient().AddNode(BRhodiumSyncer.Endpoint, true);

                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(BRhodiumMiner, BRhodiumSyncer));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(BRhodiumMiner, BRhodiumReorg));

                // create a reorg by mining on two different chains
                // ================================================

                BRhodiumMiner.CreateRPCClient().RemoveNode(BRhodiumReorg.Endpoint);
                BRhodiumSyncer.CreateRPCClient().RemoveNode(BRhodiumReorg.Endpoint);
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(BRhodiumReorg));

                var t1 = Task.Run(() => BRhodiumMiner.GenerateBRhodiumWithMiner(11));
                var t2 = Task.Delay(1000).ContinueWith(t => BRhodiumReorg.GenerateBRhodiumWithMiner(12));
                Task.WaitAll(t1, t2);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(BRhodiumMiner));
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(BRhodiumReorg));

                // make sure the nodes are actually on different chains.
                Assert.NotEqual(BRhodiumMiner.FullNode.Chain.GetBlock(2).HashBlock, BRhodiumReorg.FullNode.Chain.GetBlock(2).HashBlock);

                TestHelper.TriggerSync(BRhodiumSyncer);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(BRhodiumMiner, BRhodiumSyncer));

                // The hash before the reorg node is connected.
                var hashBeforeReorg = BRhodiumMiner.FullNode.Chain.Tip.HashBlock;

                // connect the reorg chain
                BRhodiumMiner.CreateRPCClient().AddNode(BRhodiumReorg.Endpoint, true);
                BRhodiumSyncer.CreateRPCClient().AddNode(BRhodiumReorg.Endpoint, true);

                // trigger nodes to sync
                TestHelper.TriggerSync(BRhodiumMiner);
                TestHelper.TriggerSync(BRhodiumReorg);
                TestHelper.TriggerSync(BRhodiumSyncer);

                // wait for the synced chain to get headers updated.
                TestHelper.WaitLoop(() => !BRhodiumReorg.FullNode.ConnectionManager.ConnectedPeers.Any());

                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(BRhodiumMiner, BRhodiumSyncer));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(BRhodiumReorg, BRhodiumMiner) == false);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(BRhodiumReorg, BRhodiumSyncer) == false);

                // check that a reorg did not happen.
                Assert.Equal(hashBeforeReorg, BRhodiumSyncer.FullNode.Chain.Tip.HashBlock);
            }
        }

        /// <summary>
        /// This tests simulates scenario 2 from issue 636.
        /// <para>
        /// The test mines a block and roughly at the same time, but just after that, a new block at the same height
        /// arrives from the puller. Then another block comes from the puller extending the chain without the block we mined.
        /// </para>
        /// </summary>
        /// <seealso cref="https://github.com/BRhodiumproject/BRhodiumBitcoinFullNode/issues/636"/>
        [Fact]
        public void PullerVsMinerRaceCondition()
        {
            // Temporary fix so the Network static initialize will not break.
            var m = Network.Main;
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                // This represents local node.
                var BRhodiumMinerLocal = builder.CreateBRhodiumPosNode();

                // This represents remote, which blocks are received by local node using its puller.
                var BRhodiumMinerRemote = builder.CreateBRhodiumPosNode();

                builder.StartAll();
                BRhodiumMinerLocal.NotInIBD();
                BRhodiumMinerRemote.NotInIBD();

                BRhodiumMinerLocal.SetDummyMinerSecret(new BitcoinSecret(new Key(), BRhodiumMinerLocal.FullNode.Network));
                BRhodiumMinerRemote.SetDummyMinerSecret(new BitcoinSecret(new Key(), BRhodiumMinerRemote.FullNode.Network));

                // Let's mine block Ap and Bp.
                BRhodiumMinerRemote.GenerateBRhodiumWithMiner(2);

                // Wait for block repository for block sync to work.
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(BRhodiumMinerRemote));
                BRhodiumMinerLocal.CreateRPCClient().AddNode(BRhodiumMinerRemote.Endpoint, true);

                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(BRhodiumMinerLocal, BRhodiumMinerRemote));

                // Now disconnect the peers and mine block C2p on remote.
                BRhodiumMinerLocal.CreateRPCClient().RemoveNode(BRhodiumMinerRemote.Endpoint);
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(BRhodiumMinerRemote));

                // Mine block C2p.
                BRhodiumMinerRemote.GenerateBRhodiumWithMiner(1);
                Thread.Sleep(2000);

                // Now reconnect nodes and mine block C1s before C2p arrives.
                BRhodiumMinerLocal.CreateRPCClient().AddNode(BRhodiumMinerRemote.Endpoint, true);
                BRhodiumMinerLocal.GenerateBRhodiumWithMiner(1);

                // Mine block Dp.
                uint256 dpHash = BRhodiumMinerRemote.GenerateBRhodiumWithMiner(1)[0];

                // Now we wait until the local node's chain tip has correct hash of Dp.
                TestHelper.WaitLoop(() => BRhodiumMinerLocal.FullNode.Chain.Tip.HashBlock.Equals(dpHash));

                // Then give it time to receive the block from the puller.
                Thread.Sleep(2500);

                // Check that local node accepted the Dp as consensus tip.
                Assert.Equal(BRhodiumMinerLocal.FullNode.ChainBehaviorState.ConsensusTip.HashBlock, dpHash);
            }
        }

        /// <summary>
        /// This test simulates scenario from issue #862.
        /// <para>
        /// Connection scheme:
        /// Network - Node1 - MiningNode
        /// </para>
        /// </summary>
        [Fact]
        public void MiningNodeWithOneConnectionAlwaysSynced()
        {
            NetworkSimulator simulator = new NetworkSimulator();

            simulator.Initialize(4);

            var miner = simulator.Nodes[0];
            var connector = simulator.Nodes[1];
            var networkNode1 = simulator.Nodes[2];
            var networkNode2 = simulator.Nodes[3];

            // Connect nodes with each other. Miner is connected to connector and connector, node1, node2 are connected with each other.
            miner.CreateRPCClient().AddNode(connector.Endpoint, true);
            connector.CreateRPCClient().AddNode(networkNode1.Endpoint, true);
            connector.CreateRPCClient().AddNode(networkNode2.Endpoint, true);
            networkNode1.CreateRPCClient().AddNode(networkNode2.Endpoint, true);

            simulator.MakeSureEachNodeCanMineAndSync();

            int networkHeight = miner.FullNode.Chain.Height;
            Assert.Equal(networkHeight, simulator.Nodes.Count);

            // Random node on network generates a block.
            networkNode1.GenerateBRhodium(1);

            // Wait until connector get the hash of network's block.
            while ((connector.FullNode.ChainBehaviorState.ConsensusTip.HashBlock != networkNode1.FullNode.ChainBehaviorState.ConsensusTip.HashBlock) ||
                   (networkNode1.FullNode.ChainBehaviorState.ConsensusTip.Height == networkHeight))
                Thread.Sleep(1);

            // Make sure that miner did not advance yet but connector did.
            Assert.NotEqual(miner.FullNode.Chain.Tip.HashBlock, networkNode1.FullNode.Chain.Tip.HashBlock);
            Assert.Equal(connector.FullNode.Chain.Tip.HashBlock, networkNode1.FullNode.Chain.Tip.HashBlock);
            Assert.Equal(miner.FullNode.Chain.Tip.Height, networkHeight);
            Assert.Equal(connector.FullNode.Chain.Tip.Height, networkHeight + 1);

            // Miner mines the block.
            miner.GenerateBRhodium(1);
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(miner));

            networkHeight++;

            // Make sure that at this moment miner's tip != network's and connector's tip.
            Assert.NotEqual(miner.FullNode.Chain.Tip.HashBlock, networkNode1.FullNode.Chain.Tip.HashBlock);
            Assert.Equal(connector.FullNode.Chain.Tip.HashBlock, networkNode1.FullNode.Chain.Tip.HashBlock);
            Assert.Equal(miner.FullNode.Chain.Tip.Height, networkHeight);
            Assert.Equal(connector.FullNode.Chain.Tip.Height, networkHeight);

            connector.GenerateBRhodium(1);
            networkHeight++;

            int delay = 0;

            while (true)
            {
                Thread.Sleep(50);
                if (simulator.DidAllNodesReachHeight(networkHeight))
                    break;
                delay += 50;

                Assert.True(delay < 10 * 1000, "Miner node was not able to advance!");
            }

            Assert.Equal(networkNode1.FullNode.Chain.Tip.HashBlock, miner.FullNode.Chain.Tip.HashBlock);
        }
    }
}
