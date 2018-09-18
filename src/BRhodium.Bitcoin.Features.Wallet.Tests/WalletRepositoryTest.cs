using System;
using System.Collections.Generic;
using System.Text;
using BRhodium.Bitcoin.Features.Wallet.Interfaces;
using BRhodium.Node.Configuration;
using BRhodium.Node.Tests.Common.Logging;
using BRhodium.Node.Utilities;
using Moq;
using NBitcoin;
using Xunit;
using System.Linq;

namespace BRhodium.Bitcoin.Features.Wallet.Tests
{
    public class WalletRepositoryTest : LogsTestBase, IClassFixture<WalletFixture>
    {
        private readonly WalletFixture walletFixture;
        //private readonly string walletPath;
        private CoinType coinType = CoinType.BRhodium;
        //private DataFolder dataFolder;
        public WalletRepositoryTest(WalletFixture walletFixture)
        {
            this.walletFixture = walletFixture;
            
        }

        [Fact]
        public void GetWalletByWalletName_FindsAndReturnsRightWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var repo = new WalletRepository(dataFolder.WalletPath, coinType);
            Wallet walletTarget = createWallet(dataFolder, repo);

            Wallet WalletResult = repo.GetWallet(walletTarget.Name);

            //assert it's same wallet
            Assert.NotNull(WalletResult);
            Assert.Equal(WalletResult.EncryptedSeed, walletTarget.EncryptedSeed);
            Assert.Equal(WalletResult.Name, walletTarget.Name);
            Assert.Equal(WalletResult.Id, walletTarget.Id);
        }

        [Fact]
        public void GetWalletByWalletName_ReturnsAllAddressesInIndexOrderWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var repo = new WalletRepository(dataFolder.WalletPath, coinType);
            Wallet walletTarget = createWallet(dataFolder, repo);
            var accountTarget = walletTarget.GetAccountByCoinType("account 0", coinType);


            Wallet WalletResult = repo.GetWallet(walletTarget.Name);
            var accountResult = WalletResult.GetAccountByCoinType("account 0", coinType);
            int mathces=0;
            foreach (var item in accountResult.ExternalAddresses)
            {
                if (accountTarget.ExternalAddresses.Select(a => a.Address == item.Address) != null) {
                    mathces++;
                }
            }
            Assert.Equal(accountResult.ExternalAddresses.Count(), mathces);
            //assert it's same wallet
            Assert.NotNull(WalletResult);
            Assert.Equal(WalletResult.EncryptedSeed, walletTarget.EncryptedSeed);
            Assert.Equal(WalletResult.Name, walletTarget.Name);
            Assert.Equal(WalletResult.Id, walletTarget.Id);
        }

        [Fact]
        public void GetWalletByAddress_FindsAndReturnsRightWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var repo = new WalletRepository(dataFolder.WalletPath, coinType);
            Wallet walletTarget = createWallet(dataFolder, repo);

            var account = walletTarget.GetAccountByCoinType("account 0", coinType);
            //get by address
            Wallet WalletResult = repo.GetWalletByAddress(account.ExternalAddresses.FirstOrDefault().Address);
            //assert it's same wallet
            Assert.NotNull(WalletResult);
            Assert.Equal(WalletResult.EncryptedSeed, walletTarget.EncryptedSeed);
            Assert.Equal(WalletResult.Name, walletTarget.Name);
            Assert.Equal(WalletResult.Id, walletTarget.Id);
        }

        [Fact]
        public void GetWalletByAddress_WithWrongAddress_ReturnsNullWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var repo = new WalletRepository(dataFolder.WalletPath, coinType);
            Wallet walletTarget = createWallet(dataFolder, repo);
            //get by address
            Wallet WalletResult = repo.GetWalletByAddress("WrongAddress");
            //assert it's same wallet
            Assert.Null(WalletResult);
        }


        private Wallet createWallet(DataFolder dataFolder, WalletRepository walletRepository) {
            //DataFolder dataFolder = CreateDataFolder(this);

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
                 dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default, null, walletRepository);

           return walletManager.CreateAndReturnWallet("test", "mywallet");
        }
    }
}
