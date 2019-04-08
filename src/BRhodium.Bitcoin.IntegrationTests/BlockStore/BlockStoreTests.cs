using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using BRhodium.Bitcoin.Features.BlockStore;
using BRhodium.Node.IntegrationTests.EnvironmentMockUpHelpers;
using BRhodium.Node.Utilities;
using Xunit;

namespace BRhodium.Node.IntegrationTests.BlockStore
{
    public class BlockStoreTests
    {
        /// <summary>Factory for creating loggers.</summary>
        protected readonly ILoggerFactory loggerFactory;

        /// <summary>
        /// Initializes logger factory for tests in this class.
        /// </summary>
        public BlockStoreTests()
        {
            this.loggerFactory = new LoggerFactory();
            DBreezeSerializer serializer = new DBreezeSerializer();
            serializer.Initialize(Network.BRhodiumRegTest);
        }

        private void BlockRepositoryBench()
        {
            using (var dir = TestDirectory.Create())
            {
                using (var blockRepo = new BlockRepository(Network.Main, dir.FolderName, DateTimeProvider.Default, this.loggerFactory))
                {
                    var lst = new List<Block>();
                    for (int i = 0; i < 30; i++)
                    {
                        // roughly 1mb blocks
                        var block = new Block();
                        for (int j = 0; j < 3000; j++)
                        {
                            var trx = new Transaction();
                            block.AddTransaction(new Transaction());
                            trx.AddInput(new TxIn(Script.Empty));
                            trx.AddOutput(Money.COIN + j + i, new Script(Guid.NewGuid().ToByteArray()
                                .Concat(Guid.NewGuid().ToByteArray())
                                .Concat(Guid.NewGuid().ToByteArray())
                                .Concat(Guid.NewGuid().ToByteArray())
                                .Concat(Guid.NewGuid().ToByteArray())
                                .Concat(Guid.NewGuid().ToByteArray())));
                            trx.AddInput(new TxIn(Script.Empty));
                            trx.AddOutput(Money.COIN + j + i + 1, new Script(Guid.NewGuid().ToByteArray()
                                .Concat(Guid.NewGuid().ToByteArray())
                                .Concat(Guid.NewGuid().ToByteArray())
                                .Concat(Guid.NewGuid().ToByteArray())
                                .Concat(Guid.NewGuid().ToByteArray())
                                .Concat(Guid.NewGuid().ToByteArray())));
                            block.AddTransaction(trx);
                        }
                        block.UpdateMerkleRoot();
                        block.Header.HashPrevBlock = lst.Any() ? lst.Last().GetHash() : Network.Main.GenesisHash;
                        lst.Add(block);
                    }

                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();
                    blockRepo.PutAsync(lst.Last().GetHash(), lst).GetAwaiter().GetResult();
                    var first = stopwatch.ElapsedMilliseconds;
                    blockRepo.PutAsync(lst.Last().GetHash(), lst).GetAwaiter().GetResult();
                    var second = stopwatch.ElapsedMilliseconds;
                }
            }
        }

        [Fact]
        public void BlockRepositoryPutBatch()
        {
            using (var dir = TestDirectory.Create())
            {
                using (var blockRepo = new BlockRepository(Network.Main, dir.FolderName, DateTimeProvider.Default, this.loggerFactory))
                {
                    blockRepo.SetTxIndexAsync(true).Wait();

                    var lst = new List<Block>();
                    for (int i = 0; i < 5; i++)
                    {
                        // put
                        var block = new Block();
                        block.AddTransaction(new Transaction());
                        block.AddTransaction(new Transaction());
                        block.Transactions[0].AddInput(new TxIn(Script.Empty));
                        block.Transactions[0].AddOutput(Money.COIN + i * 2, Script.Empty);
                        block.Transactions[1].AddInput(new TxIn(Script.Empty));
                        block.Transactions[1].AddOutput(Money.COIN + i * 2 + 1, Script.Empty);
                        block.UpdateMerkleRoot();
                        block.Header.HashPrevBlock = lst.Any() ? lst.Last().GetHash() : Network.Main.GenesisHash;
                        lst.Add(block);
                    }

                    blockRepo.PutAsync(lst.Last().GetHash(), lst).GetAwaiter().GetResult();

                    // check each block
                    foreach (var block in lst)
                    {
                        var received = blockRepo.GetAsync(block.GetHash()).GetAwaiter().GetResult();
                        Assert.True(block.ToBytes().SequenceEqual(received.ToBytes()));

                        foreach (var transaction in block.Transactions)
                        {
                            var trx = blockRepo.GetTrxAsync(transaction.GetHash()).GetAwaiter().GetResult();
                            Assert.True(trx.ToBytes().SequenceEqual(transaction.ToBytes()));
                        }
                    }

                    // delete
                    blockRepo.DeleteAsync(lst.ElementAt(2).GetHash(), new[] { lst.ElementAt(2).GetHash() }.ToList()).GetAwaiter().GetResult();
                    var deleted = blockRepo.GetAsync(lst.ElementAt(2).GetHash()).GetAwaiter().GetResult();
                    Assert.Null(deleted);
                }
            }
        }

