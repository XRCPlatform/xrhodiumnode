using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using BRhodium.Bitcoin.Features.Consensus;
using BRhodium.Bitcoin.Features.MemoryPool;
using BRhodium.Node.IntegrationTests.EnvironmentMockUpHelpers;
using BRhodium.Node.Utilities;
using Xunit;

namespace BRhodium.Node.IntegrationTests
{
    public class MemoryPoolTests
    {
        public class DateTimeProviderSet : DateTimeProvider
        {
            public long time;
            public DateTime timeutc;

            public override long GetTime()
            {
                return this.time;
            }

            public override DateTime GetUtcNow()
            {
                return this.timeutc;
            }
        }

        [Fact]
        public void AddToMempool()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var BRhodiumNodeSync = builder.CreateBRhodiumPowNode();
                builder.StartAll();

                BRhodiumNodeSync.SetDummyMinerSecret(new BitcoinSecret(new Key(), BRhodiumNodeSync.FullNode.Network));
                BRhodiumNodeSync.GenerateBRhodiumWithMiner(105); // coinbase maturity = 100
                TestHelper.WaitLoop(() => BRhodiumNodeSync.FullNode.ConsensusLoop().Tip.HashBlock == BRhodiumNodeSync.FullNode.Chain.Tip.HashBlock);
                TestHelper.WaitLoop(() => BRhodiumNodeSync.FullNode.HighestPersistedBlock().HashBlock == BRhodiumNodeSync.FullNode.Chain.Tip.HashBlock);

                var block = BRhodiumNodeSync.FullNode.BlockStoreManager().BlockRepository.GetAsync(BRhodiumNodeSync.FullNode.Chain.GetBlock(4).HashBlock).Result;
                var prevTrx = block.Transactions.First();
                var dest = new BitcoinSecret(new Key(), BRhodiumNodeSync.FullNode.Network);

                Transaction tx = BRhodiumNodeSync.FullNode.Network.Consensus.ConsensusFactory.CreateTransaction();
                tx.AddInput(new TxIn(new OutPoint(prevTrx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(BRhodiumNodeSync.MinerSecret.PubKey)));
                tx.AddOutput(new TxOut("25", dest.PubKey.Hash));
                tx.AddOutput(new TxOut("24", new Key().PubKey.Hash)); // 1 btc fee
                tx.Sign(BRhodiumNodeSync.FullNode.Network, BRhodiumNodeSync.MinerSecret, false);

                BRhodiumNodeSync.Broadcast(tx);

                TestHelper.WaitLoop(() => BRhodiumNodeSync.CreateRPCClient().GetRawMempool().Length == 1);
            }
        }

        [Fact]
        public void AddToMempoolTrxSpendingTwoOutputFromSameTrx()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var BRhodiumNodeSync = builder.CreateBRhodiumPowNode();
                builder.StartAll();
                BRhodiumNodeSync.NotInIBD();

                BRhodiumNodeSync.SetDummyMinerSecret(new BitcoinSecret(new Key(), BRhodiumNodeSync.FullNode.Network));
                BRhodiumNodeSync.GenerateBRhodium(105); // coinbase maturity = 100
                TestHelper.WaitLoop(() => BRhodiumNodeSync.FullNode.ConsensusLoop().Tip.HashBlock == BRhodiumNodeSync.FullNode.Chain.Tip.HashBlock);
                TestHelper.WaitLoop(() => BRhodiumNodeSync.FullNode.HighestPersistedBlock().HashBlock == BRhodiumNodeSync.FullNode.Chain.Tip.HashBlock);

                var block = BRhodiumNodeSync.FullNode.BlockStoreManager().BlockRepository.GetAsync(BRhodiumNodeSync.FullNode.Chain.GetBlock(4).HashBlock).Result;
                var prevTrx = block.Transactions.First();
                var dest1 = new BitcoinSecret(new Key(), BRhodiumNodeSync.FullNode.Network);
                var dest2 = new BitcoinSecret(new Key(), BRhodiumNodeSync.FullNode.Network);

