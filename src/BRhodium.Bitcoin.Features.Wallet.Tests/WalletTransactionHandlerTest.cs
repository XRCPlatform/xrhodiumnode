using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DBreeze.Utils;
using FluentAssertions;
using Moq;
using NBitcoin;
using BRhodium.Node.Configuration;
using BRhodium.Bitcoin.Features.Wallet.Interfaces;
using BRhodium.Node.Tests.Common.Logging;
using BRhodium.Node.Tests.Wallet.Common;
using BRhodium.Node.Utilities;
using Xunit;

namespace BRhodium.Bitcoin.Features.Wallet.Tests
{
    public class WalletTransactionHandlerTest : LogsTestBase
    {
        public readonly string CostlyOpReturnData;
        private Network network;
        public WalletTransactionHandlerTest()
        {
            // adding this data to the transaction output should increase the fee
            // 83 is the max size for the OP_RETURN script => 80 is the max for the content of the script
            byte[] maxQuantityOfBytes = Enumerable.Range(0, 80).Select(Convert.ToByte).ToArray();
            this.CostlyOpReturnData = Encoding.UTF8.GetString(maxQuantityOfBytes);
            this.network = Network.BRhodiumRegTest;
        }

        [Fact]
        public void BuildTransactionThrowsWalletExceptionWhenMoneyIsZero()
        {
            Assert.Throws<WalletException>(() =>
            {
                var walletTransactionHandler = new WalletTransactionHandler(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletFeePolicy>().Object, Network.Main);

                var result = walletTransactionHandler.BuildTransaction(CreateContext(new WalletAccountReference(), "password", new Script(), Money.Zero, FeeType.Medium, 2));
            });
        }

        [Fact]
        public void BuildTransactionNoSpendableTransactionsThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var wallet = WalletTestsHelpers.GenerateBlankWallet("myWallet1", "password");
                wallet.AccountsRoot.ElementAt(0).Accounts.Add(
                    new HdAccount
                    {
                        Name = "account1",
                        ExternalAddresses = new List<HdAddress>(),
                        InternalAddresses = new List<HdAddress>()
                    });

                var chain = new Mock<ConcurrentChain>();
                var block = new BlockHeader();
                chain.Setup(c => c.Tip).Returns(new ChainedHeader(block, block.GetHash(), 1));

