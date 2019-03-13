using System;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using BRhodium.Bitcoin.Features.Consensus;
using BRhodium.Bitcoin.Features.Wallet;
using BRhodium.Bitcoin.Features.Wallet.Controllers;
using BRhodium.Bitcoin.Features.Wallet.Models;
using BRhodium.Node.IntegrationTests.Builders;
using BRhodium.Node.IntegrationTests.EnvironmentMockUpHelpers;
using Xunit.Abstractions;

namespace BRhodium.Node.IntegrationTests.BlockStore
{
    public partial class ProofOfWorkSpendingSpecification
    {
        private const string SendingWalletName = "sending wallet";
        private const string ReceivingWalletName = "receiving wallet";
        private const string WalletPassword = "123456";
        private const string AccountName = "account 0";
        private CoreNode sendingBRhodiumBitcoinNode;
        private CoreNode receivingBRhodiumBitcoinNode;
        private int coinbaseMaturity;
        private Exception caughtException;
        private Transaction lastTransaction;
        private SharedSteps sharedSteps;
        private NodeGroupBuilder nodeGroupBuilder;

        public ProofOfWorkSpendingSpecification(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        protected override void BeforeTest()
        {
            this.nodeGroupBuilder = new NodeGroupBuilder(this.CurrentTest.DisplayName);
            this.sharedSteps = new SharedSteps();
        }

        protected override void AfterTest()
        {
            this.nodeGroupBuilder.Dispose();
        }

        private void a_sending_and_receiving_BRhodium_bitcoin_node_and_wallet()
        {
            var nodeGroup = this.nodeGroupBuilder
                .BRhodiumPowNode("sending").Start().NotInIBD()
                .WithWallet(SendingWalletName, WalletPassword)
                .BRhodiumPowNode("receiving").Start().NotInIBD()
                .WithWallet(ReceivingWalletName, WalletPassword)
                .WithConnections()
                .Connect("sending", "receiving")
                .AndNoMoreConnections()
                .Build();

            this.sendingBRhodiumBitcoinNode = nodeGroup["sending"];
            this.receivingBRhodiumBitcoinNode = nodeGroup["receiving"];

            this.coinbaseMaturity = (int)this.sendingBRhodiumBitcoinNode.FullNode
                .Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;
        }

        private void a_block_is_mined_creating_spendable_coins()
        {
            this.sharedSteps.MineBlocks(1, this.sendingBRhodiumBitcoinNode, AccountName, SendingWalletName, WalletPassword);
        }

        private void more_blocks_mined_to_just_BEFORE_maturity_of_original_block()// does not work well with coinbase maturity 1
        {
            this.sharedSteps.MineBlocks(this.coinbaseMaturity - 1, this.sendingBRhodiumBitcoinNode, AccountName, SendingWalletName, WalletPassword);
        }

        private void more_blocks_mined_to_just_AFTER_maturity_of_original_block()
        {
            this.sharedSteps.MineBlocks(this.coinbaseMaturity, this.sendingBRhodiumBitcoinNode, AccountName, SendingWalletName, WalletPassword);

        }

        private void spending_the_coins_from_original_block()
        {
            var sendtoAddress = this.receivingBRhodiumBitcoinNode.FullNode.WalletManager()
                .GetUnusedAddresses(new WalletAccountReference(ReceivingWalletName, AccountName), 2).ElementAt(1);

            try
            {
                var transactionBuildContext = SharedSteps.CreateTransactionBuildContext(
                    SendingWalletName,
                    AccountName,
                    WalletPassword,
                    new[] {
                        new Recipient {
                            Amount = Money.COIN * 1,
                            ScriptPubKey = sendtoAddress.ScriptPubKey
                        }
                    },
                    FeeType.Medium, 1);

                this.lastTransaction = this.sendingBRhodiumBitcoinNode.FullNode.WalletTransactionHandler()
                    .BuildTransaction(transactionBuildContext);

                this.sendingBRhodiumBitcoinNode.FullNode.NodeService<WalletController>()
                    .SendTransaction(new SendTransactionRequest(this.lastTransaction.ToHex()));
            }
            catch (Exception exception)
            {
                this.caughtException = exception;
            }
        }

        private void the_transaction_is_rejected_from_the_mempool()
        {
            this.caughtException.Should().BeOfType<WalletException>();

            var walletException = (WalletException)this.caughtException;
            walletException.Message.Should().Be("No spendable transactions found.");

            this.ResetCaughtException();
        }

        private void the_transaction_is_put_in_the_mempool()
        {
            var tx = this.sendingBRhodiumBitcoinNode.FullNode.MempoolManager().GetTransaction(this.lastTransaction.GetHash()).GetAwaiter().GetResult();
            tx.GetHash().Should().Be(this.lastTransaction.GetHash());
            this.caughtException.Should().BeNull();
        }

        private void ResetCaughtException()
        {
            this.caughtException = null;
        }
    }
}