                Transaction parentTx = BRhodiumNodeSync.FullNode.Network.Consensus.ConsensusFactory.CreateTransaction();
                parentTx.AddInput(new TxIn(new OutPoint(prevTrx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(BRhodiumNodeSync.MinerSecret.PubKey)));
                parentTx.AddOutput(new TxOut("25", dest1.PubKey.Hash));
                parentTx.AddOutput(new TxOut("24", dest2.PubKey.Hash)); // 1 btc fee
                parentTx.Sign(BRhodiumNodeSync.FullNode.Network, BRhodiumNodeSync.MinerSecret, false);
                BRhodiumNodeSync.Broadcast(parentTx);
                // wiat for the trx to enter the pool
                TestHelper.WaitLoop(() => BRhodiumNodeSync.CreateRPCClient().GetRawMempool().Length == 1);
                // mine the transactions in the mempool
                BRhodiumNodeSync.GenerateBRhodium(1, BRhodiumNodeSync.FullNode.MempoolManager().InfoAllAsync().Result.Select(s => s.Trx).ToList());
                TestHelper.WaitLoop(() => BRhodiumNodeSync.CreateRPCClient().GetRawMempool().Length == 0);

                //create a new trx spending both outputs
                Transaction tx = BRhodiumNodeSync.FullNode.Network.Consensus.ConsensusFactory.CreateTransaction();
                tx.AddInput(new TxIn(new OutPoint(parentTx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(dest1.PubKey)));
                tx.AddInput(new TxIn(new OutPoint(parentTx.GetHash(), 1), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(dest2.PubKey)));
                tx.AddOutput(new TxOut("48", new Key().PubKey.Hash)); // 1 btc fee
                var signed = new TransactionBuilder(BRhodiumNodeSync.FullNode.Network).AddKeys(dest1, dest2).AddCoins(parentTx.Outputs.AsCoins()).SignTransaction(tx);

                BRhodiumNodeSync.Broadcast(signed);
                TestHelper.WaitLoop(() => BRhodiumNodeSync.CreateRPCClient().GetRawMempool().Length == 1);
            }
        }

        [Fact]
        public void MempoolReceiveFromManyNodes()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var BRhodiumNodeSync = builder.CreateBRhodiumPowNode();
                builder.StartAll();
                BRhodiumNodeSync.NotInIBD();

                BRhodiumNodeSync.SetDummyMinerSecret(new BitcoinSecret(new Key(), BRhodiumNodeSync.FullNode.Network));
                BRhodiumNodeSync.GenerateBRhodiumWithMiner(10); // coinbase maturity = 6
                TestHelper.WaitLoop(() => BRhodiumNodeSync.FullNode.ConsensusLoop().Tip.HashBlock == BRhodiumNodeSync.FullNode.Chain.Tip.HashBlock);
                TestHelper.WaitLoop(() => BRhodiumNodeSync.FullNode.HighestPersistedBlock().HashBlock == BRhodiumNodeSync.FullNode.Chain.Tip.HashBlock);