        [Fact]
        public void BlockRepositoryBlockHash()
        {
            using (var dir = TestDirectory.Create())
            {
                using (var blockRepo = new BlockRepository(Network.Main, dir.FolderName, DateTimeProvider.Default, this.loggerFactory))
                {
                    blockRepo.InitializeAsync().GetAwaiter().GetResult();

                    Assert.Equal(Network.Main.GenesisHash, blockRepo.BlockHash);
                    var hash = new Block().GetHash();
                    blockRepo.SetBlockHashAsync(hash).GetAwaiter().GetResult();
                    Assert.Equal(hash, blockRepo.BlockHash);
                }
            }
        }

        [Fact]
        public void BlockBroadcastInv()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var BRhodiumNodeSync = builder.CreateBRhodiumPowNode();
                var BRhodiumNode1 = builder.CreateBRhodiumPowNode();
                var BRhodiumNode2 = builder.CreateBRhodiumPowNode();
                builder.StartAll();
                BRhodiumNodeSync.NotInIBD();
                BRhodiumNode1.NotInIBD();
                BRhodiumNode2.NotInIBD();

                // generate blocks and wait for the downloader to pickup
                BRhodiumNodeSync.SetDummyMinerSecret(new BitcoinSecret(new Key(), BRhodiumNodeSync.FullNode.Network));
                BRhodiumNodeSync.GenerateBRhodiumWithMiner(10); // coinbase maturity = 10
                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => BRhodiumNodeSync.FullNode.ConsensusLoop().Tip.HashBlock == BRhodiumNodeSync.FullNode.Chain.Tip.HashBlock);
                TestHelper.WaitLoop(() => BRhodiumNodeSync.FullNode.ChainBehaviorState.ConsensusTip.HashBlock == BRhodiumNodeSync.FullNode.Chain.Tip.HashBlock);
                TestHelper.WaitLoop(() => BRhodiumNodeSync.FullNode.HighestPersistedBlock().HashBlock == BRhodiumNodeSync.FullNode.Chain.Tip.HashBlock);

                // sync both nodes
                BRhodiumNode1.CreateRPCClient().AddNode(BRhodiumNodeSync.Endpoint, true);
                BRhodiumNode2.CreateRPCClient().AddNode(BRhodiumNodeSync.Endpoint, true);
                TestHelper.WaitLoop(() => BRhodiumNode1.CreateRPCClient().GetBestBlockHash() == BRhodiumNodeSync.CreateRPCClient().GetBestBlockHash());
                TestHelper.WaitLoop(() => BRhodiumNode2.CreateRPCClient().GetBestBlockHash() == BRhodiumNodeSync.CreateRPCClient().GetBestBlockHash());

                // set node2 to use inv (not headers)
                BRhodiumNode2.FullNode.ConnectionManager.ConnectedPeers.First().Behavior<BlockStoreBehavior>().PreferHeaders = false;

