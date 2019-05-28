using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Newtonsoft.Json;
using BRhodium.Node.Configuration;
using BRhodium.Bitcoin.Features.Wallet.Interfaces;
using BRhodium.Node.Tests.Common.Logging;
using BRhodium.Node.Tests.Wallet.Common;
using BRhodium.Node.Utilities;
using BRhodium.Node.Utilities.JsonConverters;
using Xunit;
using System.Text;

namespace BRhodium.Bitcoin.Features.Wallet.Tests
{
    public class WalletManagerTest : LogsTestBase, IClassFixture<WalletFixture>
    {
        private readonly WalletFixture walletFixture;

        public WalletManagerTest(WalletFixture walletFixture)
        {
            this.walletFixture = walletFixture;
        }

        /// <summary>
        /// This is more of an integration test to verify fields are filled correctly. This is what I could confirm.
        /// </summary>
        [Fact]
        public void CreateWalletWithoutPassphraseOrMnemonicCreatesWalletUsingPassword()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var chain = new ConcurrentChain(Network.BRhodiumMain);
            var nonce = RandomUtils.GetUInt32();
            var block = new Block();
            block.AddTransaction(new Transaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Genesis.HashBlock;
            block.Header.Nonce = nonce;
            block.Header.BlockTime = DateTimeOffset.Now;
            chain.SetTip(block.Header);

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.BRhodiumMain, chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var password = "test";

            // create the wallet
            var mnemonic = walletManager.CreateWallet(password, "mywallet");

            // assert it has saved it to disk and has been created correctly.
            var expectedWallet = walletManager.GetWalletByName("mywallet");
            //var actualWallet = walletManager.Wallets.Values.ElementAt(0);

            Assert.Equal("mywallet", expectedWallet.Name);
            Assert.Equal(Network.BRhodiumMain, expectedWallet.Network);

            Assert.Equal(1, expectedWallet.AccountsRoot.Count);

            for (var i = 0; i < expectedWallet.AccountsRoot.Count; i++)
            {
                Assert.Equal(CoinType.BRhodium, expectedWallet.AccountsRoot.ElementAt(i).CoinType);
                Assert.Equal(1, expectedWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHeight);
                Assert.Equal(block.GetHash(), expectedWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHash);


                var accountRoot = expectedWallet.AccountsRoot.ElementAt(i);
                Assert.Equal(1, accountRoot.Accounts.Count);

                for (var j = 0; j < accountRoot.Accounts.Count; j++)
                {
                    var actualAccount = accountRoot.Accounts.ElementAt(j);
                    Assert.Equal($"account {j}", actualAccount.Name);
                    Assert.Equal(j, actualAccount.Index);
                    Assert.Equal($"m/44'/10291'/{j}'", actualAccount.HdPath);

                    var extKey = new ExtKey(Key.Parse(expectedWallet.EncryptedSeed, "test", expectedWallet.Network), expectedWallet.ChainCode);
                    var expectedExtendedPubKey = extKey.Derive(new KeyPath($"m/44'/10291'/{j}'")).Neuter().ToString(expectedWallet.Network);
                    Assert.Equal(expectedExtendedPubKey, actualAccount.ExtendedPubKey);

                    Assert.Equal(20, actualAccount.InternalAddresses.Count);

                    for (var k = 0; k < actualAccount.InternalAddresses.Count; k++)
                    {
                        var actualAddress = actualAccount.InternalAddresses.ElementAt(k);
                        var expectedAddressPubKey = ExtPubKey.Parse(expectedExtendedPubKey).Derive(new KeyPath($"1/{k}")).PubKey;
                        var expectedAddress = expectedAddressPubKey.GetAddress(expectedWallet.Network);
                        Assert.Equal(k, actualAddress.Index);
                        Assert.Equal(expectedAddress.ScriptPubKey, actualAddress.ScriptPubKey);
                        Assert.Equal(expectedAddress.ToString(), actualAddress.Address);
                        Assert.Equal(expectedAddressPubKey.ScriptPubKey, actualAddress.Pubkey);
                        Assert.Equal($"m/44'/10291'/{j}'/1/{k}", actualAddress.HdPath);
                        Assert.Equal(0, actualAddress.Transactions.Count);
                    }

                    Assert.Equal(20, actualAccount.ExternalAddresses.Count);
                    for (var l = 0; l < actualAccount.ExternalAddresses.Count; l++)
                    {
                        var actualAddress = actualAccount.ExternalAddresses.ElementAt(l);
                        var expectedAddressPubKey = ExtPubKey.Parse(expectedExtendedPubKey).Derive(new KeyPath($"0/{l}")).PubKey;
                        var expectedAddress = expectedAddressPubKey.GetAddress(expectedWallet.Network);
                        Assert.Equal(l, actualAddress.Index);
                        Assert.Equal(expectedAddress.ScriptPubKey, actualAddress.ScriptPubKey);
                        Assert.Equal(expectedAddress.ToString(), actualAddress.Address);
                        Assert.Equal(expectedAddressPubKey.ScriptPubKey, actualAddress.Pubkey);
                        Assert.Equal($"m/44'/10291'/{j}'/0/{l}", actualAddress.HdPath);
                        Assert.Equal(0, actualAddress.Transactions.Count);
                    }
                }
            }

            Assert.Equal(2, expectedWallet.BlockLocator.Count);

            var expectedBlockHash = block.GetHash();
            Assert.Equal(expectedBlockHash, expectedWallet.BlockLocator.ElementAt(0));

            expectedBlockHash = chain.Genesis.HashBlock;
            Assert.Equal(expectedBlockHash, expectedWallet.BlockLocator.ElementAt(1));

            string fakePassphrase = Convert.ToBase64String(Encoding.UTF8.GetBytes(password));
            Assert.Equal(expectedWallet.EncryptedSeed, mnemonic.DeriveExtKey(fakePassphrase).PrivateKey.GetEncryptedBitcoinSecret(password, Network.BRhodiumMain).ToWif());
        }

        /// <summary>
        /// This is more of an integration test to verify fields are filled correctly. This is what I could confirm.
        /// </summary>
        [Fact]
        public void CreateWalletWithPasswordAndPassphraseCreatesWalletUsingPasswordAndPassphrase()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var chain = new ConcurrentChain(Network.BRhodiumMain);
            var nonce = RandomUtils.GetUInt32();
            var block = new Block();
            block.AddTransaction(new Transaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Genesis.HashBlock;
            block.Header.Nonce = nonce;
            block.Header.BlockTime = DateTimeOffset.Now;
            chain.SetTip(block.Header);

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.BRhodiumMain, chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var password = "test";
            var passphrase = "this is my magic passphrase";

            // create the wallet
            var mnemonic = walletManager.CreateWallet(password, "mywallet", passphrase);
            passphrase = Convert.ToBase64String(Encoding.UTF8.GetBytes(passphrase));//wallet passphrases are base64 encoded inside CreateWallet method

            // assert it has saved it to disk and has been created correctly.
            var expectedWallet = walletManager.GetWallet("mywallet");
  
            Assert.Equal("mywallet", expectedWallet.Name);
            Assert.Equal(Network.BRhodiumMain, expectedWallet.Network);

            Assert.Equal(1, expectedWallet.AccountsRoot.Count);


            for (var i = 0; i < expectedWallet.AccountsRoot.Count; i++)
            {
                Assert.Equal(CoinType.BRhodium, expectedWallet.AccountsRoot.ElementAt(i).CoinType);
                Assert.Equal(1, expectedWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHeight);
                Assert.Equal(block.GetHash(), expectedWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHash);
            }

            Assert.Equal(2, expectedWallet.BlockLocator.Count);
            var expectedBlockHash = block.GetHash();
            Assert.Equal(expectedBlockHash, expectedWallet.BlockLocator.ElementAt(0));

            expectedBlockHash = chain.Genesis.HashBlock;
            Assert.Equal(expectedBlockHash, expectedWallet.BlockLocator.ElementAt(1));
    
            Assert.Equal(expectedWallet.EncryptedSeed, mnemonic.DeriveExtKey(passphrase).PrivateKey.GetEncryptedBitcoinSecret(password, Network.BRhodiumMain).ToWif());
        }

        [Fact]
        public void CreateWalletWithMnemonicListCreatesWalletUsingMnemonicList()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var chain = new ConcurrentChain(Network.BRhodiumMain);
            var nonce = RandomUtils.GetUInt32();
            var block = new Block();
            block.AddTransaction(new Transaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Genesis.HashBlock;
            block.Header.Nonce = nonce;
            chain.SetTip(block.Header);

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.BRhodiumMain, chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                                                    dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var password = "test";

            var mnemonicList = new Mnemonic(Wordlist.French, WordCount.Eighteen);

            // create the wallet
            var mnemonic = walletManager.CreateWallet(password, "mywallet", mnemonicList: mnemonicList.ToString());

            Assert.Equal(mnemonic.DeriveSeed(), mnemonicList.DeriveSeed());
        }