                var trxs = new List<Transaction>();
                foreach (var index in Enumerable.Range(1, 10))
                {
                    var block = BRhodiumNodeSync.FullNode.BlockStoreManager().BlockRepository.GetAsync(BRhodiumNodeSync.FullNode.Chain.GetBlock(index).HashBlock).Result;
                    var prevTrx = block.Transactions.First();
                    var dest = new BitcoinSecret(new Key(), BRhodiumNodeSync.FullNode.Network);

                    Transaction tx = BRhodiumNodeSync.FullNode.Network.Consensus.ConsensusFactory.CreateTransaction();
                    tx.AddInput(new TxIn(new OutPoint(prevTrx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(BRhodiumNodeSync.MinerSecret.PubKey)));
                    tx.AddOutput(new TxOut("2.5", dest.PubKey.Hash));
                    tx.AddOutput(new TxOut("2.4", new Key().PubKey.Hash)); // 0.1 xrc fee
                    tx.Sign(BRhodiumNodeSync.FullNode.Network, BRhodiumNodeSync.MinerSecret, false);
                    trxs.Add(tx);
                }
                var options = new ParallelOptions { MaxDegreeOfParallelism = 10 };
                Parallel.ForEach(trxs, options, transaction =>
                {
                    BRhodiumNodeSync.Broadcast(transaction);
                });

                TestHelper.WaitLoop(() => BRhodiumNodeSync.CreateRPCClient().GetRawMempool().Length == 10);
            }
        }

        [Fact]
        public void TxMempoolBlockDoublespend()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var BRhodiumNodeSync = builder.CreateBRhodiumPowNode();
                builder.StartAll();
                BRhodiumNodeSync.NotInIBD();
                BRhodiumNodeSync.FullNode.Settings.RequireStandard = true; // make sure to test standard tx

                BRhodiumNodeSync.SetDummyMinerSecret(new BitcoinSecret(new Key(), BRhodiumNodeSync.FullNode.Network));
                BRhodiumNodeSync.GenerateBRhodiumWithMiner(100); // coinbase maturity = 100
                TestHelper.WaitLoop(() => BRhodiumNodeSync.FullNode.ConsensusLoop().Tip.HashBlock == BRhodiumNodeSync.FullNode.Chain.Tip.HashBlock);
                TestHelper.WaitLoop(() => BRhodiumNodeSync.FullNode.HighestPersistedBlock().HashBlock == BRhodiumNodeSync.FullNode.Chain.Tip.HashBlock);

                // Make sure skipping validation of transctions that were
                // validated going into the memory pool does not allow
                // double-spends in blocks to pass validation when they should not.

                var scriptPubKey = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(BRhodiumNodeSync.MinerSecret.PubKey);
                var genBlock = BRhodiumNodeSync.FullNode.BlockStoreManager().BlockRepository.GetAsync(BRhodiumNodeSync.FullNode.Chain.GetBlock(1).HashBlock).Result;

                // Create a double-spend of mature coinbase txn:
                List<Transaction> spends = new List<Transaction>(2);
                foreach (var index in Enumerable.Range(1, 2))
                {
                    var trx = BRhodiumNodeSync.FullNode.Network.Consensus.ConsensusFactory.CreateTransaction();
                    trx.AddInput(new TxIn(new OutPoint(genBlock.Transactions[0].GetHash(), 0), scriptPubKey));
                    trx.AddOutput(Money.Cents(11), new Key().PubKey.Hash);
                    // Sign:
                    trx.Sign(BRhodiumNodeSync.FullNode.Network, BRhodiumNodeSync.MinerSecret, false);
                    spends.Add(trx);
                }

                // Test 1: block with both of those transactions should be rejected.
                var block = BRhodiumNodeSync.GenerateBRhodium(1, spends).Single();
                TestHelper.WaitLoop(() => BRhodiumNodeSync.FullNode.ConsensusLoop().Tip.HashBlock == BRhodiumNodeSync.FullNode.Chain.Tip.HashBlock);
                Assert.True(BRhodiumNodeSync.FullNode.Chain.Tip.HashBlock != block.GetHash());

                // Test 2: ... and should be rejected if spend1 is in the memory pool
                Assert.True(BRhodiumNodeSync.AddToBRhodiumMempool(spends[0]));
                block = BRhodiumNodeSync.GenerateBRhodium(1, spends).Single();
                TestHelper.WaitLoop(() => BRhodiumNodeSync.FullNode.ConsensusLoop().Tip.HashBlock == BRhodiumNodeSync.FullNode.Chain.Tip.HashBlock);
                Assert.True(BRhodiumNodeSync.FullNode.Chain.Tip.HashBlock != block.GetHash());
                BRhodiumNodeSync.FullNode.MempoolManager().Clear().Wait();

                // Test 3: ... and should be rejected if spend2 is in the memory pool
                Assert.True(BRhodiumNodeSync.AddToBRhodiumMempool(spends[1]));
                block = BRhodiumNodeSync.GenerateBRhodium(1, spends).Single();
                TestHelper.WaitLoop(() => BRhodiumNodeSync.FullNode.ConsensusLoop().Tip.HashBlock == BRhodiumNodeSync.FullNode.Chain.Tip.HashBlock);
                Assert.True(BRhodiumNodeSync.FullNode.Chain.Tip.HashBlock != block.GetHash());
                BRhodiumNodeSync.FullNode.MempoolManager().Clear().Wait();

                // Final sanity test: first spend in mempool, second in block, that's OK:
                List<Transaction> oneSpend = new List<Transaction>();
                oneSpend.Add(spends[0]);
                Assert.True(BRhodiumNodeSync.AddToBRhodiumMempool(spends[1]));
                block = BRhodiumNodeSync.GenerateBRhodium(1, oneSpend).Single();
                TestHelper.WaitLoop(() => BRhodiumNodeSync.FullNode.ConsensusLoop().Tip.HashBlock == BRhodiumNodeSync.FullNode.Chain.Tip.HashBlock);
                Assert.True(BRhodiumNodeSync.FullNode.Chain.Tip.HashBlock == block.GetHash());

                // spends[1] should have been removed from the mempool when the
                // block with spends[0] is accepted:
                TestHelper.WaitLoop(() => BRhodiumNodeSync.FullNode.MempoolManager().MempoolSize().Result == 0);
            }
        }

        [Fact]
        public void TxMempoolMapOrphans()
        {
            var rand = new Random();
            var randByte = new byte[32];
            Func<uint256> randHash = () =>
            {
                rand.NextBytes(randByte);
                return new uint256(randByte);
            };

            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var BRhodiumNode = builder.CreateBRhodiumPowNode();
                builder.StartAll();

                BRhodiumNode.SetDummyMinerSecret(new BitcoinSecret(new Key(), BRhodiumNode.FullNode.Network));

                // 50 orphan transactions:
                for (ulong i = 0; i < 50; i++)
                {
                    Transaction tx = BRhodiumNode.FullNode.Network.Consensus.ConsensusFactory.CreateTransaction();
                    tx.AddInput(new TxIn(new OutPoint(randHash(), 0), new Script(OpcodeType.OP_1)));
                    tx.AddOutput(new TxOut(new Money(1 * Money.CENT), BRhodiumNode.MinerSecret.ScriptPubKey));

                    BRhodiumNode.FullNode.MempoolManager().Orphans.AddOrphanTx(i, tx).Wait();
                }

                Assert.Equal(50, BRhodiumNode.FullNode.MempoolManager().Orphans.OrphansList().Count);

                // ... and 50 that depend on other orphans:
                for (ulong i = 0; i < 50; i++)
                {
                    var txPrev = BRhodiumNode.FullNode.MempoolManager().Orphans.OrphansList().ElementAt(rand.Next(BRhodiumNode.FullNode.MempoolManager().Orphans.OrphansList().Count));

                    Transaction tx = BRhodiumNode.FullNode.Network.Consensus.ConsensusFactory.CreateTransaction();
                    tx.AddInput(new TxIn(new OutPoint(txPrev.Tx.GetHash(), 0), new Script(OpcodeType.OP_1)));
                    tx.AddOutput(new TxOut(new Money((1 + i + 100) * Money.CENT), BRhodiumNode.MinerSecret.ScriptPubKey));
                    BRhodiumNode.FullNode.MempoolManager().Orphans.AddOrphanTx(i, tx).Wait();
                }

                Assert.Equal(100, BRhodiumNode.FullNode.MempoolManager().Orphans.OrphansList().Count);

                // This really-big orphan should be ignored:
                for (ulong i = 0; i < 10; i++)
                {
                    var txPrev = BRhodiumNode.FullNode.MempoolManager().Orphans.OrphansList().ElementAt(rand.Next(BRhodiumNode.FullNode.MempoolManager().Orphans.OrphansList().Count));
                    Transaction tx = BRhodiumNode.FullNode.Network.Consensus.ConsensusFactory.CreateTransaction();
                    tx.AddOutput(new TxOut(new Money(1 * Money.CENT), BRhodiumNode.MinerSecret.ScriptPubKey));
                    foreach (var index in Enumerable.Range(0, 2777))
                        tx.AddInput(new TxIn(new OutPoint(txPrev.Tx.GetHash(), index), new Script(OpcodeType.OP_1)));

                    Assert.False(BRhodiumNode.FullNode.MempoolManager().Orphans.AddOrphanTx(i, tx).Result);
                }

                Assert.Equal(100, BRhodiumNode.FullNode.MempoolManager().Orphans.OrphansList().Count);

                // Test EraseOrphansFor:
                for (ulong i = 0; i < 3; i++)
                {
                    var sizeBefore = BRhodiumNode.FullNode.MempoolManager().Orphans.OrphansList().Count;
                    BRhodiumNode.FullNode.MempoolManager().Orphans.EraseOrphansFor(i).Wait();
                    Assert.True(BRhodiumNode.FullNode.MempoolManager().Orphans.OrphansList().Count < sizeBefore);
                }

                // Test LimitOrphanTxSize() function:
                BRhodiumNode.FullNode.MempoolManager().Orphans.LimitOrphanTxSizeAsync(40).Wait();
                Assert.True(BRhodiumNode.FullNode.MempoolManager().Orphans.OrphansList().Count <= 40);
                BRhodiumNode.FullNode.MempoolManager().Orphans.LimitOrphanTxSizeAsync(10).Wait();
                Assert.True(BRhodiumNode.FullNode.MempoolManager().Orphans.OrphansList().Count <= 10);
                BRhodiumNode.FullNode.MempoolManager().Orphans.LimitOrphanTxSizeAsync(0).Wait();
                Assert.True(!BRhodiumNode.FullNode.MempoolManager().Orphans.OrphansList().Any());
            }
        }

        [Fact]
        public void MempoolAddNodeWithOrphans()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var BRhodiumNodeSync = builder.CreateBRhodiumPowNode();
                builder.StartAll();
                BRhodiumNodeSync.NotInIBD();

                BRhodiumNodeSync.SetDummyMinerSecret(new BitcoinSecret(new Key(), BRhodiumNodeSync.FullNode.Network));
                BRhodiumNodeSync.GenerateBRhodium(101); // coinbase maturity = 100
                TestHelper.WaitLoop(() => BRhodiumNodeSync.FullNode.ConsensusLoop().Tip.HashBlock == BRhodiumNodeSync.FullNode.Chain.Tip.HashBlock);
                TestHelper.WaitLoop(() => BRhodiumNodeSync.FullNode.ChainBehaviorState.ConsensusTip.HashBlock == BRhodiumNodeSync.FullNode.Chain.Tip.HashBlock);
                TestHelper.WaitLoop(() => BRhodiumNodeSync.FullNode.HighestPersistedBlock().HashBlock == BRhodiumNodeSync.FullNode.Chain.Tip.HashBlock);

                var block = BRhodiumNodeSync.FullNode.BlockStoreManager().BlockRepository.GetAsync(BRhodiumNodeSync.FullNode.Chain.GetBlock(1).HashBlock).Result;
                var prevTrx = block.Transactions.First();
                var dest = new BitcoinSecret(new Key(), BRhodiumNodeSync.FullNode.Network);

                var key = new Key();
                Transaction tx = BRhodiumNodeSync.FullNode.Network.Consensus.ConsensusFactory.CreateTransaction();
                tx.AddInput(new TxIn(new OutPoint(prevTrx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(BRhodiumNodeSync.MinerSecret.PubKey)));
                tx.AddOutput(new TxOut("25", dest.PubKey.Hash));
                tx.AddOutput(new TxOut("24", key.PubKey.Hash)); // 1 btc fee
                tx.Sign(BRhodiumNodeSync.FullNode.Network, BRhodiumNodeSync.MinerSecret, false);

                Transaction txOrphan = BRhodiumNodeSync.FullNode.Network.Consensus.ConsensusFactory.CreateTransaction();
                txOrphan.AddInput(new TxIn(new OutPoint(tx.GetHash(), 1), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(key.PubKey)));
                txOrphan.AddOutput(new TxOut("10", new Key().PubKey.Hash));
                txOrphan.Sign(BRhodiumNodeSync.FullNode.Network, key.GetBitcoinSecret(BRhodiumNodeSync.FullNode.Network), false);

                // broadcast the orphan
                BRhodiumNodeSync.Broadcast(txOrphan);
                TestHelper.WaitLoop(() => BRhodiumNodeSync.FullNode.MempoolManager().Orphans.OrphansList().Count == 1);
                // broadcast the parent
                BRhodiumNodeSync.Broadcast(tx);
                TestHelper.WaitLoop(() => BRhodiumNodeSync.FullNode.MempoolManager().Orphans.OrphansList().Count == 0);
                // wait for orphan to get in the pool
                TestHelper.WaitLoop(() => BRhodiumNodeSync.CreateRPCClient().GetRawMempool().Length == 2);
            }
        }

        [Fact]
        public void MempoolSyncTransactions()
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
                BRhodiumNodeSync.GenerateBRhodiumWithMiner(105); // coinbase maturity = 100
                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(BRhodiumNodeSync));

                // sync both nodes
                BRhodiumNode1.CreateRPCClient().AddNode(BRhodiumNodeSync.Endpoint, true);
                BRhodiumNode2.CreateRPCClient().AddNode(BRhodiumNodeSync.Endpoint, true);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(BRhodiumNode1, BRhodiumNodeSync));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(BRhodiumNode2, BRhodiumNodeSync));

                // create some transactions and push them to the pool
                var trxs = new List<Transaction>();
                foreach (var index in Enumerable.Range(1, 5))
                {
                    var block = BRhodiumNodeSync.FullNode.BlockStoreManager().BlockRepository.GetAsync(BRhodiumNodeSync.FullNode.Chain.GetBlock(index).HashBlock).Result;
                    var prevTrx = block.Transactions.First();
                    var dest = new BitcoinSecret(new Key(), BRhodiumNodeSync.FullNode.Network);

                    Transaction tx = BRhodiumNodeSync.FullNode.Network.Consensus.ConsensusFactory.CreateTransaction();
                    tx.AddInput(new TxIn(new OutPoint(prevTrx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(BRhodiumNodeSync.MinerSecret.PubKey)));
                    tx.AddOutput(new TxOut("25", dest.PubKey.Hash));
                    tx.AddOutput(new TxOut("24", new Key().PubKey.Hash)); // 1 btc fee
                    tx.Sign(BRhodiumNodeSync.FullNode.Network, BRhodiumNodeSync.MinerSecret, false);
                    trxs.Add(tx);
                }
                var options = new ParallelOptions { MaxDegreeOfParallelism = 5 };
                Parallel.ForEach(trxs, options, transaction =>
                {
                    BRhodiumNodeSync.Broadcast(transaction);
                });

                // wait for all nodes to have all trx
                TestHelper.WaitLoop(() => BRhodiumNodeSync.CreateRPCClient().GetRawMempool().Length == 5);

                // the full node should be connected to both nodes
                Assert.True(BRhodiumNodeSync.FullNode.ConnectionManager.ConnectedPeers.Count() >= 2);

                // reset the trickle timer on the full node that has the transactions in the pool
                foreach (var node in BRhodiumNodeSync.FullNode.ConnectionManager.ConnectedPeers) node.Behavior<MempoolBehavior>().NextInvSend = 0;

                TestHelper.WaitLoop(() => BRhodiumNode1.CreateRPCClient().GetRawMempool().Length == 5);
                TestHelper.WaitLoop(() => BRhodiumNode2.CreateRPCClient().GetRawMempool().Length == 5);

                // mine the transactions in the mempool
                BRhodiumNodeSync.GenerateBRhodiumWithMiner(1);
                TestHelper.WaitLoop(() => BRhodiumNodeSync.CreateRPCClient().GetRawMempool().Length == 0);

                // wait for block and mempool to change
                TestHelper.WaitLoop(() => BRhodiumNode1.CreateRPCClient().GetBestBlockHash() == BRhodiumNodeSync.CreateRPCClient().GetBestBlockHash());
                TestHelper.WaitLoop(() => BRhodiumNode2.CreateRPCClient().GetBestBlockHash() == BRhodiumNodeSync.CreateRPCClient().GetBestBlockHash());
                TestHelper.WaitLoop(() => BRhodiumNode1.CreateRPCClient().GetRawMempool().Length == 0);
                TestHelper.WaitLoop(() => BRhodiumNode2.CreateRPCClient().GetRawMempool().Length == 0);
            }
        }
    }

    public class TestMemPoolEntryHelper
    {
        // Default values
        private Money nFee = Money.Zero;

        private long nTime = 0;
        private double dPriority = 0.0;
        private int nHeight = 1;
        private bool spendsCoinbase = false;
        private long sigOpCost = 4;
        private LockPoints lp = new LockPoints();

        public TxMempoolEntry FromTx(Transaction tx, TxMempool pool = null)
        {
            Money inChainValue = (pool != null && pool.HasNoInputsOf(tx)) ? tx.TotalOut : 0;

            return new TxMempoolEntry(tx, this.nFee, this.nTime, this.dPriority, this.nHeight,
                inChainValue, this.spendsCoinbase, this.sigOpCost, this.lp, new PowConsensusOptions());
        }

        // Change the default value
        public TestMemPoolEntryHelper Fee(Money fee) { this.nFee = fee; return this; }

        public TestMemPoolEntryHelper Time(long time)
        {
            this.nTime = time; return this;
        }

        public TestMemPoolEntryHelper Priority(double priority)
        {
            this.dPriority = priority; return this;
        }

        public TestMemPoolEntryHelper Height(int height)
        {
            this.nHeight = height; return this;
        }

        public TestMemPoolEntryHelper SpendsCoinbase(bool flag)
        {
            this.spendsCoinbase = flag; return this;
        }

        public TestMemPoolEntryHelper SigOpsCost(long sigopsCost)
        {
            this.sigOpCost = sigopsCost; return this;
        }
    }
}