                var dataDir = "TestData/WalletTransactionHandlerTest/BuildTransactionNoSpendableTransactionsThrowsWalletException";
                var nodeSettings = new NodeSettings(args: new string[] { $"-datadir={dataDir}", "-regtest" });
                var walletManager = new WalletManager(this.LoggerFactory.Object, this.network, chain.Object, nodeSettings, new Mock<WalletSettings>().Object,
                    new DataFolder(nodeSettings.DataDir), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
                var walletTransactionHandler = new WalletTransactionHandler(this.LoggerFactory.Object, walletManager, new Mock<IWalletFeePolicy>().Object, this.network);

                walletManager.Wallets.AddOrReplace("myWallet1", wallet);

                var walletReference = new WalletAccountReference
                {
                    AccountName = "account1",
                    WalletName = "myWallet1"
                };

                walletTransactionHandler.BuildTransaction(CreateContext(walletReference, "password", new Script(), new Money(500), FeeType.Medium, 2));
            });
        }

        [Fact]
        public void BuildTransactionFeeTooLowThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var walletFeePolicy = new Mock<IWalletFeePolicy>();
                walletFeePolicy.Setup(w => w.GetFeeRate(FeeType.Low))
                    .Returns(new FeeRate(0));

                var wallet = WalletTestsHelpers.GenerateBlankWallet("myWallet1", "password");
                var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
                var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
                var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
                var changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

                var address = new HdAddress
                {
                    Index = 0,
                    HdPath = $"m/44'/0'/0'/0/0",
                    Address = spendingKeys.Address.ToString(),
                    Pubkey = spendingKeys.PubKey.ScriptPubKey,
                    ScriptPubKey = spendingKeys.Address.ScriptPubKey,
                    Transactions = new List<TransactionData>()
                };

                var chain = new ConcurrentChain(wallet.Network);
                WalletTestsHelpers.AddBlocksWithCoinbaseToChain(wallet.Network, chain, address);

                wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
                {
                    Index = 0,
                    Name = "account1",
                    HdPath = "m/44'/0'/0'",
                    ExtendedPubKey = accountKeys.ExtPubKey,
                    ExternalAddresses = new List<HdAddress> { address },
                    InternalAddresses = new List<HdAddress>
                {
                    new HdAddress {
                        Index = 0,
                        HdPath = $"m/44'/0'/0'/1/0",
                        Address = changeKeys.Address.ToString(),
                        Pubkey = changeKeys.PubKey.ScriptPubKey,
                        ScriptPubKey = changeKeys.Address.ScriptPubKey,
                        Transactions = new List<TransactionData>()
                    }
                }
                });

                var dataDir = "TestData/WalletTransactionHandlerTest/BuildTransactionFeeTooLowThrowsWalletException";
                var nodeSettings = new NodeSettings(args: new string[] { $"-datadir={dataDir}", "-regtest" });
                var walletManager = new WalletManager(this.LoggerFactory.Object, this.network, chain, nodeSettings, new Mock<WalletSettings>().Object,
                    new DataFolder(nodeSettings.DataDir), walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
                var walletTransactionHandler = new WalletTransactionHandler(this.LoggerFactory.Object, walletManager, walletFeePolicy.Object, this.network);

                walletManager.Wallets.AddOrReplace("myWallet1", wallet);

                var walletReference = new WalletAccountReference
                {
                    AccountName = "account1",
                    WalletName = "myWallet1"
                };

                walletTransactionHandler.BuildTransaction(CreateContext(walletReference, "password", destinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0));
            });
        }

        [Fact]
        public void BuildTransactionNoChangeAdressesLeftCreatesNewChangeAddress()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var (wallet, accountKeys, destinationKeys, addressTransaction, walletTransactionHandler, walletReference) 
                = this.SetupWallet(dataFolder);

            var context = CreateContext(walletReference, "password", destinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0);
            var transactionResult = walletTransactionHandler.BuildTransaction(context);

            var result = Transaction.Load(transactionResult.ToHex(), Network.Main);
            var expectedChangeAddressKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

            Assert.Single(result.Inputs);
            Assert.Equal(addressTransaction.Id, result.Inputs[0].PrevOut.Hash);

            Assert.Equal(2, result.Outputs.Count);
            var output = result.Outputs[0];
            Assert.Equal((addressTransaction.Amount - context.TransactionFee - 7500), output.Value);
            Assert.Equal(expectedChangeAddressKeys.Address.ScriptPubKey, output.ScriptPubKey);

            output = result.Outputs[1];
            Assert.Equal(7500, output.Value);
            Assert.Equal(destinationKeys.PubKey.ScriptPubKey, output.ScriptPubKey);

            Assert.Equal(addressTransaction.Amount - context.TransactionFee, result.TotalOut);
            Assert.NotNull(transactionResult.GetHash());
            Assert.Equal(result.GetHash(), transactionResult.GetHash());
        }

        [Fact]
        public void BuildTransaction_When_OpReturnData_Is_Empty_Should_Not_Add_Extra_Output()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var (wallet, accountKeys, destinationKeys, addressTransaction, walletTransactionHandler, walletReference) 
                = this.SetupWallet(dataFolder);

            var opReturnData = "";

            var context = CreateContext(walletReference, "password", destinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0, opReturnData);
            var transactionResult = walletTransactionHandler.BuildTransaction(context);

            transactionResult.Outputs.Where(o => o.ScriptPubKey.IsUnspendable).Should()
                .BeEmpty("because opReturnData is empty");
        }
        
        [Fact]
        public void BuildTransaction_When_OpReturnData_Is_Null_Should_Not_Add_Extra_Output()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var (wallet, accountKeys, destinationKeys, addressTransaction, walletTransactionHandler, walletReference) 
                = this.SetupWallet(dataFolder);

            string opReturnData = null;

            var context = CreateContext(walletReference, "password", destinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0, opReturnData);
            var transactionResult = walletTransactionHandler.BuildTransaction(context);

            transactionResult.Outputs.Where(o => o.ScriptPubKey.IsUnspendable).Should()
                .BeEmpty("because opReturnData is null");
        }


        [Fact]
        public void BuildTransaction_When_OpReturnData_Is_Neither_Null_Nor_Empty_Should_Add_Extra_Output_With_Data()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var (wallet, accountKeys, destinationKeys, addressTransaction, walletTransactionHandler, walletReference) 
                = this.SetupWallet(dataFolder);

            var opReturnData = "some extra transaction info";
            var expectedBytes = Encoding.UTF8.GetBytes(opReturnData);

            var context = CreateContext(walletReference, "password", destinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0, opReturnData);
            var transactionResult = walletTransactionHandler.BuildTransaction(context);

            IEnumerable<TxOut> unspendableOutputs = transactionResult.Outputs.Where(o => o.ScriptPubKey.IsUnspendable).ToList();
            unspendableOutputs.Count().Should().Be(1);
            unspendableOutputs.Single().Value.Should().Be(Money.Zero);

            var ops = unspendableOutputs.Single().ScriptPubKey.ToOps();
            ops.Count().Should().Be(2);
            ops.First().Code.Should().Be(OpcodeType.OP_RETURN);
            ops.Last().PushData.Should().BeEquivalentTo(expectedBytes);

        }

        [Fact]
        public void BuildTransaction_When_OpReturnData_Is_Too_Long_Should_Fail_With_Helpful_Message()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var (wallet, accountKeys, destinationKeys, addressTransaction, walletTransactionHandler, walletReference) 
                = this.SetupWallet(dataFolder);

            var eightyOneBytes = Encoding.UTF8.GetBytes(this.CostlyOpReturnData).Concat(Convert.ToByte(1));
            var tooLongOpReturnString = Encoding.UTF8.GetString(eightyOneBytes);

            var context = CreateContext(walletReference, "password", destinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0, tooLongOpReturnString);
            new Action(() => walletTransactionHandler.BuildTransaction(context))
                .Should().Throw<ArgumentOutOfRangeException>()
                .And.Message.Should().Contain(" maximum size of 83");         

        }

        private (Wallet wallet, (ExtKey ExtKey, string ExtPubKey) accountKeys, (PubKey PubKey, BitcoinPubKeyAddress Address)
            destinationKeys, TransactionData addressTransaction, WalletTransactionHandler walletTransactionHandler,
            WalletAccountReference walletReference) SetupWallet(DataFolder dataFolder)
        {

            var wallet = WalletTestsHelpers.GenerateBlankWallet("myWallet1", "password");
            var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");

            var address = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            var chain = new ConcurrentChain(wallet.Network);
            WalletTestsHelpers.AddBlocksWithCoinbaseToChain(wallet.Network, chain, address);
            var addressTransaction = address.Transactions.First();

            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "account1",
                HdPath = "m/44'/0'/0'",
                ExtendedPubKey = accountKeys.ExtPubKey,
                ExternalAddresses = new List<HdAddress> {address},
                InternalAddresses = new List<HdAddress>()
            });

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetFeeRate(FeeType.Low))
                .Returns(new FeeRate(20000));
            var nodeSettings = new NodeSettings(args: new string[] { $"-datadir={dataFolder}", "-regtest" });
            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.BRhodiumRegTest, chain, nodeSettings,
                new Mock<WalletSettings>().Object, dataFolder,
                walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var walletTransactionHandler =
                new WalletTransactionHandler(this.LoggerFactory.Object, walletManager, walletFeePolicy.Object, this.network);

            walletManager.Wallets.AddOrReplace("myWallet1", wallet);
            

            var walletReference = new WalletAccountReference
            {
                AccountName = "account1",
                WalletName = "myWallet1"
            };
            return (wallet, accountKeys, destinationKeys, addressTransaction, walletTransactionHandler, walletReference);
        }

        [Fact]
        public void FundTransaction_Given__a_wallet_has_enough_inputs__When__adding_inputs_to_an_existing_transaction__Then__the_transaction_is_funded_successfully()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var wallet = WalletTestsHelpers.GenerateBlankWallet("myWallet1", "password");
            var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            var destinationKeys1 = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
            var destinationKeys2 = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/2");
            var destinationKeys3 = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/3");

            var address = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            // wallet with 4 coinbase outputs of 50 = 200 Bitcoin
            var chain = new ConcurrentChain(wallet.Network);
            WalletTestsHelpers.AddBlocksWithCoinbaseToChain(wallet.Network, chain, address, 4);

            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "account1",
                HdPath = "m/44'/0'/0'",
                ExtendedPubKey = accountKeys.ExtPubKey,
                ExternalAddresses = new List<HdAddress> { address },
                InternalAddresses = new List<HdAddress>()
            });

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetFeeRate(FeeType.Low)).Returns(new FeeRate(20000));
            var overrideFeeRate = new FeeRate(20000);
            var nodeSettings = new NodeSettings(args: new string[] { $"-datadir={dataFolder}", "-regtest" });
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.network, chain, nodeSettings, new Mock<WalletSettings>().Object, 
                dataFolder, walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var walletTransactionHandler = new WalletTransactionHandler(this.LoggerFactory.Object, walletManager, walletFeePolicy.Object, this.network);

            walletManager.Wallets.AddOrReplace("myWallet1", wallet);

            var walletReference = new WalletAccountReference
            {
                AccountName = "account1",
                WalletName = "myWallet1"
            };

            // create a trx with 3 outputs 1050000 + 2.5 + 2 = 1050004.5 XRC
            var context = new TransactionBuildContext(walletReference,
                new[]
                {
                    new Recipient { Amount = new Money(1050000, MoneyUnit.XRC), ScriptPubKey = destinationKeys1.PubKey.ScriptPubKey },
                    new Recipient { Amount = new Money((decimal)2.5, MoneyUnit.XRC), ScriptPubKey = destinationKeys2.PubKey.ScriptPubKey },
                    new Recipient { Amount = new Money(2, MoneyUnit.XRC), ScriptPubKey = destinationKeys3.PubKey.ScriptPubKey }
                }
                .ToList(), "password")
            {
                MinConfirmations = 0,
                FeeType = FeeType.Low
            };

            var fundTransaction = walletTransactionHandler.BuildTransaction(context);
            Assert.Equal(4, fundTransaction.Inputs.Count); // 4 inputs
            Assert.Equal(4, fundTransaction.Outputs.Count); // 3 outputs with change

            // remove the change output
            fundTransaction.Outputs.Remove(fundTransaction.Outputs.First(f => f.ScriptPubKey == context.ChangeAddress.ScriptPubKey));
            // remove 2 inputs they will be added back by fund transaction
            fundTransaction.Inputs.RemoveAt(2);
            fundTransaction.Inputs.RemoveAt(1);
            Assert.Equal(2, fundTransaction.Inputs.Count); // 2 inputs

            var fundTransactionClone = fundTransaction.Clone();
            var fundContext = new TransactionBuildContext(walletReference, new List<Recipient>(), "password")
            {
                MinConfirmations = 0,
                FeeType = FeeType.Low
            };

            fundContext.OverrideFeeRate = overrideFeeRate;
            walletTransactionHandler.FundTransaction(fundContext, fundTransaction);

            foreach (var input in fundTransactionClone.Inputs) // all original inputs are still in the trx
                Assert.Contains(fundTransaction.Inputs, a => a.PrevOut == input.PrevOut);

            Assert.Equal(4, fundTransaction.Inputs.Count); // we expect 4 inputs
            Assert.Equal(4, fundTransaction.Outputs.Count); // we expect 4 outputs (2.99984640 = change)
            Assert.Equal(new Money((decimal)1050007.5, MoneyUnit.XRC) - fundContext.TransactionFee, fundTransaction.TotalOut);

            Assert.Contains(fundTransaction.Outputs, a => a.ScriptPubKey == destinationKeys1.PubKey.ScriptPubKey);
            Assert.Contains(fundTransaction.Outputs, a => a.ScriptPubKey == destinationKeys2.PubKey.ScriptPubKey);
            Assert.Contains(fundTransaction.Outputs, a => a.ScriptPubKey == destinationKeys3.PubKey.ScriptPubKey);
        }

        [Fact]
        public void Given_AnInvalidAccountIsUsed_When_GetMaximumSpendableAmountIsCalled_Then_AnExceptionIsThrown()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var nodeSettings = new NodeSettings(args: new string[] { $"-datadir={dataFolder}", "-regtest" });
            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.BRhodiumRegTest, new Mock<ConcurrentChain>().Object, nodeSettings, new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var walletTransactionHandler = new WalletTransactionHandler(this.LoggerFactory.Object, walletManager, It.IsAny<WalletFeePolicy>(), this.network);

            var wallet = WalletTestsHelpers.CreateWallet("wallet1");
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                CoinType = (CoinType)this.network.Consensus.CoinType,
                Accounts = new List<HdAccount> { WalletTestsHelpers.CreateAccount("account 1") }
            });
            walletManager.Wallets.AddOrReplace("wallet1", wallet);

            Exception ex = Assert.Throws<WalletException>(() => walletTransactionHandler.GetMaximumSpendableAmount(new WalletAccountReference("wallet1", "noaccount"), FeeType.Low, true));
            Assert.NotNull(ex);
            Assert.NotNull(ex.Message);
            Assert.NotEqual(string.Empty, ex.Message);
            Assert.IsType<WalletException>(ex);
        }

        [Fact]
        public void Given_GetMaximumSpendableAmountIsCalled_When_ThereAreNoSpendableFound_Then_MaxAmountReturnsAsZero()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var nodeSettings = new NodeSettings(args: new string[] { $"-datadir={dataFolder}", "-regtest" });
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.network, new ConcurrentChain(this.network), nodeSettings, new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var walletTransactionHandler = new WalletTransactionHandler(this.LoggerFactory.Object, walletManager, It.IsAny<WalletFeePolicy>(), this.network);

            HdAccount account = WalletTestsHelpers.CreateAccount("account 1");

            HdAddress accountAddress1 = WalletTestsHelpers.CreateAddress();
            accountAddress1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(15000), 1, new SpendingDetails()));
            accountAddress1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(2), new Money(10000), 1, new SpendingDetails()));

            HdAddress accountAddress2 = WalletTestsHelpers.CreateAddress();
            accountAddress2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(3), new Money(20000), 3, new SpendingDetails()));
            accountAddress2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(4), new Money(120000), 4, new SpendingDetails()));

            account.ExternalAddresses.Add(accountAddress1);
            account.InternalAddresses.Add(accountAddress2);

            var wallet = WalletTestsHelpers.CreateWallet("wallet1");
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                CoinType = (CoinType)this.network.Consensus.CoinType,
                Accounts = new List<HdAccount> { account }
            });

            walletManager.Wallets.AddOrReplace("wallet1", wallet);

            (Money max, Money fee) result = walletTransactionHandler.GetMaximumSpendableAmount(new WalletAccountReference("wallet1", "account 1"), FeeType.Low, true);
            Assert.Equal(Money.Zero, result.max);
            Assert.Equal(Money.Zero, result.fee);
        }

        [Fact]
        public void Given_GetMaximumSpendableAmountIsCalledForConfirmedTransactions_When_ThereAreNoConfirmedSpendableFound_Then_MaxAmountReturnsAsZero()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var nodeSettings = new NodeSettings(args: new string[] { $"-datadir={dataFolder}", "-regtest" });
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.network, new ConcurrentChain(this.network), nodeSettings, new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var walletTransactionHandler = new WalletTransactionHandler(this.LoggerFactory.Object, walletManager, It.IsAny<WalletFeePolicy>(), this.network);

            HdAccount account = WalletTestsHelpers.CreateAccount("account 1");

            HdAddress accountAddress1 = WalletTestsHelpers.CreateAddress();
            accountAddress1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(15000), null));
            accountAddress1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(2), new Money(10000), null));

            HdAddress accountAddress2 = WalletTestsHelpers.CreateAddress();
            accountAddress2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(3), new Money(20000), null));
            accountAddress2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(4), new Money(120000), null));

            account.ExternalAddresses.Add(accountAddress1);
            account.InternalAddresses.Add(accountAddress2);

            var wallet = WalletTestsHelpers.CreateWallet("wallet1");
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                CoinType = (CoinType)this.network.Consensus.CoinType,
                Accounts = new List<HdAccount> { account }
            });

            walletManager.Wallets.AddOrReplace("wallet1", wallet);

            (Money max, Money fee) result = walletTransactionHandler.GetMaximumSpendableAmount(new WalletAccountReference("wallet1", "account 1"), FeeType.Low, false);
            Assert.Equal(Money.Zero, result.max);
            Assert.Equal(Money.Zero, result.fee);
        }

        [Fact]
        public void Given_GetMaximumSpendableAmountIsCalled_When_ThereAreNoConfirmedSpendableFound_Then_MaxAmountReturnsAsTheSumOfUnconfirmedTxs()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var nodeSettings = new NodeSettings(args: new string[] { $"-datadir={dataFolder}", "-regtest" });
            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetFeeRate(FeeType.Low)).Returns(new FeeRate(20000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.network, new ConcurrentChain(this.network), nodeSettings, new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var walletTransactionHandler = new WalletTransactionHandler(this.LoggerFactory.Object, walletManager, walletFeePolicy.Object, this.network);

            HdAccount account = WalletTestsHelpers.CreateAccount("account 1");

            HdAddress accountAddress1 = WalletTestsHelpers.CreateAddress();
            accountAddress1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(15000), null, null, null, new Key().ScriptPubKey));
            accountAddress1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(2), new Money(10000), null, null, null, new Key().ScriptPubKey));

            HdAddress accountAddress2 = WalletTestsHelpers.CreateAddress();
            accountAddress2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(3), new Money(20000), null, null, null, new Key().ScriptPubKey));
            accountAddress2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(4), new Money(120000), null, null, null, new Key().ScriptPubKey));

            account.ExternalAddresses.Add(accountAddress1);
            account.InternalAddresses.Add(accountAddress2);

            var wallet = WalletTestsHelpers.CreateWallet("wallet1");
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                CoinType = (CoinType)this.network.Consensus.CoinType,
                Accounts = new List<HdAccount> { account }
            });

            walletManager.Wallets.AddOrReplace("wallet1", wallet);

            (Money max, Money fee) result = walletTransactionHandler.GetMaximumSpendableAmount(new WalletAccountReference("wallet1", "account 1"), FeeType.Low, true);
            Assert.Equal(new Money(165000), result.max + result.fee);
        }

        [Fact]
        public void Given_GetMaximumSpendableAmountIsCalled_When_ThereAreNoTransactions_Then_MaxAmountReturnsAsZero()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var nodeSettings = new NodeSettings(args: new string[] { $"-datadir={dataFolder}", "-regtest" });
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.network, new ConcurrentChain(this.network), nodeSettings, new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var walletTransactionHandler = new WalletTransactionHandler(this.LoggerFactory.Object, walletManager, It.IsAny<WalletFeePolicy>(), this.network);
            HdAccount account = WalletTestsHelpers.CreateAccount("account 1");
            HdAddress accountAddress1 = WalletTestsHelpers.CreateAddress();
            HdAddress accountAddress2 = WalletTestsHelpers.CreateAddress();
            account.ExternalAddresses.Add(accountAddress1);
            account.InternalAddresses.Add(accountAddress2);

            var wallet = WalletTestsHelpers.CreateWallet("wallet1");
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                CoinType = (CoinType)this.network.Consensus.CoinType,
                Accounts = new List<HdAccount> { account }
            });

            walletManager.Wallets.AddOrReplace("wallet1", wallet);

            (Money max, Money fee) result = walletTransactionHandler.GetMaximumSpendableAmount(new WalletAccountReference("wallet1", "account 1"), FeeType.Low, true);
            Assert.Equal(Money.Zero, result.max);
            Assert.Equal(Money.Zero, result.fee);
        }

        /// <summary>
        /// Tests the <see cref="WalletTransactionHandler.EstimateFee(TransactionBuildContext)"/> method by
        /// comparing it's fee calculation with the transaction fee computed for the same tx in the
        /// <see cref="WalletTransactionHandler.BuildTransaction(TransactionBuildContext)"/> method.
        /// </summary>
        [Fact]
        public void EstimateFeeWithLowFeeMatchesBuildTxLowFee()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var (wallet, accountKeys, destinationKeys, addressTransaction, walletTransactionHandler, walletReference) 
                = this.SetupWallet(dataFolder);

            // Context to build requires password in order to sign transaction.
            TransactionBuildContext buildContext = CreateContext(walletReference, "password", destinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0);
            walletTransactionHandler.BuildTransaction(buildContext);

            // Context for estimate does not need password.
            TransactionBuildContext estimateContext = CreateContext(walletReference, null, destinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0);
            Money fee = walletTransactionHandler.EstimateFee(estimateContext);

            Assert.Equal(fee, buildContext.TransactionFee);
        }

        /// <summary>
        /// Tests the <see cref="WalletTransactionHandler.EstimateFee(TransactionBuildContext)"/> method by
        /// comparing it's fee calculation with the transaction fee computed for the same tx in the
        /// <see cref="WalletTransactionHandler.BuildTransaction(TransactionBuildContext)"/> method.
        /// </summary>
        [Fact]
        public void EstimateFee_WithLowFee_Matches_BuildTransaction_WithLowFee_With_Long_OpReturnData_added()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var (wallet, accountKeys, destinationKeys, addressTransaction, walletTransactionHandler, walletReference)
                = this.SetupWallet(dataFolder);

            // Context to build requires password in order to sign transaction.
            TransactionBuildContext buildContext = CreateContext(walletReference, "password", destinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0, this.CostlyOpReturnData);
            walletTransactionHandler.BuildTransaction(buildContext);

            // Context for estimate does not need password.
            TransactionBuildContext estimateContext = CreateContext(walletReference, null, destinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0, this.CostlyOpReturnData);
            Money feeEstimate = walletTransactionHandler.EstimateFee(estimateContext);

            feeEstimate.Should().Be(buildContext.TransactionFee);
        }

        /// <summary>
        /// Make sure that if you add data to the transaction in an OP_RETURN the estimated fee increases
        /// </summary>
        [Fact]
        public void EstimateFee_Without_OpReturnData_Should_Be_Less_Than_Estimate_Fee_With_Costly_OpReturnData()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var (wallet, accountKeys, destinationKeys, addressTransaction, walletTransactionHandler, walletReference) 
                = this.SetupWallet(dataFolder);

            // Context with OpReturnData
            var estimateContextWithOpReturn = CreateContext(walletReference, null, destinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0, this.CostlyOpReturnData);
            var feeEstimateWithOpReturn = walletTransactionHandler.EstimateFee(estimateContextWithOpReturn);

            // Context without OpReturnData
            var estimateContextWithoutOpReturn = CreateContext(walletReference, null, destinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0, null);
            var feeEstimateWithoutOpReturn = walletTransactionHandler.EstimateFee(estimateContextWithoutOpReturn);

            feeEstimateWithOpReturn.Should().NotBe(feeEstimateWithoutOpReturn);
            feeEstimateWithoutOpReturn.Satoshi.Should().BeLessThan(feeEstimateWithOpReturn.Satoshi);
        }

        /// <summary>
        /// Make sure that if you add data to the transaction in an OP_RETURN the actual fee increases
        /// </summary>
        [Fact]
        public void Actual_Fee_Without_OpReturnData_Should_Be_Less_Than_Actual_Fee_With_Costly_OpReturnData()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var (wallet, accountKeys, destinationKeys, addressTransaction, walletTransactionHandler, walletReference)
                = this.SetupWallet(dataFolder);

            // Context with OpReturnData
            var contextWithOpReturn = CreateContext(walletReference, "password", destinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0, this.CostlyOpReturnData);
            walletTransactionHandler.BuildTransaction(contextWithOpReturn);
            
            // Context without OpReturnData
            var contextWithoutOpReturn = CreateContext(walletReference, "password", destinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0, null);
            walletTransactionHandler.BuildTransaction(contextWithoutOpReturn);

            contextWithoutOpReturn.TransactionFee.Should().NotBe(contextWithOpReturn.TransactionFee);
            contextWithoutOpReturn.TransactionFee.Satoshi.Should().BeLessThan(contextWithOpReturn.TransactionFee.Satoshi);
        }


        public static TransactionBuildContext CreateContext(WalletAccountReference accountReference, string password,
            Script destinationScript, Money amount, FeeType feeType, int minConfirmations, string opReturnData = null)
        {
            return new TransactionBuildContext(accountReference,
                new[] { new Recipient { Amount = amount, ScriptPubKey = destinationScript } }.ToList(), password, opReturnData)
            {
                MinConfirmations = minConfirmations,
                FeeType = feeType
            };
        }
    }
}