        [Fact]
        public void UpdateLastBlockSyncedHeightWhileWalletCreatedDoesNotThrowInvalidOperationException()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>()))
               .Returns(new Mock<ILogger>().Object);

            var walletManager = new WalletManager(loggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                                                  dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var concurrentChain = new ConcurrentChain(this.Network);
            ChainedHeader tip = WalletTestsHelpers.AppendBlock(null, concurrentChain).ChainedHeader;

            walletManager.SaveWallet(WalletTestsHelpers.GenerateBlankWallet("walletA", "walletA", this.Network));
            walletManager.SaveWallet(WalletTestsHelpers.GenerateBlankWallet("walletB", "walletB", this.Network));

            Parallel.For(0, 50, new ParallelOptions { MaxDegreeOfParallelism = 10 }, (int iteration) =>
            {
                string walletName = $"wallet{iteration}";
                walletManager.UpdateLastBlockSyncedHeight(tip);
                walletManager.SaveWallet(WalletTestsHelpers.GenerateBlankWallet(walletName, walletName, this.Network));
                walletManager.UpdateLastBlockSyncedHeight(tip);
            });

            Assert.Equal(52, walletManager.GetWalletNames().Count());
            foreach (var walletName in walletManager.GetWalletNames())
            {
                Assert.True(walletManager.GetWalletByName(walletName).BlockLocator != null);
            }
        }

        [Fact]
        public void LoadWalletWithExistingWalletLoadsWalletOntoManager()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var wallet = WalletTestsHelpers.GenerateBlankWalletWithExtKey("testWallet", "password", this.Network, 1); //this.walletFixture.GenerateBlankWallet("testWallet", "password");

            Network network = Network.BRhodiumRegTest;
            var walletManager = new WalletManager(this.LoggerFactory.Object, network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(network), new Mock<WalletSettings>().Object,
                                                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            walletManager.SaveWallet(wallet.wallet);

            var result = walletManager.LoadWallet("password", "testWallet");

            Assert.Equal("testWallet", result.Name);
            Assert.Equal(network, result.Network);
        }

        [Fact]
        public void LoadWalletWithNonExistingWalletThrowsFileNotFoundException()
        {
            Assert.Throws<WalletDoesNotExistException>(() =>
            {
                DataFolder dataFolder = CreateDataFolder(this);
                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                                                 dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

                walletManager.LoadWallet("password", "testWallet");
            });
        }

        [Fact]
        public void RecoverWalletWithEqualInputAsExistingWalletRecoversWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var password = "test";
            var passphrase = "this is my magic passphrase";
            var walletName = "mywallet";

            ConcurrentChain chain = WalletTestsHelpers.PrepareChainWithBlock(this.Network);

            // create a fresh manager.
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);


            // Prepare an existing wallet through this manager and delete the file from disk. Return the created wallet object and mnemonic.
            var deletedWallet = this.CreateWalletOnDiskAndDeleteWallet(walletManager, password, passphrase, walletName, chain);
            Assert.Throws<WalletException>(() => walletManager.GetWalletByName(walletName));


            // Try to recover it.
            var recoveredWallet = walletManager.RecoverWallet(password, walletName, deletedWallet.mnemonic.ToString(), DateTime.Now.AddDays(1), passphrase);

            Assert.False(walletManager.GetWalletByName(walletName) == null);

            var expectedWallet = deletedWallet.wallet;

            Assert.Equal(expectedWallet.Name, recoveredWallet.Name);
            Assert.Equal(expectedWallet.Network, recoveredWallet.Network);
            Assert.Equal(expectedWallet.EncryptedSeed, recoveredWallet.EncryptedSeed);
            Assert.Equal(expectedWallet.ChainCode, recoveredWallet.ChainCode);

            Assert.Equal(1, expectedWallet.AccountsRoot.Count);
            Assert.Equal(1, recoveredWallet.AccountsRoot.Count);

            for (var i = 0; i < recoveredWallet.AccountsRoot.Count; i++)
            {
                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).CoinType, recoveredWallet.AccountsRoot.ElementAt(i).CoinType);
                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHeight, recoveredWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHeight);
                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHash, recoveredWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHash);

                var recoveredAccountRoot = recoveredWallet.AccountsRoot.ElementAt(i);
                var expectedAccountRoot = expectedWallet.AccountsRoot.ElementAt(i);

                Assert.Equal(1, recoveredAccountRoot.Accounts.Count);
                Assert.Equal(1, expectedAccountRoot.Accounts.Count);

                for (var j = 0; j < expectedAccountRoot.Accounts.Count; j++)
                {
                    var expectedAccount = expectedAccountRoot.Accounts.ElementAt(j);
                    var recoveredAccount = recoveredAccountRoot.Accounts.ElementAt(j);
                    Assert.Equal(expectedAccount.Name, recoveredAccount.Name);
                    Assert.Equal(expectedAccount.Index, recoveredAccount.Index);
                    Assert.Equal(expectedAccount.HdPath, recoveredAccount.HdPath);
                    Assert.Equal(expectedAccount.ExtendedPubKey, expectedAccount.ExtendedPubKey);

                    Assert.Equal(20, recoveredAccount.InternalAddresses.Count);

                    for (var k = 0; k < recoveredAccount.InternalAddresses.Count; k++)
                    {
                        var expectedAddress = expectedAccount.InternalAddresses.ElementAt(k);
                        var recoveredAddress = recoveredAccount.InternalAddresses.ElementAt(k);
                        Assert.Equal(expectedAddress.Index, recoveredAddress.Index);
                        Assert.Equal(expectedAddress.ScriptPubKey, recoveredAddress.ScriptPubKey);
                        Assert.Equal(expectedAddress.Address, recoveredAddress.Address);
                        Assert.Equal(expectedAddress.Pubkey, recoveredAddress.Pubkey);
                        Assert.Equal(expectedAddress.HdPath, recoveredAddress.HdPath);
                        Assert.Equal(0, expectedAddress.Transactions.Count);
                        Assert.Equal(expectedAddress.Transactions.Count, recoveredAddress.Transactions.Count);
                    }

                    Assert.Equal(20, recoveredAccount.ExternalAddresses.Count);
                    for (var l = 0; l < recoveredAccount.ExternalAddresses.Count; l++)
                    {
                        var expectedAddress = expectedAccount.ExternalAddresses.ElementAt(l);
                        var recoveredAddress = recoveredAccount.ExternalAddresses.ElementAt(l);
                        Assert.Equal(expectedAddress.Index, recoveredAddress.Index);
                        Assert.Equal(expectedAddress.ScriptPubKey, recoveredAddress.ScriptPubKey);
                        Assert.Equal(expectedAddress.Address, recoveredAddress.Address);
                        Assert.Equal(expectedAddress.Pubkey, recoveredAddress.Pubkey);
                        Assert.Equal(expectedAddress.HdPath, recoveredAddress.HdPath);
                        Assert.Equal(0, expectedAddress.Transactions.Count);
                        Assert.Equal(expectedAddress.Transactions.Count, recoveredAddress.Transactions.Count);
                    }
                }
            }

            Assert.Equal(2, expectedWallet.BlockLocator.Count);
            Assert.Equal(2, recoveredWallet.BlockLocator.Count);
            Assert.Equal(expectedWallet.BlockLocator.ElementAt(0), recoveredWallet.BlockLocator.ElementAt(0));
            Assert.Equal(expectedWallet.BlockLocator.ElementAt(1), recoveredWallet.BlockLocator.ElementAt(1));
            Assert.Equal(expectedWallet.EncryptedSeed, recoveredWallet.EncryptedSeed);
        }

        [Fact]
        public void RecoverWalletOnlyWithPasswordWalletRecoversWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var password = "test";
            var walletName = "mywallet";

            ConcurrentChain chain = WalletTestsHelpers.PrepareChainWithBlock(this.Network);

            // create a fresh manager.
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);


            // prepare an existing wallet through this manager and delete the file from disk. Return the created wallet object and mnemonic.
            var deletedWallet = this.CreateWalletOnDiskAndDeleteWallet(walletManager, password, password, walletName, chain);
            //Assert.False(File.Exists(Path.Combine(dataFolder.WalletPath + $"/{walletName}.wallet.json")));


            Assert.Throws<WalletException>(() => walletManager.GetWalletByName(walletName));

            // try to recover it.
            var recoveredWallet = walletManager.RecoverWallet(password, walletName, deletedWallet.mnemonic.ToString(), DateTime.Now.AddDays(1), password);

            var wallet = walletManager.GetWalletByName(walletName);
            Assert.True(wallet != null);
            //Assert.True(File.Exists(Path.Combine(dataFolder.WalletPath + $"/{walletName}.wallet.json")));

            var expectedWallet = deletedWallet.wallet;

            Assert.Equal(expectedWallet.Name, recoveredWallet.Name);
            Assert.Equal(expectedWallet.Network, recoveredWallet.Network);
            Assert.Equal(expectedWallet.EncryptedSeed, recoveredWallet.EncryptedSeed);
            Assert.Equal(expectedWallet.ChainCode, recoveredWallet.ChainCode);

            Assert.Equal(1, expectedWallet.AccountsRoot.Count);
            Assert.Equal(1, recoveredWallet.AccountsRoot.Count);

            for (var i = 0; i < recoveredWallet.AccountsRoot.Count; i++)
            {
                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).CoinType, recoveredWallet.AccountsRoot.ElementAt(i).CoinType);
                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHeight, recoveredWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHeight);
                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHash, recoveredWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHash);

                var recoveredAccountRoot = recoveredWallet.AccountsRoot.ElementAt(i);
                var expectedAccountRoot = expectedWallet.AccountsRoot.ElementAt(i);

                Assert.Equal(1, recoveredAccountRoot.Accounts.Count);
                Assert.Equal(1, expectedAccountRoot.Accounts.Count);

                for (var j = 0; j < expectedAccountRoot.Accounts.Count; j++)
                {
                    var expectedAccount = expectedAccountRoot.Accounts.ElementAt(j);
                    var recoveredAccount = recoveredAccountRoot.Accounts.ElementAt(j);
                    Assert.Equal(expectedAccount.Name, recoveredAccount.Name);
                    Assert.Equal(expectedAccount.Index, recoveredAccount.Index);
                    Assert.Equal(expectedAccount.HdPath, recoveredAccount.HdPath);
                    Assert.Equal(expectedAccount.ExtendedPubKey, expectedAccount.ExtendedPubKey);

                    Assert.Equal(20, recoveredAccount.InternalAddresses.Count);

                    for (var k = 0; k < recoveredAccount.InternalAddresses.Count; k++)
                    {
                        var expectedAddress = expectedAccount.InternalAddresses.ElementAt(k);
                        var recoveredAddress = recoveredAccount.InternalAddresses.ElementAt(k);
                        Assert.Equal(expectedAddress.Index, recoveredAddress.Index);
                        Assert.Equal(expectedAddress.ScriptPubKey, recoveredAddress.ScriptPubKey);
                        Assert.Equal(expectedAddress.Address, recoveredAddress.Address);
                        Assert.Equal(expectedAddress.Pubkey, recoveredAddress.Pubkey);
                        Assert.Equal(expectedAddress.HdPath, recoveredAddress.HdPath);
                        Assert.Equal(0, expectedAddress.Transactions.Count);
                        Assert.Equal(expectedAddress.Transactions.Count, recoveredAddress.Transactions.Count);
                    }

                    Assert.Equal(20, recoveredAccount.ExternalAddresses.Count);
                    for (var l = 0; l < recoveredAccount.ExternalAddresses.Count; l++)
                    {
                        var expectedAddress = expectedAccount.ExternalAddresses.ElementAt(l);
                        var recoveredAddress = recoveredAccount.ExternalAddresses.ElementAt(l);
                        Assert.Equal(expectedAddress.Index, recoveredAddress.Index);
                        Assert.Equal(expectedAddress.ScriptPubKey, recoveredAddress.ScriptPubKey);
                        Assert.Equal(expectedAddress.Address, recoveredAddress.Address);
                        Assert.Equal(expectedAddress.Pubkey, recoveredAddress.Pubkey);
                        Assert.Equal(expectedAddress.HdPath, recoveredAddress.HdPath);
                        Assert.Equal(0, expectedAddress.Transactions.Count);
                        Assert.Equal(expectedAddress.Transactions.Count, recoveredAddress.Transactions.Count);
                    }
                }
            }

            Assert.Equal(2, expectedWallet.BlockLocator.Count);
            Assert.Equal(2, recoveredWallet.BlockLocator.Count);
            Assert.Equal(expectedWallet.BlockLocator.ElementAt(0), recoveredWallet.BlockLocator.ElementAt(0));
            Assert.Equal(expectedWallet.BlockLocator.ElementAt(1), recoveredWallet.BlockLocator.ElementAt(1));
            Assert.Equal(expectedWallet.EncryptedSeed, recoveredWallet.EncryptedSeed);
        }
        [Fact]
        public void LoadKeysLookupInParallelDoesNotThrowInvalidOperationException()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            // generate 3 wallet with 2 accounts containing 20 external and 20 internal addresses each.
            var template1 = WalletTestsHelpers.GenerateBlankWalletWithExtKey("wallet1", "pass1", this.Network);
            var template2 = WalletTestsHelpers.GenerateBlankWalletWithExtKey("wallet2", "pass2", this.Network);
            var template3 = WalletTestsHelpers.GenerateBlankWalletWithExtKey("wallet3", "pass3", this.Network);

            walletManager.SaveWallet(template1.wallet);
            walletManager.SaveWallet(template2.wallet);
            walletManager.SaveWallet(template3.wallet);

            Parallel.For(0, 5000, new ParallelOptions { MaxDegreeOfParallelism = 10 }, (int iteration) =>
            {
                walletManager.LoadKeysLookupLock();
                walletManager.LoadKeysLookupLock();
                walletManager.LoadKeysLookupLock();
            });

            Assert.Equal(240, walletManager.addressByScriptLookup.Count);
        }


        [Fact]
        public void GetUnusedAccountUsingNameForNonExistinAccountThrowsWalletException()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Assert.Throws<WalletException>(() =>
            {
                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                    dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

                walletManager.GetUnusedAccount("nonexisting", "password");
            });
        }

        [Fact]
        public void GetUnusedAccountUsingWalletNameWithExistingAccountReturnsUnusedAccountIfExistsOnWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            string walletName = Guid.NewGuid().ToString();
            string walletPassword = Guid.NewGuid().ToString();

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var tuple = WalletTestsHelpers.GenerateBlankWalletWithExtKey(walletName, walletPassword, this.Network, 1);

            walletManager.SaveWallet(tuple.wallet);

            var result = walletManager.GetUnusedAccount(walletName, walletPassword);

            Assert.Equal("account 1", result.Name);
        }

        [Fact]
        public void GetUnusedAccountUsingWalletNameWithoutUnusedAccountsCreatesAccountAndSavesWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            string walletName = Guid.NewGuid().ToString();
            string walletPassword = Guid.NewGuid().ToString();

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = WalletTestsHelpers.GenerateBlankWallet(walletName, walletPassword, this.Network);
            wallet.AccountsRoot.ElementAt(0).Accounts.Clear();
            walletManager.SaveWallet(wallet);

            var result = walletManager.GetUnusedAccount(walletName, walletPassword);

            Assert.Equal("account 0", result.Name);
        }

        [Fact]
        public void GetUnusedAccountUsingWalletWithExistingAccountReturnsUnusedAccountIfExistsOnWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            string walletName = Guid.NewGuid().ToString();
            string walletPassword = Guid.NewGuid().ToString();

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var tuple = WalletTestsHelpers.GenerateBlankWalletWithExtKey(walletName, walletPassword, this.Network,1);

            walletManager.SaveWallet(tuple.wallet);

            var result = walletManager.GetUnusedAccount(walletName, walletPassword);

            Assert.Equal("account 1", result.Name);
        }

        [Fact]
        public void GetUnusedAccountUsingWalletWithoutUnusedAccountsCreatesAccountAndSavesWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet("testWallet", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Clear();
            walletManager.SaveWallet(wallet);

            var result = walletManager.GetUnusedAccount(wallet, "password");

            Assert.Equal("account 0", result.Name);
        }

        [Fact]
        public void CreateNewAccountGivenNoAccountsExistingInWalletCreatesNewAccount()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet("testWallet", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Clear();

            var result = wallet.AddNewAccount("password", (CoinType)this.Network.Consensus.CoinType, DateTimeOffset.UtcNow);

            Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.Count);
            var extKey = new ExtKey(Key.Parse(wallet.EncryptedSeed, "password", wallet.Network), wallet.ChainCode);
            var expectedExtendedPubKey = extKey.Derive(new KeyPath($"m/44'/1'/0'")).Neuter().ToString(wallet.Network);
            Assert.Equal($"account 0", result.Name);
            Assert.Equal(0, result.Index);
            Assert.Equal($"m/44'/1'/0'", result.HdPath);
            Assert.Equal(expectedExtendedPubKey, result.ExtendedPubKey);
            Assert.Equal(0, result.InternalAddresses.Count);
            Assert.Equal(0, result.ExternalAddresses.Count);
        }

        [Fact]
        public void CreateNewAccountGivenExistingAccountInWalletCreatesNewAccount()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet("testWallet", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount { Name = "unused" });

            var result = wallet.AddNewAccount("password", (CoinType)this.Network.Consensus.CoinType, DateTimeOffset.UtcNow);

            Assert.Equal(3, wallet.AccountsRoot.ElementAt(0).Accounts.Count);
            var extKey = new ExtKey(Key.Parse(wallet.EncryptedSeed, "password", wallet.Network), wallet.ChainCode);
            var expectedExtendedPubKey = extKey.Derive(new KeyPath($"m/44'/1'/1'")).Neuter().ToString(wallet.Network);
            Assert.Equal($"account 1", result.Name);
            Assert.Equal(1, result.Index);
            Assert.Equal($"m/44'/1'/1'", result.HdPath);
            Assert.Equal(expectedExtendedPubKey, result.ExtendedPubKey);
            Assert.Equal(0, result.InternalAddresses.Count);
            Assert.Equal(0, result.ExternalAddresses.Count);
        }

        [Fact]
        public void GetUnusedAddressUsingNameWithWalletWithoutAccountOfGivenNameThrowsException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                    CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
                var wallet = this.walletFixture.GenerateBlankWallet("testWallet", "password");
                //walletManager.Wallets.AddOrReplace("testWallet", wallet);

                var result = walletManager.GetUnusedAddress(new WalletAccountReference("testWallet", "unexistingAccount"));
            });
        }

        [Fact]
        public void GetUnusedAddressUsingNameForNonExistinAccountThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                    CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

                walletManager.GetUnusedAddress(new WalletAccountReference("nonexisting", "account"));
            });
        }

        [Fact]
        public void GetUnusedAddressWithWalletHavingUnusedAddressReturnsAddress()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            string name = Guid.NewGuid().ToString();
            string pass = Guid.NewGuid().ToString();

            var template = WalletTestsHelpers.GenerateBlankWalletWithExtKey(name, pass, this.Network);
            var wallet = template.wallet;

            var usedAddress = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
            var unUsedAddress = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1);
            usedAddress.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(15000), null, null, null, usedAddress.ScriptPubKey));
            
            walletManager.SaveWallet(wallet,true);

            var result = walletManager.GetUnusedAddress(new WalletAccountReference(name, "account 0"));

            Assert.Equal(unUsedAddress.Address, result.Address);
        }

        [Fact]
        public void GetOrCreateChangeAddressWithWalletHavingUnusedAddressReturnsAddress()
        {
            string walletName = Guid.NewGuid().ToString();
            string walletPassword = Guid.NewGuid().ToString();
            DataFolder dataFolder = CreateDataFolder(this);
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = WalletTestsHelpers.GenerateBlankWallet(walletName, walletPassword, this.Network);

            HdAddress used = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0);
            HdAddress unused = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(1);

            used.Transactions.Add(
                 new TransactionData()
                 {
                     Id = new uint256(),
                     Amount = new Money(1000000000),
                     CreationTime = DateTimeOffset.Now,
                     ScriptPubKey = new Script()
                 });
          
            walletManager.SaveWallet(wallet,true);

            var result = walletManager.GetUnusedChangeAddress(new WalletAccountReference(wallet.Name, wallet.AccountsRoot.First().Accounts.First().Name));

            Assert.Equal(unused.Address, result.Address);
        }

        [Fact]
        public void GetOrCreateChangeAddressWithWalletNotHavingUnusedAddressReturnsAddress()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = WalletTestsHelpers.GenerateBlankWallet("myWallet", "password",this.Network);

            var extKey = new ExtKey(Key.Parse(wallet.EncryptedSeed, "password", wallet.Network), wallet.ChainCode);
            var accountExtendedPubKey = extKey.Derive(new KeyPath($"m/44'/0'/0'")).Neuter().ToString(wallet.Network);
            wallet.AccountsRoot.ElementAt(0).Accounts.Clear();
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "myAccount",
                HdPath = "m/44'/0'/0'",
                ExtendedPubKey = accountExtendedPubKey,
                InternalAddresses = new List<HdAddress>(),
                ExternalAddresses = new List<HdAddress>()
            });
            walletManager.SaveWallet(wallet);

            var result = walletManager.GetUnusedChangeAddress(new WalletAccountReference(wallet.Name, wallet.AccountsRoot.First().Accounts.First().Name));

            Assert.NotNull(result.Address);
        }

        [Fact]
        public void GetUnusedAddressWithoutWalletHavingUnusedAddressCreatesAddressAndSavesWallet()
        {
            string walletName = Guid.NewGuid().ToString();
            string walletPassword = Guid.NewGuid().ToString();
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = WalletTestsHelpers.GenerateBlankWallet(walletName, walletPassword, this.Network);
            var extKey = new ExtKey(Key.Parse(wallet.EncryptedSeed, walletPassword, wallet.Network), wallet.ChainCode);
            var accountExtendedPubKey = extKey.Derive(new KeyPath($"m/44'/0'/0'")).Neuter().ToString(wallet.Network);
            wallet.AccountsRoot.ElementAt(0).Accounts.Clear();
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "myAccount",
                HdPath = "m/44'/0'/0'",
                ExternalAddresses = new List<HdAddress>
                {
                    new HdAddress {
                        Index = 0,
                        Address = "myUsedAddress",
                        ScriptPubKey = new Script(),
                        Pubkey = new Script(),
                        HdPath = "m/44'/0'/0'/0",
                        Transactions = new List<TransactionData>
                        {
                            new TransactionData(){
                               Id = new uint256(),
                               Amount = new Money(1000000000),
                               CreationTime = DateTimeOffset.Now,
                               ScriptPubKey = new Script()
                            },
                        },
                    }
                },
                InternalAddresses = new List<HdAddress>(),
                ExtendedPubKey = accountExtendedPubKey
            });
            walletManager.SaveWallet(wallet,true);

            var result = walletManager.GetUnusedAddress(new WalletAccountReference(walletName, "myAccount"));

            KeyPath keyPath = new KeyPath($"0/1");
            ExtPubKey extPubKey = ExtPubKey.Parse(accountExtendedPubKey).Derive(keyPath);
            var pubKey = extPubKey.PubKey;
            BitcoinPubKeyAddress address = pubKey.GetAddress(wallet.Network);
            Assert.Equal(1, result.Index);
            Assert.Equal("m/44'/0'/0'/0/1", result.HdPath);
            Assert.Equal(address.ToString(), result.Address);
            Assert.Equal(pubKey.ScriptPubKey, result.Pubkey);
            Assert.Equal(address.ScriptPubKey, result.ScriptPubKey);
            Assert.Equal(0, result.Transactions.Count);
            Assert.True(walletManager.GetWalletByName(walletName) != null);
        }

        [Fact]
        public void GetHistoryByNameWithExistingWalletReturnsAllAddressesWithTransactions()
        {
            string walletName = Guid.NewGuid().ToString();
            string walletPassword = Guid.NewGuid().ToString();
            var chain = new ConcurrentChain(this.Network);
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = WalletTestsHelpers.GenerateBlankWallet(walletName, walletPassword, this.Network);

            var account  = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0);
            var extAddy = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
            var internalAddy = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0);
            WalletTestsHelpers.AddBlocksWithCoinbaseToChain(this.Network, chain, extAddy, 1);
            WalletTestsHelpers.AddBlocksWithCoinbaseToChain(this.Network, chain, internalAddy, 1);

            walletManager.SaveWallet(wallet, true);
            var result = walletManager.GetHistory(walletName).ToList();

            Assert.NotEmpty(result);
            Assert.Single(result);
            var accountHistory = result.ElementAt(0);
            Assert.NotNull(accountHistory.Account);
            Assert.Equal("account 0", accountHistory.Account.Name);
            Assert.NotEmpty(accountHistory.History);
            Assert.Equal(2, accountHistory.History.Count());

            var historyAddress = accountHistory.History.ElementAt(0);
            Assert.Equal(extAddy.Address, historyAddress.Address.Address);
            historyAddress = accountHistory.History.ElementAt(1);
            Assert.Equal(internalAddy.Address, historyAddress.Address.Address);
        }

        [Fact]
        public void GetHistoryByAccountWithExistingAccountReturnsAllAddressesWithTransactions()
        {
            string walletName = Guid.NewGuid().ToString();
            string walletPassword = Guid.NewGuid().ToString();
            var chain = new ConcurrentChain(this.Network);
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = WalletTestsHelpers.GenerateBlankWallet(walletName, walletPassword, this.Network);

            var account = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0);
            var extAddy = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
            var internalAddy = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0);
            WalletTestsHelpers.AddBlocksWithCoinbaseToChain(this.Network, chain, extAddy, 1);
            WalletTestsHelpers.AddBlocksWithCoinbaseToChain(this.Network, chain, internalAddy, 1);

            walletManager.SaveWallet(wallet, true);            

            var accountHistory = walletManager.GetHistory(account);

            Assert.NotNull(accountHistory);
            Assert.NotNull(accountHistory.Account);
            Assert.Equal("account 0", accountHistory.Account.Name);
            Assert.NotEmpty(accountHistory.History);
            Assert.Equal(2, accountHistory.History.Count());

            var historyAddress = accountHistory.History.ElementAt(0);
            Assert.Equal(extAddy.Address, historyAddress.Address.Address);
            historyAddress = accountHistory.History.ElementAt(1);
            Assert.Equal(internalAddy.Address, historyAddress.Address.Address);
        }

        [Fact]
        public void GetHistoryByAccountWithoutHavingAddressesWithTransactionsReturnsEmptyList()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");

            HdAccount account = new HdAccount
            {
                Index = 0,
                Name = "myAccount",
                HdPath = "m/44'/0'/0'",
                ExternalAddresses = new List<HdAddress>(),
                InternalAddresses = new List<HdAddress>(),
                ExtendedPubKey = "blabla"
            };
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(account);
            walletManager.SaveWallet(wallet, true);

            var result = walletManager.GetHistory(account);

            Assert.NotNull(result.Account);
            Assert.Equal("myAccount", result.Account.Name);
            Assert.Empty(result.History);
        }

        [Fact]
        public void GetHistoryByWalletNameWithoutExistingWalletThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                    CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
                walletManager.GetHistory("noname");
            });
        }

        [Fact]
        public void GetWalletByNameWithExistingWalletReturnsWallet()
        {
            string walletName = Guid.NewGuid().ToString();
            string walletPassword = Guid.NewGuid().ToString();
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet(walletName, walletPassword);
            walletManager.SaveWallet(wallet, true);

            var result = walletManager.GetWallet(walletName);

            Assert.Equal(wallet.EncryptedSeed, result.EncryptedSeed);
        }

        [Fact]
        public void GetWalletByNameWithoutExistingWalletThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                    CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
                walletManager.GetWallet("noname");
            });
        }

        [Fact]
        public void GetAccountsByNameWithExistingWalletReturnsAccountsFromWallet()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var tuple = WalletTestsHelpers.GenerateBlankWalletWithExtKey("myWallet", "password",this.Network, 4);
            Wallet wallet = tuple.wallet;
     
            walletManager.SaveWallet(wallet);

            var result = walletManager.GetAccounts("myWallet");

            Assert.Equal(4, result.Count());
            Assert.Equal("account 0", result.ElementAt(0).Name);
            Assert.Equal("account 1", result.ElementAt(1).Name);
            Assert.Equal("account 2", result.ElementAt(2).Name);
            Assert.Equal("account 3", result.ElementAt(3).Name);
        }

        [Fact]
        public void GetAccountsByNameWithExistingWalletMissingAccountsReturnsEmptyList()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            wallet.AccountsRoot.FirstOrDefault().Accounts.Clear();

            walletManager.SaveWallet(wallet, true);

            var result = walletManager.GetAccounts("myWallet");

            Assert.Empty(result);
        }

        [Fact]
        public void GetAccountsByNameWithoutExistingWalletThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                    CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

                walletManager.GetAccounts("myWallet");
            });
        }

        [Fact]
        public void LastBlockHeightWithoutWalletsReturnsChainTipHeight()
        {
            var chain = new ConcurrentChain(Network.BRhodiumMain);
            var nonce = RandomUtils.GetUInt32();
            var block = new Block();
            block.AddTransaction(new Transaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Genesis.HashBlock;
            block.Header.Nonce = nonce;
            chain.SetTip(block.Header);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var result = walletManager.LastBlockHeight();

            Assert.Equal(chain.Tip.Height, result);
        }

        [Fact]
        public void LastBlockHeightWithWalletsReturnsLowestLastBlockSyncedHeight()
        {
            string walletName = Guid.NewGuid().ToString();
            string walletName2 = Guid.NewGuid().ToString();
            string walletName3 = Guid.NewGuid().ToString();

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = WalletTestsHelpers.GenerateBlankWallet(walletName, walletName, this.Network);
            wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHeight = 15;
            var wallet2 = WalletTestsHelpers.GenerateBlankWallet(walletName2, walletName2, this.Network);
            wallet2.AccountsRoot.ElementAt(0).LastBlockSyncedHeight = 20;
            var wallet3 = WalletTestsHelpers.GenerateBlankWallet(walletName3, walletName3, this.Network);
            wallet3.AccountsRoot.ElementAt(0).LastBlockSyncedHeight = 56;

            walletManager.SaveWallet(wallet, true);
            walletManager.SaveWallet(wallet2, true);
            walletManager.SaveWallet(wallet3, true);

            var result = walletManager.LastBlockHeight();

            Assert.Equal(15, result);
        }


        [Fact]
        public void LastReceivedBlockHashWithoutWalletsReturnsChainTipHashBlock()
        {
            var chain = new ConcurrentChain(Network.BRhodiumMain);
            var nonce = RandomUtils.GetUInt32();
            var block = new Block();
            block.AddTransaction(new Transaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Genesis.HashBlock;
            block.Header.Nonce = nonce;
            chain.SetTip(block.Header);

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.BRhodiumMain, chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var result = walletManager.LastReceivedBlockHash();

            Assert.Equal(chain.Tip.HashBlock, result);
        }

        [Fact]
        public void LastReceivedBlockHashWithWalletsReturnsLowestLastBlockSyncedHash()
        {

            string walletName = Guid.NewGuid().ToString();
            string walletName2 = Guid.NewGuid().ToString();
            string walletName3 = Guid.NewGuid().ToString();

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = WalletTestsHelpers.GenerateBlankWallet(walletName, walletName, this.Network);
            wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHeight = 15;
            wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHash = new uint256(15);
            var wallet2 = WalletTestsHelpers.GenerateBlankWallet(walletName2, walletName2, this.Network);
            wallet2.AccountsRoot.ElementAt(0).LastBlockSyncedHeight = 20;
            wallet2.AccountsRoot.ElementAt(0).LastBlockSyncedHash = new uint256(20);
            var wallet3 = WalletTestsHelpers.GenerateBlankWallet(walletName3, walletName3, this.Network);
            wallet3.AccountsRoot.ElementAt(0).LastBlockSyncedHeight = 56;
            wallet3.AccountsRoot.ElementAt(0).LastBlockSyncedHash = new uint256(56);

            walletManager.SaveWallet(wallet, true);
            walletManager.SaveWallet(wallet2, true);
            walletManager.SaveWallet(wallet3, true);

            var result = walletManager.LastReceivedBlockHash();

            //var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
            //    CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            //var wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            //wallet.AccountsRoot.ElementAt(0).CoinType = CoinType.BRhodium;
            //wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHeight = 15;
            //wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHash = new uint256(15);
            //var wallet2 = this.walletFixture.GenerateBlankWallet("myWallet2", "password");
            //wallet2.AccountsRoot.ElementAt(0).CoinType = (CoinType)this.Network.Consensus.CoinType;
            //wallet2.AccountsRoot.ElementAt(0).LastBlockSyncedHeight = 20;
            //wallet2.AccountsRoot.ElementAt(0).LastBlockSyncedHash = new uint256(20);
            //var wallet3 = this.walletFixture.GenerateBlankWallet("myWallet3", "password");
            //wallet3.AccountsRoot.ElementAt(0).CoinType = (CoinType)this.Network.Consensus.CoinType;
            //wallet3.AccountsRoot.ElementAt(0).LastBlockSyncedHeight = 56;
            //wallet3.AccountsRoot.ElementAt(0).LastBlockSyncedHash = new uint256(56);
            ////walletManager.Wallets.AddOrReplace("myWallet", wallet);
            ////walletManager.Wallets.AddOrReplace("myWallet2", wallet2);
            ////walletManager.Wallets.AddOrReplace("myWallet3", wallet3);

            //var result = walletManager.LastReceivedBlockHash();

            Assert.Equal(new uint256(56), result);
        }



        [Fact]
        public void NoLastReceivedBlockHashInWalletReturnsChainTip()
        {
            var chain = WalletTestsHelpers.GenerateChainWithHeight(2, this.Network);
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            wallet.AccountsRoot.ElementAt(0).CoinType = CoinType.BRhodium;
            //walletManager.Wallets.AddOrReplace("myWallet", wallet);

            var result = walletManager.LastReceivedBlockHash();
            Assert.Equal(chain.Tip.HashBlock, result);
        }

        [Fact]
        public void GetSpendableTransactionsWithChainOfHeightZeroReturnsNoTransactions()
        {
            var chain = WalletTestsHelpers.GenerateChainWithHeight(0, this.Network);
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            var account = wallet.AccountsRoot.ElementAt(0).Accounts.FirstOrDefault();
            var exTransactions = WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(1, 9, 10);
            for (int i = 0; i < exTransactions.Count; i++)
            {
                account.ExternalAddresses.ElementAt(i).Transactions.Add(exTransactions.ElementAt(i));
            }
            var intTransactions = WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(1, 9, 10);
            for (int i = 0; i < intTransactions.Count; i++)
            {
                account.InternalAddresses.ElementAt(i).Transactions.Add(intTransactions.ElementAt(i));
            }

            walletManager.SaveWallet(wallet);

            var result = walletManager.GetSpendableTransactionsInWallet("myWallet", confirmations: 1);

            Assert.Empty(result);
        }


        /// <summary>
        /// If the block height of the transaction is x+ away from the current chain top transactions must be returned where x is higher or equal to the specified amount of confirmations.
        /// </summary>
        [Fact]
        public void GetSpendableTransactionsReturnsTransactionsGivenBlockHeight()
        {
            var chain = WalletTestsHelpers.GenerateChainWithHeight(10, this.Network);
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var wallet = WalletTestsHelpers.GenerateBlankWallet("myWallet1", Guid.NewGuid().ToString(),this.Network);
            var account = wallet.AccountsRoot.ElementAt(0).Accounts.FirstOrDefault();
            account.Name = "First expectation";
            PlantTransactionsToAddresses(account.ExternalAddresses, WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(1, 9, 10));
            PlantTransactionsToAddresses(account.InternalAddresses, WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(2, 9, 10));
            walletManager.SaveWallet(wallet, true);
          

            var wallet2 = WalletTestsHelpers.GenerateBlankWallet("myWallet2", Guid.NewGuid().ToString(), this.Network);
            var account2 = wallet2.AccountsRoot.ElementAt(0).Accounts.FirstOrDefault();
            PlantTransactionsToAddresses(account2.ExternalAddresses, WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(1, 3, 5, 7, 9, 10));
            PlantTransactionsToAddresses(account2.InternalAddresses, WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(2, 4, 6, 8, 9, 10));
            walletManager.SaveWallet(wallet2, true);

            var wallet3 = WalletTestsHelpers.GenerateBlankWallet("myWallet3", Guid.NewGuid().ToString(), this.Network);
            var account3 = wallet3.AccountsRoot.ElementAt(0).Accounts.FirstOrDefault();
            account3.Name = "Second expectation";
            PlantTransactionsToAddresses(account3.ExternalAddresses, WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(5, 9, 11));
            PlantTransactionsToAddresses(account3.InternalAddresses, WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(6, 9, 11));
            walletManager.SaveWallet(wallet3, true);


            var result = walletManager.GetSpendableTransactionsInWallet("myWallet3", confirmations: 1).ToArray();

            Assert.Equal(4, result.Count());
            var info = result[0];
            Assert.Equal("Second expectation", info.Account.Name);
            Assert.Equal(wallet3.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).Address, info.Address.Address);
            Assert.Equal(5, info.Transaction.BlockHeight);
            info = result[1];
            Assert.Equal("Second expectation", info.Account.Name);
            Assert.Equal(wallet3.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Address, info.Address.Address);
            Assert.Equal(9, info.Transaction.BlockHeight);
            info = result[2];
            Assert.Equal("Second expectation", info.Account.Name);
            Assert.Equal(wallet3.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Address, info.Address.Address);
            Assert.Equal(6, info.Transaction.BlockHeight);
            info = result[3];
            Assert.Equal("Second expectation", info.Account.Name);
            Assert.Equal(wallet3.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(1).Address, info.Address.Address);
            Assert.Equal(9, info.Transaction.BlockHeight);
        }

        private static void PlantTransactionsToAddresses(ICollection<HdAddress> addresses, ICollection<TransactionData> intTransactions)
        {
            for (int i = 0; i < intTransactions.Count; i++)
            { 
                var t = intTransactions.ElementAt(i);
                addresses.ElementAt(i).Transactions.Add(t);
            }
        }

        [Fact]
        public void GetSpendableTransactionsWithSpentTransactionsReturnsSpendableTransactionsGivenBlockHeight()
        {
            var chain = WalletTestsHelpers.GenerateChainWithHeight(10, this.Network);
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);



            var wallet = WalletTestsHelpers.GenerateBlankWallet("myWallet1", Guid.NewGuid().ToString(), this.Network);
            var account = wallet.AccountsRoot.ElementAt(0).Accounts.FirstOrDefault();
            account.Name = "First expectation";
            PlantTransactionsToAddresses(account.ExternalAddresses, WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(1, 9, 11).Concat(WalletTestsHelpers.CreateSpentTransactionsOfBlockHeights(this.Network,1, 9, 11)).ToList());
            PlantTransactionsToAddresses(account.InternalAddresses, WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(2, 9, 11).Concat(WalletTestsHelpers.CreateSpentTransactionsOfBlockHeights(this.Network,2, 9, 11)).ToList());
            walletManager.SaveWallet(wallet, true);


            var result = walletManager.GetSpendableTransactionsInWallet("myWallet1", confirmations: 1).ToArray();

            Assert.Equal(4, result.Count());
            var info = result[0];
            Assert.Equal("First expectation", info.Account.Name);
            Assert.Equal(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).Address, info.Address.Address);
            Assert.Equal(1, info.Transaction.BlockHeight);
            Assert.Null(info.Transaction.SpendingDetails);
            info = result[1];
            Assert.Equal("First expectation", info.Account.Name);
            Assert.Equal(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Address, info.Address.Address);
            Assert.Equal(9, info.Transaction.BlockHeight);
            Assert.Null(info.Transaction.SpendingDetails);
            info = result[2];
            Assert.Equal("First expectation", info.Account.Name);
            Assert.Equal(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Address, info.Address.Address);
            Assert.Equal(2, info.Transaction.BlockHeight);
            Assert.Null(info.Transaction.SpendingDetails);
            info = result[3];
            Assert.Equal("First expectation", info.Account.Name);
            Assert.Equal(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(1).Address, info.Address.Address);
            Assert.Equal(9, info.Transaction.BlockHeight);
            Assert.Null(info.Transaction.SpendingDetails);
        }


       

        [Fact]
        public void GetSpendableTransactionsWithoutWalletsThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var chain = WalletTestsHelpers.GenerateChainWithHeight(10, this.Network);
                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                    CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

                walletManager.GetSpendableTransactionsInWallet("myWallet", confirmations: 1);
            });
        }

        [Fact]
        public void GetSpendableTransactionsWithoutWalletsOfWalletManagerCoinTypeReturnsEmptyList()
        {
            var chain = WalletTestsHelpers.GenerateChainWithHeight(10, this.Network);
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var wallet = this.walletFixture.GenerateBlankWallet("myWallet2", "password");
            var account = wallet.AccountsRoot.ElementAt(0).Accounts.FirstOrDefault();
            PlantTransactionsToAddresses(account.ExternalAddresses, WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(1, 3, 5, 7, 9, 10));
            PlantTransactionsToAddresses(account.InternalAddresses, WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(2, 4, 6, 8, 9, 10));
            walletManager.SaveWallet(wallet);


            var result = walletManager.GetSpendableTransactionsInWallet("myWallet2", confirmations: 1);

            Assert.Empty(result);
        }


        [Fact]
        public void GetSpendableTransactionsWithOnlySpentTransactionsReturnsEmptyList()
        {
            var chain = WalletTestsHelpers.GenerateChainWithHeight(10, this.Network);
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            var account = wallet.AccountsRoot.ElementAt(0).Accounts.FirstOrDefault();
            account.Name = "First expectation";
            PlantTransactionsToAddresses(account.ExternalAddresses,WalletTestsHelpers.CreateSpentTransactionsOfBlockHeights(this.Network,1, 9, 10));
            PlantTransactionsToAddresses(account.InternalAddresses,WalletTestsHelpers.CreateSpentTransactionsOfBlockHeights(this.Network,2, 9, 10));
            walletManager.SaveWallet(wallet);

            var result = walletManager.GetSpendableTransactionsInWallet("myWallet1", confirmations: 1);

            Assert.Empty(result);
        }


        [Fact]
        public void GetKeyForAddressWithoutWalletsThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                    CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

                var wallet = walletManager.GetWalletByName("mywallet");
                var key = wallet.GetExtendedPrivateKeyForAddress("password", new HdAddress()).PrivateKey;
            });
        }

        [Fact]
        public void GetKeyForAddressWithWalletReturnsAddressExtPrivateKey()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var data = WalletTestsHelpers.GenerateBlankWalletWithExtKey("myWallet", "password", this.Network);

            var address = new HdAddress
            {
                Index = 0,
                HdPath = "m/44'/0'/0'/0/0",
            };

            data.wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                ExternalAddresses = new List<HdAddress> {
                    address
                },
                InternalAddresses = new List<HdAddress>(),
                Name = "savings account"
            });
            //walletManager.Wallets.AddOrReplace("myWallet", data.wallet);

            var result = data.wallet.GetExtendedPrivateKeyForAddress("password", address);

            Assert.Equal(data.key.Derive(new KeyPath("m/44'/0'/0'/0/0")).GetWif(data.wallet.Network), result);
        }

        [Fact]
        public void GetKeyForAddressWitoutAddressOnWalletThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                    CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
                var data = WalletTestsHelpers.GenerateBlankWalletWithExtKey("myWallet", "password", this.Network);

                var address = new HdAddress
                {
                    Index = 0,
                    HdPath = "m/44'/0'/0'/0/0",
                };

                data.wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
                {
                    Index = 0,
                    ExternalAddresses = new List<HdAddress>(),
                    InternalAddresses = new List<HdAddress>(),
                    Name = "savings account"
                });
                //walletManager.Wallets.AddOrReplace("myWallet", data.wallet);

                data.wallet.GetExtendedPrivateKeyForAddress("password", address);
            });
        }

        [Fact]
        public void ProcessTransactionWithValidTransactionLoadsTransactionsIntoWalletIfMatching()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);

            var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            wallet.AccountsRoot.FirstOrDefault().Accounts.Clear();

            var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
            var changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

            var spendingAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            var destinationAddress = new HdAddress
            {
                Index = 1,
                HdPath = $"m/44'/0'/0'/0/1",
                Address = destinationKeys.Address.ToString(),
                Pubkey = destinationKeys.PubKey.ScriptPubKey,
                ScriptPubKey = destinationKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            var changeAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/1/0",
                Address = changeKeys.Address.ToString(),
                Pubkey = changeKeys.PubKey.ScriptPubKey,
                ScriptPubKey = changeKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            //Generate a spendable transaction
            var chainInfo = WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);
            TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
            spendingAddress.Transactions.Add(spendingTransaction);

            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "account1",
                HdPath = "m/44'/0'/0'",
                ExtendedPubKey = accountKeys.ExtPubKey,
                ExternalAddresses = new List<HdAddress> { spendingAddress, destinationAddress },
                InternalAddresses = new List<HdAddress> { changeAddress }
            });

            // setup a payment to yourself
            var transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
                .Returns(new Money(5000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chainInfo.chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            walletManager.SaveWallet(wallet,true);
            walletManager.LoadKeysLookupLock();
            walletManager.ProcessTransaction(transaction);
            wallet = walletManager.GetWallet("myWallet1");

            var spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
            Assert.Equal(1, spendingAddress.Transactions.Count);
            Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
            Assert.Equal(transaction.Outputs[1].Value, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).DestinationScriptPubKey);

            Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.Count);
            var destinationAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.ElementAt(0);
            Assert.Equal(transaction.GetHash(), destinationAddressResult.Id);
            Assert.Equal(transaction.Outputs[1].Value, destinationAddressResult.Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, destinationAddressResult.ScriptPubKey);

            Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Count);
            var changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.ElementAt(0);
            Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
            Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
            Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);
        }

        [Fact]
        public void ProcessTransactionWithEmptyScriptInTransactionDoesNotAddTransactionToWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);

            var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            wallet.AccountsRoot.FirstOrDefault().Accounts.Clear();

            var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
            var changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

            var spendingAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            var destinationAddress = new HdAddress
            {
                Index = 1,
                HdPath = $"m/44'/0'/0'/0/1",
                Address = destinationKeys.Address.ToString(),
                Pubkey = destinationKeys.PubKey.ScriptPubKey,
                ScriptPubKey = destinationKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            var changeAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/1/0",
                Address = changeKeys.Address.ToString(),
                Pubkey = changeKeys.PubKey.ScriptPubKey,
                ScriptPubKey = changeKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            //Generate a spendable transaction
            var chainInfo = WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);
            TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
            spendingAddress.Transactions.Add(spendingTransaction);

            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "account1",
                HdPath = "m/44'/0'/0'",
                ExtendedPubKey = accountKeys.ExtPubKey,
                ExternalAddresses = new List<HdAddress> { spendingAddress, destinationAddress },
                InternalAddresses = new List<HdAddress> { changeAddress }
            });

            // setup a payment to yourself
            var transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));
            transaction.Outputs.ElementAt(1).Value = Money.Zero;
            transaction.Outputs.ElementAt(1).ScriptPubKey = Script.Empty;

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
                .Returns(new Money(5000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chainInfo.chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            walletManager.SaveWallet(wallet,true);
            walletManager.LoadKeysLookupLock();
            walletManager.ProcessTransaction(transaction);
            wallet = walletManager.GetWallet("myWallet1");

            var spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
            Assert.Equal(1, spendingAddress.Transactions.Count);
            Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
            Assert.Equal(0, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.Count);

            Assert.Equal(0, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.Count);

            Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Count);
            var changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.ElementAt(0);
            Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
            Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
            Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);
        }

        [Fact]
        public void ProcessTransactionWithDestinationToChangeAddressDoesNotAddTransactionAsPayment()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);

            var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            wallet.AccountsRoot.FirstOrDefault().Accounts.Clear();
            var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            var changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");
            var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/1");

            var spendingAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            var changeAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/1/0",
                Address = changeKeys.Address.ToString(),
                Pubkey = changeKeys.PubKey.ScriptPubKey,
                ScriptPubKey = changeKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            var destinationChangeAddress = new HdAddress
            {
                Index = 1,
                HdPath = $"m/44'/0'/0'/1/1",
                Address = destinationKeys.Address.ToString(),
                Pubkey = destinationKeys.PubKey.ScriptPubKey,
                ScriptPubKey = destinationKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            //Generate a spendable transaction
            var chainInfo = WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);
            TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
            spendingAddress.Transactions.Add(spendingTransaction);

            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "account1",
                HdPath = "m/44'/0'/0'",
                ExtendedPubKey = accountKeys.ExtPubKey,
                ExternalAddresses = new List<HdAddress> { spendingAddress },
                InternalAddresses = new List<HdAddress> { changeAddress, destinationChangeAddress }
            });

            // setup a payment to yourself
            var transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
                .Returns(new Money(5000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chainInfo.chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            walletManager.SaveWallet(wallet, true);
            walletManager.LoadKeysLookupLock();

            walletManager.ProcessTransaction(transaction);
            wallet = walletManager.GetWallet("myWallet1");
            var spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
            Assert.Equal(1, spendingAddress.Transactions.Count);
            Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
            Assert.Equal(0, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.Count);
            Assert.Equal(1, spentAddressResult.Transactions.ElementAt(0).BlockHeight);

            Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Count);
            var destinationAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.ElementAt(0);
            Assert.Null(destinationAddressResult.BlockHeight);
            Assert.Equal(transaction.GetHash(), destinationAddressResult.Id);
            Assert.Equal(transaction.Outputs[0].Value, destinationAddressResult.Amount);
            Assert.Equal(transaction.Outputs[0].ScriptPubKey, destinationAddressResult.ScriptPubKey);

            Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(1).Transactions.Count);
            var changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(1).Transactions.ElementAt(0);
            Assert.Null(destinationAddressResult.BlockHeight);
            Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
            Assert.Equal(transaction.Outputs[1].Value, changeAddressResult.Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, changeAddressResult.ScriptPubKey);
        }

        [Fact]
        public void ProcessTransactionWithBlockHeightSetsBlockHeightOnTransactionData()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);

            var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            wallet.AccountsRoot.FirstOrDefault().Accounts.Clear();

            var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
            var changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

            var spendingAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            var destinationAddress = new HdAddress
            {
                Index = 1,
                HdPath = $"m/44'/0'/0'/0/1",
                Address = destinationKeys.Address.ToString(),
                Pubkey = destinationKeys.PubKey.ScriptPubKey,
                ScriptPubKey = destinationKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            var changeAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/1/0",
                Address = changeKeys.Address.ToString(),
                Pubkey = changeKeys.PubKey.ScriptPubKey,
                ScriptPubKey = changeKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            //Generate a spendable transaction
            var chainInfo = WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);
            TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
            spendingAddress.Transactions.Add(spendingTransaction);

            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "account1",
                HdPath = "m/44'/0'/0'",
                ExtendedPubKey = accountKeys.ExtPubKey,
                ExternalAddresses = new List<HdAddress> { spendingAddress, destinationAddress },
                InternalAddresses = new List<HdAddress> { changeAddress }
            });

            // setup a payment to yourself
            var transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
                .Returns(new Money(5000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chainInfo.chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            walletManager.SaveWallet(wallet,true);          

            walletManager.LoadKeysLookupLock();

            var block = WalletTestsHelpers.AppendTransactionInNewBlockToChain(chainInfo.chain, transaction);

            var blockHeight = chainInfo.chain.GetBlock(block.GetHash()).Height;
            walletManager.ProcessTransaction(transaction, blockHeight);

            wallet = walletManager.GetWallet("myWallet1");

            var spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
            Assert.Equal(1, spendingAddress.Transactions.Count);
            Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
            Assert.Equal(transaction.Outputs[1].Value, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).DestinationScriptPubKey);
            Assert.Equal(blockHeight - 1, spentAddressResult.Transactions.ElementAt(0).BlockHeight);

            Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.Count);
            var destinationAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.ElementAt(0);
            Assert.Equal(blockHeight, destinationAddressResult.BlockHeight);
            Assert.Equal(transaction.GetHash(), destinationAddressResult.Id);
            Assert.Equal(transaction.Outputs[1].Value, destinationAddressResult.Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, destinationAddressResult.ScriptPubKey);

            Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Count);
            var changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.ElementAt(0);
            Assert.Equal(blockHeight, destinationAddressResult.BlockHeight);
            Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
            Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
            Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);
        }

        [Fact]
        public void ProcessTransactionWithBlockSetsBlockHash()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);

            var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            wallet.AccountsRoot.FirstOrDefault().Accounts.Clear();

            var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
            var changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

            var spendingAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            var destinationAddress = new HdAddress
            {
                Index = 1,
                HdPath = $"m/44'/0'/0'/0/1",
                Address = destinationKeys.Address.ToString(),
                Pubkey = destinationKeys.PubKey.ScriptPubKey,
                ScriptPubKey = destinationKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            var changeAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/1/0",
                Address = changeKeys.Address.ToString(),
                Pubkey = changeKeys.PubKey.ScriptPubKey,
                ScriptPubKey = changeKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            //Generate a spendable transaction
            var chainInfo = WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);
            TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
            spendingAddress.Transactions.Add(spendingTransaction);

            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "account1",
                HdPath = "m/44'/0'/0'",
                ExtendedPubKey = accountKeys.ExtPubKey,
                ExternalAddresses = new List<HdAddress> { spendingAddress, destinationAddress },
                InternalAddresses = new List<HdAddress> { changeAddress }
            });

            // setup a payment to yourself
            var transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
                .Returns(new Money(5000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chainInfo.chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            walletManager.SaveWallet(wallet,true);
            walletManager.LoadKeysLookupLock();

            var block = WalletTestsHelpers.AppendTransactionInNewBlockToChain(chainInfo.chain, transaction);

            walletManager.ProcessTransaction(transaction, block: block);

            wallet = walletManager.GetWallet("myWallet1");

            var spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
            Assert.Equal(1, spendingAddress.Transactions.Count);
            Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
            Assert.Equal(transaction.Outputs[1].Value, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).DestinationScriptPubKey);
            Assert.Equal(chainInfo.block.GetHash(), spentAddressResult.Transactions.ElementAt(0).BlockHash);

            Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.Count);
            var destinationAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.ElementAt(0);
            Assert.Equal(block.GetHash(), destinationAddressResult.BlockHash);
            Assert.Equal(transaction.GetHash(), destinationAddressResult.Id);
            Assert.Equal(transaction.Outputs[1].Value, destinationAddressResult.Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, destinationAddressResult.ScriptPubKey);

            Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Count);
            var changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.ElementAt(0);
            Assert.Equal(block.GetHash(), destinationAddressResult.BlockHash);
            Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
            Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
            Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);
        }

        /// <summary>
        /// TODO: [SENDTRANSACTION] Conceptual changes had been introduced to tx sending.
        /// <para>
        /// These tests don't make sense anymore, it must be either removed or refactored.
        /// </para>
        /// </summary>
        //[Fact(Skip = "See TODO")]
        //public void SendTransactionWithoutMempoolValidatorProcessesTransactionAndBroadcastsTransactionToConnectionManagerNodes()
        //{
        //DataFolder dataFolder = CreateDataFolder(this);
        //Directory.CreateDirectory(dataFolder.WalletPath);

        //var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
        //var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
        //var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
        //var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
        //var changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

        //var spendingAddress = new HdAddress
        //{
        //    Index = 0,
        //    HdPath = $"m/44'/0'/0'/0/0",
        //    Address = spendingKeys.Address.ToString(),
        //    Pubkey = spendingKeys.PubKey.ScriptPubKey,
        //    ScriptPubKey = spendingKeys.Address.ScriptPubKey,
        //    Transactions = new List<TransactionData>()
        //};

        //var destinationAddress = new HdAddress
        //{
        //    Index = 1,
        //    HdPath = $"m/44'/0'/0'/0/1",
        //    Address = destinationKeys.Address.ToString(),
        //    Pubkey = destinationKeys.PubKey.ScriptPubKey,
        //    ScriptPubKey = destinationKeys.Address.ScriptPubKey,
        //    Transactions = new List<TransactionData>()
        //};

        //var changeAddress = new HdAddress
        //{
        //    Index = 0,
        //    HdPath = $"m/44'/0'/0'/1/0",
        //    Address = changeKeys.Address.ToString(),
        //    Pubkey = changeKeys.PubKey.ScriptPubKey,
        //    ScriptPubKey = changeKeys.Address.ScriptPubKey,
        //    Transactions = new List<TransactionData>()
        //};

        ////Generate a spendable transaction
        //var chainInfo = WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);
        //TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
        //spendingAddress.Transactions.Add(spendingTransaction);

        //wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
        //{
        //    Index = 0,
        //    Name = "account1",
        //    HdPath = "m/44'/0'/0'",
        //    ExtendedPubKey = accountKeys.ExtPubKey,
        //    ExternalAddresses = new List<HdAddress> { spendingAddress, destinationAddress },
        //    InternalAddresses = new List<HdAddress> { changeAddress }
        //});

        //// setup a payment to yourself
        //var transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));
        //transaction.Outputs.ElementAt(1).Value = Money.Zero;
        //transaction.Outputs.ElementAt(1).ScriptPubKey = Script.Empty;

        //var walletFeePolicy = new Mock<IWalletFeePolicy>();
        //walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
        //    .Returns(new Money(5000));

        //using (var nodeSocket = new NodeTcpListenerStub(Utils.ParseIpEndpoint("localhost", wallet.Network.DefaultPort)))
        //{
        //    using (var node = Node.ConnectToLocal(wallet.Network, new NodeConnectionParameters()))
        //    {
        //        var payloads = new List<Payload>();
        //        node.Filters.Add(new Action<IncomingMessage, Action>((i, a) => { a(); }),
        //                  new Action<Node, Payload, Action>((n, p, a) => { payloads.Add(p); a(); }));

        //        var nodeCollection = new NodesCollection();
        //        nodeCollection.Add(node);

        //        var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chainInfo.chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
        //            dataFolder, walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
        //        walletManager.Wallets.Add(wallet);

        //        var result = walletManager.SendTransaction(transaction.ToHex());

        //        Assert.True(result);
        //        var spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
        //        Assert.Equal(1, spendingAddress.Transactions.Count);
        //        Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
        //        Assert.Equal(0, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.Count);

        //        Assert.Equal(0, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.Count);

        //        Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Count);
        //        var changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.ElementAt(0);
        //        Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
        //        Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
        //        Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);

        //        Assert.Equal(1, payloads.Count);
        //        Assert.Equal(typeof(TxPayload), payloads[0].GetType());

        //        var payload = payloads[0] as TxPayload;
        //        var payloadTransaction = payload.Object;
        //        Assert.Equal(transaction.ToHex(), payloadTransaction.ToHex());
        //}
        //}
        //}

        /// <summary>
        /// TODO: [SENDTRANSACTION] Conceptual changes had been introduced to tx sending.
        /// <para>
        /// These tests don't make sense anymore, it must be either removed or refactored.
        /// </para>
        /// </summary>
        //[Fact(Skip = "See TODO")]
        //public void SendTransactionWithMempoolValidatorWithAcceptToMemoryPoolSuccessProcessesTransaction()
        //{
        //DataFolder dataFolder = CreateDataFolder(this);
        //Directory.CreateDirectory(dataFolder.WalletPath);

        //var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
        //var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
        //var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
        //var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
        //var changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

        //var spendingAddress = new HdAddress
        //{
        //    Index = 0,
        //    HdPath = $"m/44'/0'/0'/0/0",
        //    Address = spendingKeys.Address.ToString(),
        //    Pubkey = spendingKeys.PubKey.ScriptPubKey,
        //    ScriptPubKey = spendingKeys.Address.ScriptPubKey,
        //    Transactions = new List<TransactionData>()
        //};

        //var destinationAddress = new HdAddress
        //{
        //    Index = 1,
        //    HdPath = $"m/44'/0'/0'/0/1",
        //    Address = destinationKeys.Address.ToString(),
        //    Pubkey = destinationKeys.PubKey.ScriptPubKey,
        //    ScriptPubKey = destinationKeys.Address.ScriptPubKey,
        //    Transactions = new List<TransactionData>()
        //};

        //var changeAddress = new HdAddress
        //{
        //    Index = 0,
        //    HdPath = $"m/44'/0'/0'/1/0",
        //    Address = changeKeys.Address.ToString(),
        //    Pubkey = changeKeys.PubKey.ScriptPubKey,
        //    ScriptPubKey = changeKeys.Address.ScriptPubKey,
        //    Transactions = new List<TransactionData>()
        //};

        ////Generate a spendable transaction
        //var chainInfo = WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);
        //TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
        //spendingAddress.Transactions.Add(spendingTransaction);

        //wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
        //{
        //    Index = 0,
        //    Name = "account1",
        //    HdPath = "m/44'/0'/0'",
        //    ExtendedPubKey = accountKeys.ExtPubKey,
        //    ExternalAddresses = new List<HdAddress> { spendingAddress, destinationAddress },
        //    InternalAddresses = new List<HdAddress> { changeAddress }
        //});

        //// setup a payment to yourself
        //var transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));
        //transaction.Outputs.ElementAt(1).Value = Money.Zero;
        //transaction.Outputs.ElementAt(1).ScriptPubKey = Script.Empty;

        //var walletFeePolicy = new Mock<IWalletFeePolicy>();
        //walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
        //    .Returns(new Money(5000));

        //using (var nodeSocket = new NodeTcpListenerStub(Utils.ParseIpEndpoint("localhost", wallet.Network.DefaultPort)))
        //{
        //    using (var node = Node.ConnectToLocal(wallet.Network, new NodeConnectionParameters()))
        //    {
        //        var payloads = new List<Payload>();
        //        node.Filters.Add(new Action<IncomingMessage, Action>((i, a) => { a(); }),
        //                  new Action<Node, Payload, Action>((n, p, a) => { payloads.Add(p); a(); }));

        //        var nodeCollection = new NodesCollection();
        //        nodeCollection.Add(node);

        //        var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chainInfo.chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
        //            dataFolder, walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
        //        walletManager.Wallets.Add(wallet);

        //        var result = walletManager.SendTransaction(transaction.ToHex());

        //        Assert.True(result);
        //        // verify AcceptToMemoryPool has been called.
        //        mempoolValidator.Verify();

        //        var spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
        //        Assert.Equal(1, spendingAddress.Transactions.Count);
        //        Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
        //        Assert.Equal(0, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.Count);

        //        Assert.Equal(0, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.Count);

        //        Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Count);
        //        var changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.ElementAt(0);
        //        Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
        //        Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
        //        Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);

        //        Assert.Equal(1, payloads.Count);
        //        Assert.Equal(typeof(TxPayload), payloads[0].GetType());

        //        var payload = payloads[0] as TxPayload;
        //        var payloadTransaction = payload.Object;
        //        Assert.Equal(transaction.ToHex(), payloadTransaction.ToHex());
        //    }
        //}
        //}

        /// <summary>
        /// TODO: [SENDTRANSACTION] Conceptual changes had been introduced to tx sending.
        /// <para>
        /// These tests don't make sense anymore, it must be either removed or refactored.
        /// </para>
        /// </summary>
        //[Fact(Skip = "See TODO")]
        //public void SendTransactionWithMempoolValidatorWithAcceptToMemoryPoolFailedDoesNotProcessesTransaction()
        //{
        //DataFolder dataFolder = CreateDataFolder(this);
        //Directory.CreateDirectory(dataFolder.WalletPath);

        //var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
        //var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
        //var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
        //var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
        //var changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

        //var spendingAddress = new HdAddress
        //{
        //    Index = 0,
        //    HdPath = $"m/44'/0'/0'/0/0",
        //    Address = spendingKeys.Address.ToString(),
        //    Pubkey = spendingKeys.PubKey.ScriptPubKey,
        //    ScriptPubKey = spendingKeys.Address.ScriptPubKey,
        //    Transactions = new List<TransactionData>()
        //};

        //var destinationAddress = new HdAddress
        //{
        //    Index = 1,
        //    HdPath = $"m/44'/0'/0'/0/1",
        //    Address = destinationKeys.Address.ToString(),
        //    Pubkey = destinationKeys.PubKey.ScriptPubKey,
        //    ScriptPubKey = destinationKeys.Address.ScriptPubKey,
        //    Transactions = new List<TransactionData>()
        //};

        //var changeAddress = new HdAddress
        //{
        //    Index = 0,
        //    HdPath = $"m/44'/0'/0'/1/0",
        //    Address = changeKeys.Address.ToString(),
        //    Pubkey = changeKeys.PubKey.ScriptPubKey,
        //    ScriptPubKey = changeKeys.Address.ScriptPubKey,
        //    Transactions = new List<TransactionData>()
        //};

        ////Generate a spendable transaction
        //var chainInfo = WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);
        //TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
        //spendingAddress.Transactions.Add(spendingTransaction);

        //wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
        //{
        //    Index = 0,
        //    Name = "account1",
        //    HdPath = "m/44'/0'/0'",
        //    ExtendedPubKey = accountKeys.ExtPubKey,
        //    ExternalAddresses = new List<HdAddress> { spendingAddress, destinationAddress },
        //    InternalAddresses = new List<HdAddress> { changeAddress }
        //});

        //// setup a payment to yourself
        //var transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));
        //transaction.Outputs.ElementAt(1).Value = Money.Zero;
        //transaction.Outputs.ElementAt(1).ScriptPubKey = Script.Empty;

        //var walletFeePolicy = new Mock<IWalletFeePolicy>();
        //walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
        //    .Returns(new Money(5000));

        //using (var nodeSocket = new NodeTcpListenerStub(Utils.ParseIpEndpoint("localhost", wallet.Network.DefaultPort)))
        //{
        //    using (var node = Node.ConnectToLocal(wallet.Network, new NodeConnectionParameters()))
        //    {
        //        var payloads = new List<Payload>();
        //        node.Filters.Add(new Action<IncomingMessage, Action>((i, a) => { a(); }),
        //                  new Action<Node, Payload, Action>((n, p, a) => { payloads.Add(p); a(); }));

        //        var nodeCollection = new NodesCollection();
        //        nodeCollection.Add(node);

        //        var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chainInfo.chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
        //            dataFolder, walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
        //        walletManager.Wallets.Add(wallet);

        //        var result = walletManager.SendTransaction(transaction.ToHex());

        //        Assert.False(result);
        //        // verify AcceptToMemoryPool has been called.
        //        mempoolValidator.Verify();

        //        var spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
        //        Assert.Equal(1, spendingAddress.Transactions.Count);
        //        Assert.Null(spentAddressResult.Transactions.ElementAt(0).SpendingDetails);
        //        Assert.Null(spentAddressResult.Transactions.ElementAt(0).SpendingDetails);
        //        Assert.Equal(0, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.Count);
        //        Assert.Equal(0, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Count);
        //        Assert.Equal(0, payloads.Count);
        //    }
        //}
        //}

        [Fact]
        public void RemoveBlocksRemovesTransactionsWithHigherBlockHeightAndUpdatesLastSyncedBlockHeight()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var trxId = uint256.Parse("21e74d1daed6dec93d58396a3406803c5fc8d220b59f4b4dd185cab5f7a9a22e");
            int trxCount = 0;
            var concurrentchain = new ConcurrentChain(this.Network);
            var chainedHeader = WalletTestsHelpers.AppendBlock(null, concurrentchain).ChainedHeader;
            chainedHeader = WalletTestsHelpers.AppendBlock(chainedHeader, concurrentchain).ChainedHeader;
            chainedHeader = WalletTestsHelpers.AppendBlock(chainedHeader, concurrentchain).ChainedHeader;



            var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            var account = wallet.AccountsRoot.ElementAt(0).Accounts.FirstOrDefault();
            account.Name = "First account";
            PlantTransactionsToAddresses(account.ExternalAddresses, WalletTestsHelpers.CreateSpentTransactionsOfBlockHeights(this.Network, 1, 2, 3, 4));
            PlantTransactionsToAddresses(account.InternalAddresses, WalletTestsHelpers.CreateSpentTransactionsOfBlockHeights(this.Network, 1, 2, 3, 4));


            // reorg at block 3

            // Trx at block 0 is not spent
            wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).Transactions.First().Id = new uint256((ulong)trxCount++);
            wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).Transactions.First().SpendingDetails = null;
            wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).Transactions.First().BlockHeight = 0;
            
            //internal
            wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.First().Id = new uint256((ulong)trxCount++);
            wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.First().SpendingDetails = null;
            wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).Transactions.First().BlockHeight = 0;

            // Trx at block 2 is spent in block 3, after reorg it will not be spendable.
            wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.First().SpendingDetails.TransactionId = new uint256((ulong)trxCount++);
            wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.First().SpendingDetails.BlockHeight = 3;
            wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(2).Transactions.First().BlockHeight = 2;
            //internal
            wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(1).Transactions.First().SpendingDetails.TransactionId = new uint256((ulong)trxCount++);
            wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(1).Transactions.First().SpendingDetails.BlockHeight = 3;
            wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(2).Transactions.First().BlockHeight = 2;

            // Trx at block 3 is spent at block 5, after reorg it will be spendable.
            wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(2).Transactions.First().SpendingDetails.TransactionId = new uint256((ulong)trxCount++);
            wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(2).Transactions.First().SpendingDetails.BlockHeight = 5;
            wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(2).Transactions.First().BlockHeight = 3;
            //internal
            wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(2).Transactions.First().SpendingDetails.TransactionId = new uint256((ulong)trxCount++);
            wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(2).Transactions.First().SpendingDetails.BlockHeight = 5;
            wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(2).Transactions.First().BlockHeight = 3;


            walletManager.SaveWallet(wallet, true);
            walletManager.LoadKeysLookupLock();

            wallet = walletManager.GetWallet("myWallet1");
            var account1 = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0);
            Assert.Equal(6, account1.InternalAddresses.Concat(account1.ExternalAddresses).SelectMany(r => r.Transactions).Count());

            walletManager.RemoveBlocks(chainedHeader);

            wallet = walletManager.GetWallet("myWallet1");

            Assert.Equal(chainedHeader.GetLocator().Blocks, wallet.BlockLocator);
            Assert.Equal(chainedHeader.Height, wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHeight);
            Assert.Equal(chainedHeader.HashBlock, wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHash);
            Assert.Equal(chainedHeader.HashBlock, walletManager.WalletTipHash);

            account1 = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0);

            Assert.Equal(6, account1.InternalAddresses.Concat(account1.ExternalAddresses).SelectMany(r => r.Transactions).Count());
            Assert.True(account1.InternalAddresses.Concat(account1.ExternalAddresses).SelectMany(r => r.Transactions).All(r => r.BlockHeight <= chainedHeader.Height));
            Assert.True(account1.InternalAddresses.Concat(account1.ExternalAddresses).SelectMany(r => r.Transactions).All(r => r.SpendingDetails == null || r.SpendingDetails.BlockHeight == null || r.SpendingDetails.BlockHeight <= chainedHeader.Height));
            Assert.Equal(4, account1.InternalAddresses.Concat(account1.ExternalAddresses).SelectMany(r => r.Transactions).Count(t => t.SpendingDetails == null));
        }


        [Fact]
        public void ProcessBlockWithoutWalletsSetsWalletTipToBlockHash()
        {
            var concurrentchain = new ConcurrentChain(this.Network);
            var blockResult = WalletTestsHelpers.AppendBlock(null, concurrentchain);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            walletManager.ProcessBlock(blockResult.Block, blockResult.ChainedHeader);

            Assert.Equal(blockResult.ChainedHeader.HashBlock, walletManager.WalletTipHash);
        }

        [Fact]
        public void ProcessBlockWithWalletsProcessesTransactionsOfBlockToWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);

            var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            wallet.AccountsRoot.FirstOrDefault().Accounts.Clear();

            var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
            var changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

            var spendingAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            var destinationAddress = new HdAddress
            {
                Index = 1,
                HdPath = $"m/44'/0'/0'/0/1",
                Address = destinationKeys.Address.ToString(),
                Pubkey = destinationKeys.PubKey.ScriptPubKey,
                ScriptPubKey = destinationKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            var changeAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/1/0",
                Address = changeKeys.Address.ToString(),
                Pubkey = changeKeys.PubKey.ScriptPubKey,
                ScriptPubKey = changeKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            //Generate a spendable transaction
            var chainInfo = WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);

            TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
            spendingAddress.Transactions.Add(spendingTransaction);

            // setup a payment to yourself in a new block.
            var transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));
            var block = WalletTestsHelpers.AppendTransactionInNewBlockToChain(chainInfo.chain, transaction);

            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "account1",
                HdPath = "m/44'/0'/0'",
                ExtendedPubKey = accountKeys.ExtPubKey,
                ExternalAddresses = new List<HdAddress> { spendingAddress, destinationAddress },
                InternalAddresses = new List<HdAddress> { changeAddress }
            });

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
                .Returns(new Money(5000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chainInfo.chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            walletManager.SaveWallet(wallet, true);
            walletManager.LoadKeysLookupLock();
            walletManager.WalletTipHash = block.Header.GetHash();

            var chainedBlock = chainInfo.chain.GetBlock(block.GetHash());
            walletManager.ProcessBlock(block, chainedBlock);
            wallet = walletManager.GetWallet("myWallet1");

            var spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
            Assert.Equal(1, spendingAddress.Transactions.Count);
            Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
            Assert.Equal(transaction.Outputs[1].Value, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).DestinationScriptPubKey);

            Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.Count);
            var destinationAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.ElementAt(0);
            Assert.Equal(transaction.GetHash(), destinationAddressResult.Id);
            Assert.Equal(transaction.Outputs[1].Value, destinationAddressResult.Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, destinationAddressResult.ScriptPubKey);

            Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Count);
            var changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.ElementAt(0);
            Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
            Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
            Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);

            Assert.Equal(chainedBlock.GetLocator().Blocks, wallet.BlockLocator);
            Assert.Equal(chainedBlock.Height, wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHeight);
            Assert.Equal(chainedBlock.HashBlock, wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHash);
            Assert.Equal(chainedBlock.HashBlock, walletManager.WalletTipHash);
        }

        [Fact]
        public void ProcessBlockWithWalletTipBlockNotOnChainYetThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                DataFolder dataFolder = CreateDataFolder(this);
                Directory.CreateDirectory(dataFolder.WalletPath);

                var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");

                ConcurrentChain chain = new ConcurrentChain(wallet.Network.GetGenesis().Header);
                var chainResult = WalletTestsHelpers.AppendBlock(chain.Genesis, chain);

                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                    dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
                walletManager.SaveWallet(wallet);

                walletManager.WalletTipHash = new uint256(15012522521);

                walletManager.ProcessBlock(chainResult.Block, chainResult.ChainedHeader);
            });
        }

        [Fact]
        public void ProcessBlockWithBlockAheadOfWalletThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                DataFolder dataFolder = CreateDataFolder(this);
                Directory.CreateDirectory(dataFolder.WalletPath);

                var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");

                ConcurrentChain chain = new ConcurrentChain(wallet.Network.GetGenesis().Header);
                var chainResult = WalletTestsHelpers.AppendBlock(chain.Genesis, chain);
                var chainResult2 = WalletTestsHelpers.AppendBlock(chainResult.ChainedHeader, chain);

                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                    dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
                walletManager.SaveWallet(wallet);

                walletManager.WalletTipHash = wallet.Network.GetGenesis().Header.GetHash();

                walletManager.ProcessBlock(chainResult2.Block, chainResult2.ChainedHeader);
            });
        }

        [Fact]
        public void CheckWalletBalanceEstimationWithConfirmedTransactions()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            // generate 3 wallet with 2 accounts containing 20 external and 20 internal addresses each.
            const string name = "wallet1";
            const string pass = "pass";

            var template = WalletTestsHelpers.GenerateBlankWalletWithExtKey(name, pass, this.Network);
            walletManager.SaveWallet(template.wallet);

            var firstAccount = walletManager.GetWalletByName(name).AccountsRoot.First().Accounts.First();

            // add two unconfirmed transactions
            for (int i = 1; i < 3; i++)
            {
                firstAccount.InternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10 });
                firstAccount.ExternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10 });
            }

            Assert.Equal(0, firstAccount.GetSpendableAmount().ConfirmedAmount);
            Assert.Equal(40, firstAccount.GetSpendableAmount().UnConfirmedAmount);
        }


        [Fact]
        public void GetAccountBalancesReturnsCorrectAccountBalances()
        {
            // Arrange.
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var walletName = Guid.NewGuid().ToString();
            var tuple = WalletTestsHelpers.GenerateBlankWalletWithExtKey(walletName, Guid.NewGuid().ToString(),this.Network,2);
            Wallet wallet = tuple.wallet;

            HdAccount account = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0);

            HdAddress accountAddress1 = account.ExternalAddresses.ElementAt(0);
            accountAddress1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(15000), null, null, null, accountAddress1.ScriptPubKey));
            accountAddress1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(2), new Money(10000), 1, null, null, accountAddress1.ScriptPubKey));

            HdAddress accountAddress2 = account.ExternalAddresses.ElementAt(1);
            accountAddress2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(3), new Money(20000), null, null, null, accountAddress2.ScriptPubKey));
            accountAddress2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(4), new Money(120000), 2, null, null, accountAddress2.ScriptPubKey));

            HdAccount account2 = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(1);
            HdAddress accountAddress21 = account2.ExternalAddresses.ElementAt(0);
            accountAddress21.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(5), new Money(74000), null, null, null, accountAddress21.ScriptPubKey));
            accountAddress21.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(6), new Money(18700), 3, null, null, accountAddress21.ScriptPubKey));

            HdAddress accountAddress22 = account2.ExternalAddresses.ElementAt(1);
            accountAddress22.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(7), new Money(65000), null, null, null, accountAddress22.ScriptPubKey));
            accountAddress22.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(8), new Money(89300), 4, null, null, accountAddress22.ScriptPubKey));

            walletManager.SaveWallet(wallet,true);

            // Act.
            var balances = walletManager.GetBalances(walletName);

            // Assert.
            AccountBalance resultingBalance = balances.First();
            Assert.Equal(account.Name, resultingBalance.Account.Name);
            Assert.Equal(account.HdPath, resultingBalance.Account.HdPath);
            Assert.Equal(new Money(130000), resultingBalance.AmountConfirmed);
            Assert.Equal(new Money(35000), resultingBalance.AmountUnconfirmed);

            resultingBalance = balances.ElementAt(1);
            Assert.Equal(account2.Name, resultingBalance.Account.Name);
            Assert.Equal(account2.HdPath, resultingBalance.Account.HdPath);
            Assert.Equal(new Money(108000), resultingBalance.AmountConfirmed);
            Assert.Equal(new Money(139000), resultingBalance.AmountUnconfirmed);
        }

        [Fact]
        public void CheckWalletBalanceEstimationWithUnConfirmedTransactions()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            const string name = "wallet1";
            const string pass = "pass";

            var template = WalletTestsHelpers.GenerateBlankWalletWithExtKey(name, pass, this.Network);
            walletManager.SaveWallet(template.wallet);

            var firstAccount = walletManager.GetWalletByName(name).AccountsRoot.First().Accounts.First();

            // add two confirmed transactions
            for (int i = 1; i < 3; i++)
            {
                firstAccount.InternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10, BlockHeight = 10 });
                firstAccount.ExternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10, BlockHeight = 10 });
            }

            Assert.Equal(40, firstAccount.GetSpendableAmount().ConfirmedAmount);
            Assert.Equal(0, firstAccount.GetSpendableAmount().UnConfirmedAmount);
        }


        [Fact]
        public void CheckWalletBalanceEstimationWithSpentTransactions()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            // generate 3 wallet with 2 accounts containing 20 external and 20 internal addresses each.
            const string name = "wallet1";
            var template1 = WalletTestsHelpers.GenerateBlankWalletWithExtKey(name, "pass1", this.Network);
            walletManager.SaveWallet(template1.wallet);

            var firstAccount = walletManager.GetWalletByName(name).AccountsRoot.First().Accounts.First();

            // add two spent transactions
            for (int i = 1; i < 3; i++)
            {
                firstAccount.InternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10, BlockHeight = 10, SpendingDetails = new SpendingDetails() });
                firstAccount.ExternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10, BlockHeight = 10, SpendingDetails = new SpendingDetails() });
            }

            Assert.Equal(0, firstAccount.GetSpendableAmount().ConfirmedAmount);
            Assert.Equal(0, firstAccount.GetSpendableAmount().UnConfirmedAmount);
        }


        [Fact]
        public void CheckWalletBalanceEstimationWithSpentAndConfirmedTransactions()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            // generate 1 wallet with 2 accounts containing 20 external and 20 internal addresses each.
            const string name = "wallet1";
            const string pass = "pass";

            var template = WalletTestsHelpers.GenerateBlankWalletWithExtKey(name, pass, this.Network);
            walletManager.SaveWallet(template.wallet);

            var firstAccount = walletManager.GetWalletByName(name).AccountsRoot.First().Accounts.First();

            // add two spent transactions
            for (int i = 1; i < 3; i++)
            {
                firstAccount.InternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10, BlockHeight = 10, SpendingDetails = new SpendingDetails() });
                firstAccount.ExternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10, BlockHeight = 10, SpendingDetails = new SpendingDetails() });
            }

            for (int i = 3; i < 5; i++)
            {
                firstAccount.InternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10, BlockHeight = 10 });
                firstAccount.ExternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10, BlockHeight = 10 });
            }

            Assert.Equal(40, firstAccount.GetSpendableAmount().ConfirmedAmount);
            Assert.Equal(0, firstAccount.GetSpendableAmount().UnConfirmedAmount);
        }



        [Fact]
        public void CheckWalletBalanceEstimationWithSpentAndUnConfirmedTransactions()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            const string name = "wallet1";
            const string pass = "pass";

            var template = WalletTestsHelpers.GenerateBlankWalletWithExtKey(name, pass, this.Network);
            walletManager.SaveWallet(template.wallet);


            var firstAccount = walletManager.GetWalletByName(name).AccountsRoot.First().Accounts.First();

            // add two spent transactions
            for (int i = 1; i < 3; i++)
            {
                firstAccount.InternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10, BlockHeight = 10, SpendingDetails = new SpendingDetails() });
                firstAccount.ExternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10, BlockHeight = 10, SpendingDetails = new SpendingDetails() });
            }

            for (int i = 3; i < 5; i++)
            {
                firstAccount.InternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10 });
                firstAccount.ExternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10 });
            }

            Assert.Equal(0, firstAccount.GetSpendableAmount().ConfirmedAmount);
            Assert.Equal(40, firstAccount.GetSpendableAmount().UnConfirmedAmount);
        }




        [Fact]
        public void SaveToFileWithWalletParameterSavesGivenWalletToDisk()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);
            var wallet = this.walletFixture.GenerateBlankWallet("wallet1", "test");
            var wallet2 = this.walletFixture.GenerateBlankWallet("wallet2", "test");
            Wallet deletePlaceholder;
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            Assert.Throws<WalletException>(() => walletManager.GetWalletByName("wallet1"));
            Assert.Throws<WalletException>(() => walletManager.GetWalletByName("wallet2"));

            walletManager.SaveWallet(wallet);
            //walletManager.Wallets.Remove("wallet1", out deletePlaceholder);
            walletManager.LoadWallet("test", "wallet1");

            Assert.True(walletManager.GetWalletByName("wallet1") != null);
            Assert.Throws<WalletException>(() => walletManager.GetWalletByName("wallet2"));

            var resultWallet = walletManager.GetWalletByName("wallet1");
            Assert.Equal(wallet.Name, resultWallet.Name);
            Assert.Equal(wallet.EncryptedSeed, resultWallet.EncryptedSeed);
            Assert.Equal(wallet.ChainCode, resultWallet.ChainCode);
            Assert.Equal(wallet.Network, resultWallet.Network);
            Assert.Equal(wallet.AccountsRoot.Count, resultWallet.AccountsRoot.Count);
        }

     

        [Fact]
        public void GetWalletsReturnsLoadedWalletNames()
        {
            string walletName1 = "wallet1";
            string walletName2 = "wallet2";
            var wallet = WalletTestsHelpers.GenerateBlankWallet(walletName1, Guid.NewGuid().ToString(), this.Network);
            var wallet2 = WalletTestsHelpers.GenerateBlankWallet(walletName2, Guid.NewGuid().ToString(), this.Network);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            walletManager.SaveWallet(wallet);
            walletManager.SaveWallet(wallet2);

            var result = walletManager.GetWalletNames().OrderBy(w => w).ToArray();

            Assert.Equal(2, result.Count());
            Assert.Equal(walletName1, result[0]);
            Assert.Equal(walletName2, result[1]);
        }

        [Fact]
        public void GetWalletsWithoutLoadedWalletsReturnsEmptyList()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var result = walletManager.GetWalletNames().OrderBy(w => w);

            Assert.Empty(result);
        }
        
        [Fact]
        public void LoadKeysLookupWithKeysLoadsKeyLookup()
        {

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
              CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            var account = wallet.AccountsRoot.ElementAt(0).Accounts.FirstOrDefault();
            account.Name = "First account";
            PlantTransactionsToAddresses(account.ExternalAddresses,WalletTestsHelpers.CreateSpentTransactionsOfBlockHeights(this.Network,1, 2, 3));
            PlantTransactionsToAddresses(account.InternalAddresses,WalletTestsHelpers.CreateSpentTransactionsOfBlockHeights(this.Network,1, 2, 3));
            walletManager.SaveWallet(wallet);

            walletManager.LoadKeysLookupLock();

            Assert.NotNull(walletManager.addressByScriptLookup);
            Assert.Equal(80, walletManager.addressByScriptLookup.Count);

            var externalAddresses = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses;
            Assert.Equal(externalAddresses.ElementAt(0).Address, walletManager.addressByScriptLookup[externalAddresses.ElementAt(0).ScriptPubKey.Hash].HdAddress.Address);
            Assert.Equal(externalAddresses.ElementAt(1).Address, walletManager.addressByScriptLookup[externalAddresses.ElementAt(1).ScriptPubKey.Hash].HdAddress.Address);
            Assert.Equal(externalAddresses.ElementAt(2).Address, walletManager.addressByScriptLookup[externalAddresses.ElementAt(2).ScriptPubKey.Hash].HdAddress.Address);

            var internalAddresses = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses;
            Assert.Equal(internalAddresses.ElementAt(0).Address, walletManager.addressByScriptLookup[internalAddresses.ElementAt(0).ScriptPubKey.Hash].HdAddress.Address);
            Assert.Equal(internalAddresses.ElementAt(1).Address, walletManager.addressByScriptLookup[internalAddresses.ElementAt(1).ScriptPubKey.Hash].HdAddress.Address);
            Assert.Equal(internalAddresses.ElementAt(2).Address, walletManager.addressByScriptLookup[internalAddresses.ElementAt(2).ScriptPubKey.Hash].HdAddress.Address);
        }


        [Fact]
        public void LoadKeysLookupWithoutWalletsInitializesEmptyDictionary()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            walletManager.LoadKeysLookupLock();

            Assert.NotNull(walletManager.addressLookup);
            Assert.Empty(walletManager.addressLookup);
        }

        [Fact]
        public void CreateBip44PathWithChangeAddressReturnsPath()
        {
            var result = HdOperations.CreateHdPath((int)CoinType.BRhodium, 4, 3, true);

            Assert.Equal("m/44'/10291'/4'/1/3", result);
        }

        [Fact]
        public void CreateBip44PathWithoutChangeAddressReturnsPath()
        {
            var result = HdOperations.CreateHdPath((int)CoinType.BRhodium, 4, 3, false);

            Assert.Equal("m/44'/10291'/4'/0/3", result);
        }

    

        [Fact]
        public void UpdateLastBlockSyncedHeightWithChainedBlockUpdatesWallets()
        {
            var wallet = WalletTestsHelpers.GenerateBlankWallet(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(),this.Network);
            var wallet2 = WalletTestsHelpers.GenerateBlankWallet(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), this.Network);

            ConcurrentChain chain = new ConcurrentChain(wallet.Network.GetGenesis().Header);
            var chainedBlock = WalletTestsHelpers.AppendBlock(chain.Genesis, chain).ChainedHeader;

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            walletManager.SaveWallet(wallet);
            walletManager.SaveWallet(wallet2);

            walletManager.WalletTipHash = new uint256(125125125);

            walletManager.UpdateLastBlockSyncedHeight(chainedBlock);

            Assert.Equal(chainedBlock.HashBlock, walletManager.WalletTipHash);
            foreach (var name in walletManager.GetWalletNames())
            {
                var w = walletManager.GetWalletByName(name);
                Assert.Equal(chainedBlock.GetLocator().Blocks, w.BlockLocator);
                Assert.Equal(chainedBlock.Height, w.AccountsRoot.ElementAt(0).LastBlockSyncedHeight);
                Assert.Equal(chainedBlock.HashBlock, w.AccountsRoot.ElementAt(0).LastBlockSyncedHash);
            }
        }

        [Fact]
        public void UpdateLastBlockSyncedHeightWithWalletAndChainedBlockUpdatesGivenWallet()
        {
            string walletName = Guid.NewGuid().ToString();
            var wallet = WalletTestsHelpers.GenerateBlankWallet(walletName, Guid.NewGuid().ToString(), this.Network);
            var wallet2 = WalletTestsHelpers.GenerateBlankWallet(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), this.Network);

            ConcurrentChain chain = new ConcurrentChain(wallet.Network.GetGenesis().Header);
            var chainedBlock = WalletTestsHelpers.AppendBlock(chain.Genesis, chain).ChainedHeader;

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            walletManager.SaveWallet(wallet);
            walletManager.SaveWallet(wallet2);
                       

            walletManager.WalletTipHash = new uint256(125125125);

            walletManager.UpdateLastBlockSyncedHeight(chainedBlock);
            wallet = walletManager.GetWallet(walletName);

            Assert.Equal(chainedBlock.GetLocator().Blocks, wallet.BlockLocator);
            Assert.Equal(chainedBlock.Height, wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHeight);
            Assert.Equal(chainedBlock.HashBlock, wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHash);
            Assert.Equal(chainedBlock.HashBlock, walletManager.WalletTipHash);
        }


        [Fact]
        public void RemoveAllTransactionsInWalletReturnsRemovedTransactionsList()
        {
            // Arrange.
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            // Generate a wallet with an account and a few transactions.
            string name = Guid.NewGuid().ToString();
            string pass = Guid.NewGuid().ToString();

            var template = WalletTestsHelpers.GenerateBlankWalletWithExtKey(name, pass, this.Network);
            var wallet = template.wallet;

            var firstAccount = wallet.AccountsRoot.First().Accounts.First();

            // Add two unconfirmed transactions.
            uint256 trxId = uint256.Parse("d6043add63ec364fcb591cf209285d8e60f1cc06186d4dcbce496cdbb4303400");
            int counter = 0;

            for (int i = 0; i < 3; i++)
            {
                firstAccount.ExternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10, Id = trxId >> counter++, ScriptPubKey = new Script() });
                firstAccount.InternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10, Id = trxId >> counter++, ScriptPubKey = new Script() });
            }

            // Add two confirmed transactions.
            for (int i = 3; i < 6; i++)
            {
                firstAccount.InternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10, Id = trxId >> counter++, ScriptPubKey = new Script() });
                firstAccount.ExternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10, Id = trxId >> counter++, ScriptPubKey = new Script() });
            }

            var transactionCount = firstAccount.GetCombinedAddresses().SelectMany(a => a.Transactions).Count();
            Assert.Equal(12, transactionCount);

            walletManager.SaveWallet(wallet, true);
            // Act.
            var result = walletManager.RemoveAllTransactions(name);
            wallet = walletManager.GetWallet(name);

            // Assert.
            Assert.Empty(wallet.AccountsRoot.First().Accounts.First().GetCombinedAddresses().SelectMany(a => a.Transactions));
            Assert.Equal(12, result.Count);
        }

        [Fact]
        public void RemoveAllTransactionsWhenNoTransactionsArePresentReturnsEmptyList()
        {
            // Arrange.
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            // Generate a wallet with an account and no transactions.
            string name = "wallet1";
            string pass = Guid.NewGuid().ToString();

            var template = WalletTestsHelpers.GenerateBlankWalletWithExtKey(name, pass, this.Network);

            walletManager.SaveWallet(template.wallet,true);
            var wallet = template.wallet;

            var firstAccount = wallet.AccountsRoot.First().Accounts.First();

            var transactionCount = firstAccount.GetCombinedAddresses().SelectMany(a => a.Transactions).Count();
            Assert.Equal(0, transactionCount);

            // Act.
            var result = walletManager.RemoveAllTransactions("wallet1");

            // Assert.
            Assert.Empty(firstAccount.GetCombinedAddresses().SelectMany(a => a.Transactions));
            Assert.Empty(result);
        }


        [Fact]
        public void RemoveTransactionsByIdsWhenTransactionsAreUnconfirmedReturnsRemovedTransactionsList()
        {
            // Arrange.
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            // Generate a wallet with an account and a few transactions.
            string name = "wallet1";
            string pass = Guid.NewGuid().ToString();

            var template = WalletTestsHelpers.GenerateBlankWalletWithExtKey(name, pass, this.Network);
            
            var wallet = template.wallet;

            var firstAccount = wallet.AccountsRoot.First().Accounts.First();

            // Add two unconfirmed transactions.
            uint256 trxId = uint256.Parse("d6043add63ec364fcb591cf209285d8e60f1cc06186d4dcbce496cdbb4303400");
            int counter = 0;

            TransactionData trxUnconfirmed1 = new TransactionData { Amount = 10, ScriptPubKey = new Script(), Id = trxId >> counter++ };
            TransactionData trxUnconfirmed2 = new TransactionData { Amount = 10, ScriptPubKey = new Script(), Id = trxId >> counter++ };
            TransactionData trxConfirmed1 = new TransactionData { Amount = 10, ScriptPubKey = new Script(), Id = trxId >> counter++, BlockHeight = 50000 };
            TransactionData trxConfirmed2 = new TransactionData { Amount = 10, ScriptPubKey = new Script(), Id = trxId >> counter++, BlockHeight = 50001 };

            firstAccount.ExternalAddresses.ElementAt(0).Transactions.Add(trxUnconfirmed1);
            firstAccount.ExternalAddresses.ElementAt(1).Transactions.Add(trxConfirmed1);
            firstAccount.InternalAddresses.ElementAt(0).Transactions.Add(trxUnconfirmed2);
            firstAccount.InternalAddresses.ElementAt(1).Transactions.Add(trxConfirmed2);

            walletManager.SaveWallet(template.wallet,true);

            var transactionCount = firstAccount.GetCombinedAddresses().SelectMany(a => a.Transactions).Count();
            Assert.Equal(4, transactionCount);

            // Act.
            var result = walletManager.RemoveTransactionsByIds("wallet1", new[] { trxUnconfirmed1.Id, trxUnconfirmed2.Id, trxConfirmed1.Id, trxConfirmed2.Id });
            wallet = walletManager.GetWallet(name);

            // Assert.
            var remainingTrxs = wallet.AccountsRoot.First().Accounts.First().GetCombinedAddresses().SelectMany(a => a.Transactions).ToList();
            Assert.Equal(2, remainingTrxs.Count());
            Assert.Equal(2, result.Count);
            Assert.Contains((trxUnconfirmed1.Id, trxConfirmed1.CreationTime), result);
            Assert.Contains((trxUnconfirmed2.Id, trxConfirmed2.CreationTime), result);
            Assert.DoesNotContain(trxUnconfirmed1, remainingTrxs);
            Assert.DoesNotContain(trxUnconfirmed2, remainingTrxs);
        }


        
        [Fact]
        public void RemoveTransactionsByIdsAlsoRemovesUnconfirmedSpendingDetailsTransactions()
        {
            // Arrange.
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(this.Network), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            // Generate a wallet with an account and a few transactions.
            const string name = "wallet1";
            const string pass = "pass";

            var template = WalletTestsHelpers.GenerateBlankWalletWithExtKey(name, pass, this.Network);
            WalletTestsHelpers.AddAddressesToWallet(template.wallet, 20, template.key);
            walletManager.SaveWallet(template.wallet);
            var wallet = template.wallet;

            var firstAccount = wallet.AccountsRoot.First().Accounts.First();

            // Add two unconfirmed transactions.
            uint256 trxId = uint256.Parse("d6043add63ec364fcb591cf209285d8e60f1cc06186d4dcbce496cdbb4303400");
            int counter = 0;

            // Confirmed transaction with confirmed spending.
            SpendingDetails confirmedSpendingDetails = new SpendingDetails { TransactionId = trxId >> counter++, BlockHeight = 500002 };
            TransactionData trxConfirmed1 = new TransactionData { Amount = 10, Id = trxId >> counter++, BlockHeight = 50000, SpendingDetails = confirmedSpendingDetails };

            // Confirmed transaction with unconfirmed spending.
            uint256 unconfirmedTransactionId = trxId >> counter++;
            SpendingDetails unconfirmedSpendingDetails1 = new SpendingDetails { TransactionId = unconfirmedTransactionId };
            TransactionData trxConfirmed2 = new TransactionData { Amount = 10, Id = trxId >> counter++, BlockHeight = 50001, SpendingDetails = unconfirmedSpendingDetails1 };
            
            // Unconfirmed transaction.
            TransactionData trxUnconfirmed1 = new TransactionData { Amount = 10, Id = unconfirmedTransactionId };
            
            firstAccount.ExternalAddresses.ElementAt(0).Transactions.Add(trxUnconfirmed1);
            firstAccount.ExternalAddresses.ElementAt(1).Transactions.Add(trxConfirmed1);
            firstAccount.InternalAddresses.ElementAt(1).Transactions.Add(trxConfirmed2);

            var transactionCount = firstAccount.GetCombinedAddresses().SelectMany(a => a.Transactions).Count();
            Assert.Equal(3, transactionCount);

            // Act.
            var result = walletManager.RemoveTransactionsByIds("wallet1", new[]
            {
                trxConfirmed1.Id, // Shouldn't be removed.
                unconfirmedTransactionId, // A transaction + a spending transaction should be removed.
                trxConfirmed2.Id, // Shouldn't be removed.
                confirmedSpendingDetails.TransactionId, // Shouldn't be removed.
            });

            // Assert.
            var remainingTrxs = firstAccount.GetCombinedAddresses().SelectMany(a => a.Transactions).ToList();
            Assert.Equal(2, remainingTrxs.Count);
            Assert.Single(result);
            Assert.Contains((unconfirmedTransactionId, trxUnconfirmed1.CreationTime), result);
            Assert.DoesNotContain(trxUnconfirmed1, remainingTrxs);
            Assert.Null(trxConfirmed2.SpendingDetails);
        }


        private (Mnemonic mnemonic, Wallet wallet) CreateWalletOnDiskAndDeleteWallet(WalletManager walletManager, string password, string passphrase, string walletName, ConcurrentChain chain)
        {
            // create the wallet
            var mnemonic = walletManager.CreateWallet(password, walletName, passphrase);
            var wallet = walletManager.GetWalletByName(walletName);

            walletManager.DeleteWallet(walletName);

            return (mnemonic, wallet);
        }
    }

    public class WalletFixture : IDisposable
    {
        private readonly Dictionary<(string, string), Wallet> walletsGenerated;
        private Network network;

        public WalletFixture()
        {
            this.walletsGenerated = new Dictionary<(string, string), Wallet>();
            this.network = Network.BRhodiumRegTest;
        }

        public void Dispose()
        {
        }

        /// <summary>
        /// Creates a new wallet.
        /// </summary>
        /// <remarks>
        /// If it's the first time this wallet is created within this class, it is added to a collection for use by other tests.
        /// If the same parameters have already been used to create a wallet, the wallet will be retrieved from the internal collection and a copy of this wallet will be returned.
        /// </remarks>
        /// <param name="name">The name.</param>
        /// <param name="password">The password.</param>
        /// <returns></returns>
        public Wallet GenerateBlankWallet(string name, string password)
        {
            if (this.walletsGenerated.TryGetValue((name, password), out Wallet existingWallet))
            {
                string serializedExistingWallet = JsonConvert.SerializeObject(existingWallet, Formatting.None);
                return JsonConvert.DeserializeObject<Wallet>(serializedExistingWallet);
            }

            Wallet newWallet = WalletTestsHelpers.GenerateBlankWallet(name, password, this.network);
            this.walletsGenerated.Add((name, password), newWallet);

            string serializedNewWallet = JsonConvert.SerializeObject(newWallet, Formatting.None);
            return JsonConvert.DeserializeObject<Wallet>(serializedNewWallet);
        }
    }
}
