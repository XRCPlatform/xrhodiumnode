using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using BRhodium.Bitcoin.Features.Consensus;
using BRhodium.Bitcoin.Features.Wallet;
using BRhodium.Bitcoin.Features.Wallet.Controllers;
using BRhodium.Bitcoin.Features.Wallet.Interfaces;
using BRhodium.Bitcoin.Features.Wallet.Models;
using BRhodium.Node.IntegrationTests.EnvironmentMockUpHelpers;
using Xunit;

namespace BRhodium.Node.IntegrationTests.Wallet
{
    public class WalletTests
    {
        [Fact]
        public void WalletCanReceiveAndSendCorrectly()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var BRhodiumSender = builder.CreateBRhodiumPowNode();
                var BRhodiumReceiver = builder.CreateBRhodiumPowNode();

                builder.StartAll();
                BRhodiumSender.NotInIBD();
                BRhodiumReceiver.NotInIBD();

                // get a key from the wallet
                var mnemonic1 = BRhodiumSender.FullNode.WalletManager().CreateWallet("123456", "mywallet");
                var mnemonic2 = BRhodiumReceiver.FullNode.WalletManager().CreateWallet("123456", "mywallet");
                Assert.Equal(12, mnemonic1.Words.Length);
                Assert.Equal(12, mnemonic2.Words.Length);
                var addr = BRhodiumSender.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
                var wallet = BRhodiumSender.FullNode.WalletManager().GetWalletByName("mywallet");
                var key = wallet.GetExtendedPrivateKeyForAddress("123456", addr).PrivateKey;

                BRhodiumSender.SetDummyMinerSecret(new BitcoinSecret(key, BRhodiumSender.FullNode.Network));
                var maturity = (int)BRhodiumSender.FullNode.Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;
                BRhodiumSender.GenerateBRhodium(maturity + 5);
                // wait for block repo for block sync to work

                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(BRhodiumSender));

                // the mining should add coins to the wallet
                var total = BRhodiumSender.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Sum(s => s.Transaction.Amount);
                Assert.Equal(105001250000000, total);