                // generate two new blocks
                BRhodiumNodeSync.GenerateBRhodiumWithMiner(2);
                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => BRhodiumNodeSync.FullNode.Chain.Tip.HashBlock == BRhodiumNodeSync.FullNode.ConsensusLoop().Tip.HashBlock);
                TestHelper.WaitLoop(() => BRhodiumNodeSync.FullNode.BlockStoreManager().BlockRepository.GetAsync(BRhodiumNodeSync.CreateRPCClient().GetBestBlockHash()).Result != null);

                // wait for the other nodes to pick up the newly generated blocks
                TestHelper.WaitLoop(() => BRhodiumNode1.CreateRPCClient().GetBestBlockHash() == BRhodiumNodeSync.CreateRPCClient().GetBestBlockHash());
                TestHelper.WaitLoop(() => BRhodiumNode2.CreateRPCClient().GetBestBlockHash() == BRhodiumNodeSync.CreateRPCClient().GetBestBlockHash());
            }
        }

        [Fact]
        public void BlockStoreCanRecoverOnStartup()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var BRhodiumNodeSync = builder.CreateBRhodiumPowNode();
                builder.StartAll();
                BRhodiumNodeSync.NotInIBD();

                // generate blocks and wait for the downloader to pickup
                BRhodiumNodeSync.SetDummyMinerSecret(new BitcoinSecret(new Key(), BRhodiumNodeSync.FullNode.Network));

                BRhodiumNodeSync.GenerateBRhodiumWithMiner(10);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(BRhodiumNodeSync));

                // set the tip of best chain some blocks in the apst
                BRhodiumNodeSync.FullNode.Chain.SetTip(BRhodiumNodeSync.FullNode.Chain.GetBlock(BRhodiumNodeSync.FullNode.Chain.Height - 5));

                // stop the node it will persist the chain with the reset tip
                BRhodiumNodeSync.FullNode.Dispose();

                var newNodeInstance = builder.CloneBRhodiumNode(BRhodiumNodeSync);

                // load the node, this should hit the block store recover code
                newNodeInstance.Start();

                // check that store recovered to be the same as the best chain.
                Assert.Equal(newNodeInstance.FullNode.Chain.Tip.HashBlock, newNodeInstance.FullNode.HighestPersistedBlock().HashBlock);
                //TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(BRhodiumNodeSync));
            }
        }

        [Fact]
        public void BlockStoreCanReorg()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var BRhodiumNodeSync = builder.CreateBRhodiumPowNode();
                var BRhodiumNode1 = builder.CreateBRhodiumPowNode();
                var BRhodiumNode2 = builder.CreateBRhodiumPowNode();
                builder.StartAll();
                BRhodiumNodeSync.NotInIBD();
                BRhodiumNode1.NotInIBD();
                BRhodiumNode2.NotInIBD();

                // generate blocks and wait for the downloader to pickup
                BRhodiumNode1.SetDummyMinerSecret(new BitcoinSecret(new Key(), BRhodiumNodeSync.FullNode.Network));
                BRhodiumNode2.SetDummyMinerSecret(new BitcoinSecret(new Key(), BRhodiumNodeSync.FullNode.Network));
                // sync both nodes
                BRhodiumNodeSync.CreateRPCClient().AddNode(BRhodiumNode1.Endpoint, true);
                BRhodiumNodeSync.CreateRPCClient().AddNode(BRhodiumNode2.Endpoint, true);

                BRhodiumNode1.GenerateBRhodiumWithMiner(10);
                TestHelper.WaitLoop(() => BRhodiumNode1.FullNode.HighestPersistedBlock().Height == 10);

                TestHelper.WaitLoop(() => BRhodiumNode1.FullNode.HighestPersistedBlock().HashBlock == BRhodiumNodeSync.FullNode.HighestPersistedBlock().HashBlock);
                TestHelper.WaitLoop(() => BRhodiumNode2.FullNode.HighestPersistedBlock().HashBlock == BRhodiumNodeSync.FullNode.HighestPersistedBlock().HashBlock);

                // remove node 2
                BRhodiumNodeSync.CreateRPCClient().RemoveNode(BRhodiumNode2.Endpoint);
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(BRhodiumNode2));

                // mine some more with node 1
                BRhodiumNode1.GenerateBRhodiumWithMiner(10);

                // wait for node 1 to sync
                TestHelper.WaitLoop(() => BRhodiumNode1.FullNode.HighestPersistedBlock().Height == 20);
                TestHelper.WaitLoop(() => BRhodiumNode1.FullNode.HighestPersistedBlock().HashBlock == BRhodiumNodeSync.FullNode.HighestPersistedBlock().HashBlock);

                // remove node 1
                BRhodiumNodeSync.CreateRPCClient().RemoveNode(BRhodiumNode1.Endpoint);
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(BRhodiumNode1));

                // mine a higher chain with node2
                BRhodiumNode2.GenerateBRhodiumWithMiner(20);
                TestHelper.WaitLoop(() => BRhodiumNode2.FullNode.HighestPersistedBlock().Height == 30);

                // add node2
                BRhodiumNodeSync.CreateRPCClient().AddNode(BRhodiumNode2.Endpoint, true);

                // node2 should be synced
                TestHelper.WaitLoop(() => BRhodiumNode2.FullNode.HighestPersistedBlock().HashBlock == BRhodiumNodeSync.FullNode.HighestPersistedBlock().HashBlock);
            }
        }

        [Fact]
        public void BlockStoreIndexTx()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var BRhodiumNode1 = builder.CreateBRhodiumPowNode();
                var BRhodiumNode2 = builder.CreateBRhodiumPowNode();
                builder.StartAll();
                BRhodiumNode1.NotInIBD();
                BRhodiumNode2.NotInIBD();

                // generate blocks and wait for the downloader to pickup
                BRhodiumNode1.SetDummyMinerSecret(new BitcoinSecret(new Key(), BRhodiumNode1.FullNode.Network));
                BRhodiumNode2.SetDummyMinerSecret(new BitcoinSecret(new Key(), BRhodiumNode2.FullNode.Network));
                // sync both nodes
                BRhodiumNode1.CreateRPCClient().AddNode(BRhodiumNode2.Endpoint, true);
                BRhodiumNode1.GenerateBRhodiumWithMiner(10);
                TestHelper.WaitLoop(() => BRhodiumNode1.FullNode.HighestPersistedBlock().Height == 10);
                TestHelper.WaitLoop(() => BRhodiumNode1.FullNode.HighestPersistedBlock().HashBlock == BRhodiumNode2.FullNode.HighestPersistedBlock().HashBlock);

                var bestBlock1 = BRhodiumNode1.FullNode.BlockStoreManager().BlockRepository.GetAsync(BRhodiumNode1.FullNode.Chain.Tip.HashBlock).Result;
                Assert.NotNull(bestBlock1);

                // get the block coinbase trx
                var trx = BRhodiumNode2.FullNode.BlockStoreManager().BlockRepository.GetTrxAsync(bestBlock1.Transactions.First().GetHash()).Result;
                Assert.NotNull(trx);
                Assert.Equal(bestBlock1.Transactions.First().GetHash(), trx.GetHash());
            }
        }
    }
}