                // sync both nodes
                BRhodiumSender.CreateRPCClient().AddNode(BRhodiumReceiver.Endpoint, true);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(BRhodiumReceiver, BRhodiumSender));

                // send coins to the receiver
                var sendto = BRhodiumReceiver.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
                var trx = BRhodiumSender.FullNode.WalletTransactionHandler().BuildTransaction(CreateContext(
                    new WalletAccountReference("mywallet", "account 0"), "123456", sendto.ScriptPubKey, Money.COIN * 100, FeeType.Medium, 1));

                // broadcast to the other node
                BRhodiumSender.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(trx.ToHex()));

                // wait for the trx to arrive
                TestHelper.WaitLoop(() => BRhodiumReceiver.CreateRPCClient().GetRawMempool().Length > 0);
                TestHelper.WaitLoop(() => BRhodiumReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Any());

                var receivetotal = BRhodiumReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * 100, receivetotal);
                Assert.Null(BRhodiumReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").First().Transaction.BlockHeight);

                // generate two new blocks do the trx is confirmed
                BRhodiumSender.GenerateBRhodium(1, new List<Transaction>(new[] { trx.Clone() }));
                BRhodiumSender.GenerateBRhodium(1);

                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(BRhodiumSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(BRhodiumReceiver, BRhodiumSender));

                TestHelper.WaitLoop(() => maturity + 6 == BRhodiumReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").First().Transaction.BlockHeight);
            }
        }

        [Fact]
        public void CanMineAndSendToAddress()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                CoreNode BRhodiumSender = builder.CreateBRhodiumPowNode();
                CoreNode BRhodiumReceiver = builder.CreateBRhodiumPowNode();
                
                builder.StartAll();


                // Move a wallet file to the right folder and restart the wallet manager to take it into account.
                this.InitializeTestWallet(BRhodiumSender.FullNode.DataFolder.WalletPath);
                var walletManager = BRhodiumSender.FullNode.NodeService<IWalletManager>() as WalletManager;
                walletManager.Start();
                var wallet = walletManager.Wallets.FirstOrDefault();
                var account  = wallet.AccountsRoot.FirstOrDefault().Accounts.FirstOrDefault();
                var rpc = BRhodiumSender.CreateRPCClient();
                var addressToMine = account.ExternalAddresses.FirstOrDefault();
                for (int i = 0; i < 10; i++)//handle situations where mining does not return blocs
                {
                    rpc.SendCommand(NBitcoin.RPC.RPCOperations.generate, 1, addressToMine.Address, 100000000000000);
                }
               
                int mined = rpc.GetBlockCount();
                Assert.True(mined > 2);

                // sync both nodes
                rpc.AddNode(BRhodiumReceiver.Endpoint, true);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(BRhodiumReceiver, BRhodiumSender));

                var address = new Key().PubKey.GetAddress(rpc.Network);
                var tx = rpc.SendToAddress(wallet.Name,"password", address.ToString(), Money.Coins(1.0m).ToDecimal(MoneyUnit.XRC));
                Assert.NotNull(tx);
            }
        }

        [Fact]
        public void WalletCanReorg()
        {
            // this test has 4 parts:
            // send first transaction from one wallet to another and wait for it to be confirmed
            // send a second transaction and wait for it to be confirmed
            // connected to a longer chain that couse a reorg back so the second trasnaction is undone
            // mine the second transaction back in to the main chain

            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var BRhodiumSender = builder.CreateBRhodiumPowNode();
                var BRhodiumReceiver = builder.CreateBRhodiumPowNode();
                var BRhodiumReorg = builder.CreateBRhodiumPowNode();

                builder.StartAll();
                BRhodiumSender.NotInIBD();
                BRhodiumReceiver.NotInIBD();
                BRhodiumReorg.NotInIBD();

                // get a key from the wallet
                var mnemonic1 = BRhodiumSender.FullNode.WalletManager().CreateWallet("123456", "mywallet");
                var mnemonic2 = BRhodiumReceiver.FullNode.WalletManager().CreateWallet("123456", "mywallet");
                Assert.Equal(12, mnemonic1.Words.Length);
                Assert.Equal(12, mnemonic2.Words.Length);
                var addr = BRhodiumSender.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
                var wallet = BRhodiumSender.FullNode.WalletManager().GetWalletByName("mywallet");
                var key = wallet.GetExtendedPrivateKeyForAddress("123456", addr).PrivateKey;

                BRhodiumSender.SetDummyMinerSecret(new BitcoinSecret(key, BRhodiumSender.FullNode.Network));
                BRhodiumReorg.SetDummyMinerSecret(new BitcoinSecret(key, BRhodiumSender.FullNode.Network));

                var maturity = (int)BRhodiumSender.FullNode.Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;
                //BRhodiumSender.GenerateBRhodiumWithMiner(maturity + 15);

                var rpc = BRhodiumSender.CreateRPCClient();
                int blockCount = 0;
                while (blockCount < maturity + 15)// there is an unpredictability in mining so ensure 10 blocks mined.
                {
                    BRhodiumSender.GenerateBRhodium(1);
                    blockCount = rpc.GetBlockCount();
                }

                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(BRhodiumSender));


                var currentBestHeight = maturity + 15;

                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(BRhodiumSender));

                // the mining should add coins to the wallet
                var total = BRhodiumSender.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * (((currentBestHeight-1) * 2.5) + 1050000), total);

                // sync all nodes
                BRhodiumReceiver.CreateRPCClient().AddNode(BRhodiumSender.Endpoint, true);
                BRhodiumReceiver.CreateRPCClient().AddNode(BRhodiumReorg.Endpoint, true);
                BRhodiumSender.CreateRPCClient().AddNode(BRhodiumReorg.Endpoint, true);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(BRhodiumReceiver, BRhodiumSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(BRhodiumReceiver, BRhodiumReorg));

                // Build Transaction 1
                // ====================
                // send coins to the receiver
                var sendto = BRhodiumReceiver.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
                var transaction1 = BRhodiumSender.FullNode.WalletTransactionHandler()
                    .BuildTransaction(
                        CreateContext(new WalletAccountReference("mywallet", "account 0"), "123456", sendto.ScriptPubKey, Money.COIN * 100, FeeType.Medium, 1)
                    );

                // broadcast to the other node
                BRhodiumSender.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(transaction1.ToHex()));

                // wait for the trx to arrive
                TestHelper.WaitLoop(() => BRhodiumReceiver.CreateRPCClient().GetRawMempool().Length > 0);
                Assert.NotNull(BRhodiumReceiver.CreateRPCClient().GetRawTransaction(transaction1.GetHash(), false));
                TestHelper.WaitLoop(() => BRhodiumReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Any());

                var receivetotal = BRhodiumReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * 100, receivetotal);
                Assert.Null(BRhodiumReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").First().Transaction.BlockHeight);

                // generate two new blocks so the trx is confirmed
                BRhodiumSender.GenerateBRhodiumWithMiner(1);
                var transaction1MinedHeight = currentBestHeight + 1;
                BRhodiumSender.GenerateBRhodiumWithMiner(1);
                currentBestHeight = currentBestHeight + 2;

                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(BRhodiumSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(BRhodiumReceiver, BRhodiumSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(BRhodiumReceiver, BRhodiumReorg));
                Assert.Equal(currentBestHeight, BRhodiumReceiver.FullNode.Chain.Tip.Height);
                TestHelper.WaitLoop(() => transaction1MinedHeight == BRhodiumReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").First().Transaction.BlockHeight);

                // Build Transaction 2
                // ====================
                // remove the reorg node
                BRhodiumReceiver.CreateRPCClient().RemoveNode(BRhodiumReorg.Endpoint);
                BRhodiumSender.CreateRPCClient().RemoveNode(BRhodiumReorg.Endpoint);
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(BRhodiumReorg));
                var forkblock = BRhodiumReceiver.FullNode.Chain.Tip;

                // send more coins to the wallet
                sendto = BRhodiumReceiver.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
                var transaction2 = BRhodiumSender.FullNode.WalletTransactionHandler().BuildTransaction(CreateContext(new WalletAccountReference("mywallet", "account 0"), "123456", sendto.ScriptPubKey, Money.COIN * 10, FeeType.Medium, 1));
                BRhodiumSender.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(transaction2.ToHex()));
                // wait for the trx to arrive
                TestHelper.WaitLoop(() => BRhodiumReceiver.CreateRPCClient().GetRawMempool().Length > 0);
                Assert.NotNull(BRhodiumReceiver.CreateRPCClient().GetRawTransaction(transaction2.GetHash(), false));
                TestHelper.WaitLoop(() => BRhodiumReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Any());
                var newamount = BRhodiumReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * 110, newamount);
                Assert.Contains(BRhodiumReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet"), b => b.Transaction.BlockHeight == null);

                // mine more blocks so its included in the chain

                BRhodiumSender.GenerateBRhodiumWithMiner(1);
                var transaction2MinedHeight = currentBestHeight + 1;
                BRhodiumSender.GenerateBRhodiumWithMiner(1);
                currentBestHeight = currentBestHeight + 2;
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(BRhodiumSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(BRhodiumReceiver, BRhodiumSender));
                Assert.Equal(currentBestHeight, BRhodiumReceiver.FullNode.Chain.Tip.Height);
                TestHelper.WaitLoop(() => BRhodiumReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Any(b => b.Transaction.BlockHeight == transaction2MinedHeight));

                // create a reorg by mining on two different chains
                // ================================================
                // advance both chains, one chin is longer
                BRhodiumSender.GenerateBRhodiumWithMiner(2);
                BRhodiumReorg.GenerateBRhodiumWithMiner(10);
                currentBestHeight = forkblock.Height + 10;
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(BRhodiumSender));
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(BRhodiumReorg));

                // connect the reorg chain
                BRhodiumReceiver.CreateRPCClient().AddNode(BRhodiumReorg.Endpoint, true);
                BRhodiumSender.CreateRPCClient().AddNode(BRhodiumReorg.Endpoint, true);
                // wait for the chains to catch up
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(BRhodiumReceiver, BRhodiumSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(BRhodiumReceiver, BRhodiumReorg));
                Assert.Equal(currentBestHeight, BRhodiumReceiver.FullNode.Chain.Tip.Height);

                // ensure wallet reorg complete
                TestHelper.WaitLoop(() => BRhodiumReceiver.FullNode.WalletManager().WalletTipHash == BRhodiumReorg.CreateRPCClient().GetBestBlockHash());
                // check the wallet amount was rolled back
                var newtotal = BRhodiumReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Sum(s => s.Transaction.Amount);
                Assert.Equal(receivetotal, newtotal);
                TestHelper.WaitLoop(() => maturity + 16 == BRhodiumReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").First().Transaction.BlockHeight);

                // ReBuild Transaction 2
                // ====================
                // After the reorg transaction2 was returned back to mempool
                BRhodiumSender.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(transaction2.ToHex()));

                TestHelper.WaitLoop(() => BRhodiumReceiver.CreateRPCClient().GetRawMempool().Length > 0);
                // mine the transaction again
                BRhodiumSender.GenerateBRhodiumWithMiner(1);
                transaction2MinedHeight = currentBestHeight + 1;
                BRhodiumSender.GenerateBRhodiumWithMiner(1);
                currentBestHeight = currentBestHeight + 2;

                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(BRhodiumSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(BRhodiumReceiver, BRhodiumSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(BRhodiumReceiver, BRhodiumReorg));
                Assert.Equal(currentBestHeight, BRhodiumReceiver.FullNode.Chain.Tip.Height);
                var newsecondamount = BRhodiumReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Sum(s => s.Transaction.Amount);
                Assert.Equal(newamount, newsecondamount);
                TestHelper.WaitLoop(() => BRhodiumReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Any(b => b.Transaction.BlockHeight == transaction2MinedHeight));
            }
        }

        [Fact]
        public void Given_TheNodeHadAReorg_And_WalletTipIsBehindConsensusTip_When_ANewBlockArrives_Then_WalletCanRecover()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var BRhodiumSender = builder.CreateBRhodiumPowNode();
                var BRhodiumReceiver = builder.CreateBRhodiumPowNode();
                var BRhodiumReorg = builder.CreateBRhodiumPowNode();

                builder.StartAll();
                BRhodiumSender.NotInIBD();
                BRhodiumReceiver.NotInIBD();
                BRhodiumReorg.NotInIBD();

                BRhodiumSender.SetDummyMinerSecret(new BitcoinSecret(new Key(), BRhodiumSender.FullNode.Network));
                BRhodiumReorg.SetDummyMinerSecret(new BitcoinSecret(new Key(), BRhodiumReorg.FullNode.Network));

                var rpc = BRhodiumSender.CreateRPCClient();
                int blockCount = 0;
                while (blockCount < 10)// there is an unpredictability in mining so ensure 10 blocks mined.
                {
                    BRhodiumSender.GenerateBRhodium(1);
                    blockCount = rpc.GetBlockCount();
                }

                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(BRhodiumSender));

                //// sync all nodes
                BRhodiumReceiver.CreateRPCClient().AddNode(BRhodiumSender.Endpoint, true);
                BRhodiumReceiver.CreateRPCClient().AddNode(BRhodiumReorg.Endpoint, true);
                BRhodiumSender.CreateRPCClient().AddNode(BRhodiumReorg.Endpoint, true);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(BRhodiumReceiver, BRhodiumSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(BRhodiumReceiver, BRhodiumReorg));

                // remove the reorg node
                BRhodiumReceiver.CreateRPCClient().RemoveNode(BRhodiumReorg.Endpoint);
                BRhodiumSender.CreateRPCClient().RemoveNode(BRhodiumReorg.Endpoint);
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(BRhodiumReorg));

                // create a reorg by mining on two different chains
                // ================================================
                // advance both chains, one chin is longer
                BRhodiumSender.GenerateBRhodiumWithMiner(2);
                BRhodiumReorg.GenerateBRhodiumWithMiner(10);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(BRhodiumSender));
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(BRhodiumReorg));

                // rewind the wallet in the BRhodiumReceiver node
                (BRhodiumReceiver.FullNode.NodeService<IWalletSyncManager>() as WalletSyncManager).SyncFromHeight(5);

                // connect the reorg chain
                BRhodiumReceiver.CreateRPCClient().AddNode(BRhodiumReorg.Endpoint, true);
                BRhodiumSender.CreateRPCClient().AddNode(BRhodiumReorg.Endpoint, true);
                // wait for the chains to catch up
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(BRhodiumReceiver, BRhodiumSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(BRhodiumReceiver, BRhodiumReorg));
                Assert.Equal(20, BRhodiumReceiver.FullNode.Chain.Tip.Height);

                BRhodiumSender.GenerateBRhodiumWithMiner(5);

                TestHelper.TriggerSync(BRhodiumReceiver);
                TestHelper.TriggerSync(BRhodiumSender);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(BRhodiumReceiver, BRhodiumSender));
                Assert.Equal(25, BRhodiumReceiver.FullNode.Chain.Tip.Height);
            }
        }

        [Fact]
        public void Given_TheNodeHadAReorg_And_ConensusTipIsdifferentFromWalletTip_When_ANewBlockArrives_Then_WalletCanRecover()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var BRhodiumSender = builder.CreateBRhodiumPowNode();
                var BRhodiumReceiver = builder.CreateBRhodiumPowNode();
                var BRhodiumReorg = builder.CreateBRhodiumPowNode();

                builder.StartAll();
                BRhodiumSender.NotInIBD();
                BRhodiumReceiver.NotInIBD();
                BRhodiumReorg.NotInIBD();

                BRhodiumSender.SetDummyMinerSecret(new BitcoinSecret(new Key(), BRhodiumSender.FullNode.Network));
                BRhodiumReorg.SetDummyMinerSecret(new BitcoinSecret(new Key(), BRhodiumReorg.FullNode.Network));
                var rpc = BRhodiumSender.CreateRPCClient();
                int blockCount = 0;
                while (blockCount < 10)// there is an unpredictability in mining so ensure 10 blocks mined.
                {
                    BRhodiumSender.GenerateBRhodiumWithMiner(1);
                    blockCount = rpc.GetBlockCount();
                }
                

                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(BRhodiumSender));

                //// sync all nodes
                BRhodiumReceiver.CreateRPCClient().AddNode(BRhodiumSender.Endpoint, true);
                BRhodiumReceiver.CreateRPCClient().AddNode(BRhodiumReorg.Endpoint, true);
                BRhodiumSender.CreateRPCClient().AddNode(BRhodiumReorg.Endpoint, true);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(BRhodiumReceiver, BRhodiumSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(BRhodiumReceiver, BRhodiumReorg));

                // remove the reorg node and wait for node to be disconnected
                BRhodiumReceiver.CreateRPCClient().RemoveNode(BRhodiumReorg.Endpoint);
                BRhodiumSender.CreateRPCClient().RemoveNode(BRhodiumReorg.Endpoint);
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(BRhodiumReorg));

                // create a reorg by mining on two different chains
                // ================================================
                // advance both chains, one chin is longer
                BRhodiumSender.GenerateBRhodiumWithMiner(2);
                BRhodiumReorg.GenerateBRhodiumWithMiner(10);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(BRhodiumSender));
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(BRhodiumReorg));

                // connect the reorg chain
                BRhodiumReceiver.CreateRPCClient().AddNode(BRhodiumReorg.Endpoint, true);
                BRhodiumSender.CreateRPCClient().AddNode(BRhodiumReorg.Endpoint, true);
                // wait for the chains to catch up
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(BRhodiumReceiver, BRhodiumSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(BRhodiumReceiver, BRhodiumReorg));
                Assert.Equal(20, BRhodiumReceiver.FullNode.Chain.Tip.Height);

                // rewind the wallet in the BRhodiumReceiver node
                (BRhodiumReceiver.FullNode.NodeService<IWalletSyncManager>() as WalletSyncManager).SyncFromHeight(10);

                BRhodiumSender.GenerateBRhodiumWithMiner(5);

                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(BRhodiumReceiver, BRhodiumSender));
                Assert.Equal(25, BRhodiumReceiver.FullNode.Chain.Tip.Height);
            }
        }

        [Fact]
        public void WalletCanCatchupWithBestChain()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var BRhodiumminer = builder.CreateBRhodiumPowNode();

                builder.StartAll();
                BRhodiumminer.NotInIBD();

                // get a key from the wallet
                var mnemonic = BRhodiumminer.FullNode.WalletManager().CreateWallet("123456", "mywallet");
                Assert.Equal(12, mnemonic.Words.Length);
                var addr = BRhodiumminer.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
                var wallet = BRhodiumminer.FullNode.WalletManager().GetWalletByName("mywallet");
                var key = wallet.GetExtendedPrivateKeyForAddress("123456", addr).PrivateKey;

                BRhodiumminer.SetDummyMinerSecret(key.GetBitcoinSecret(BRhodiumminer.FullNode.Network));

                var rpc = BRhodiumminer.CreateRPCClient();
                int blockCount = 0;
                while (blockCount < 10)// there is an unpredictability in mining so ensure 10 blocks mined.
                {
                    BRhodiumminer.GenerateBRhodium(1);
                    blockCount = rpc.GetBlockCount();
                }
                
                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(BRhodiumminer));
                

                // push the wallet back
                BRhodiumminer.FullNode.Services.ServiceProvider.GetService<IWalletSyncManager>().SyncFromHeight(5);

                BRhodiumminer.GenerateBRhodium(5);

                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(BRhodiumminer));
            }
        }

        [Fact]
        public void WalletCanRecoverOnStartup()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var BRhodiumNodeSync = builder.CreateBRhodiumPowNode();
                builder.StartAll();
                BRhodiumNodeSync.NotInIBD();

                // get a key from the wallet
                var mnemonic = BRhodiumNodeSync.FullNode.WalletManager().CreateWallet("123456", "mywallet");
                Assert.Equal(12, mnemonic.Words.Length);
                var addr = BRhodiumNodeSync.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
                var wallet = BRhodiumNodeSync.FullNode.WalletManager().GetWalletByName("mywallet");
                var key = wallet.GetExtendedPrivateKeyForAddress("123456", addr).PrivateKey;

                BRhodiumNodeSync.SetDummyMinerSecret(key.GetBitcoinSecret(BRhodiumNodeSync.FullNode.Network));
                BRhodiumNodeSync.GenerateBRhodium(10);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(BRhodiumNodeSync));

                // set the tip of best chain some blocks in the apst
                BRhodiumNodeSync.FullNode.Chain.SetTip(BRhodiumNodeSync.FullNode.Chain.GetBlock(BRhodiumNodeSync.FullNode.Chain.Height - 5));

                // stop the node it will persist the chain with the reset tip
                BRhodiumNodeSync.FullNode.Dispose();

                var newNodeInstance = builder.CloneBRhodiumNode(BRhodiumNodeSync);

                // load the node, this should hit the block store recover code
                newNodeInstance.Start();

                // check that store recovered to be the same as the best chain.
                Assert.Equal(newNodeInstance.FullNode.Chain.Tip.HashBlock, newNodeInstance.FullNode.WalletManager().WalletTipHash);
            }
        }

        public static TransactionBuildContext CreateContext(WalletAccountReference accountReference, string password,
            NBitcoin.Script destinationScript, Money amount, FeeType feeType, int minConfirmations)
        {
            return new TransactionBuildContext(accountReference,
                new[] { new Recipient { Amount = amount, ScriptPubKey = destinationScript } }.ToList(), password)
            {
                MinConfirmations = minConfirmations,
                FeeType = feeType
            };
        }

        /// <summary>
        /// Copies the test wallet into data folder for node if it isnt' already present.
        /// </summary>
        /// <param name="path">The path of the folder to move the wallet to.</param>
        private void InitializeTestWallet(string path)
        {
            string testWalletPath = Path.Combine(path, "test.wallet.json");
            if (!File.Exists(testWalletPath))
                File.Copy("Data/test.wallet.json", testWalletPath);
        }
    }
}