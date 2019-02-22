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
        private CoinType coinType = CoinType.BRhodium;

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

            Wallet WalletResult = repo.GetWalletByName(walletTarget.Name);

            Assert.NotNull(WalletResult);
            Assert.Equal(WalletResult.EncryptedSeed, walletTarget.EncryptedSeed);
            Assert.Equal(WalletResult.Name, walletTarget.Name);
            Assert.Equal(WalletResult.Id, walletTarget.Id);
        }
        
        [Fact]
        public void GetWalletByWalletName_ReturnsAllAddressesInIndexOrder()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var repo = new WalletRepository(dataFolder.WalletPath, coinType);
            Wallet walletTarget = createWallet(dataFolder, repo);
            var accountTarget = walletTarget.GetAccountByCoinType("account 0", coinType);


            Wallet WalletResult = repo.GetWalletByName(walletTarget.Name);
            var accountResult = WalletResult.GetAccountByCoinType("account 0", coinType);

            HdAddress[] resultAddressArray = accountResult.ExternalAddresses.ToArray<HdAddress>();
            HdAddress[] targetAddressArray = accountTarget.ExternalAddresses.ToArray<HdAddress>();
            
            for (int i = 0; i < resultAddressArray.Length; i++)
            {
                var result = resultAddressArray[i];
                var target = targetAddressArray[i];                
                Assert.Equal(target.Index, result.Index);
                Assert.Equal(target.Index, i);
            }
        }

        [Fact]
        public void GetWalletByWalletName_ReturnsAllAddressesInWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var repo = new WalletRepository(dataFolder.WalletPath, coinType);
            Wallet walletTarget = createWallet(dataFolder, repo);
            var accountTarget = walletTarget.GetAccountByCoinType("account 0", coinType);


            Wallet WalletResult = repo.GetWalletByName(walletTarget.Name);
            var accountResult = WalletResult.GetAccountByCoinType("account 0", coinType);
            int mathces=0;
            foreach (var item in accountResult.ExternalAddresses)
            {
                if (accountTarget.ExternalAddresses.Select(a => a.Address == item.Address) != null) {
                    mathces++;
                }
            }
            Assert.Equal(accountResult.ExternalAddresses.Count(), mathces);

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

            Wallet WalletResult = repo.GetWalletByAddress(account.ExternalAddresses.FirstOrDefault().Address);

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

            Wallet WalletResult = repo.GetWalletByAddress("WrongAddress");
            Assert.Null(WalletResult);
        }

        [Fact]
        public void GetWallet_ReturnsWalletAccounts_WithLastSynchedBlockHeightAndHash()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var repo = new WalletRepository(dataFolder.WalletPath, coinType);
            Wallet walletTarget = createWallet(dataFolder, repo);

            var chain = new ConcurrentChain(Network.BRhodiumMain);
            var nonce = RandomUtils.GetUInt32();
            var block = new Block();
            block.AddTransaction(new Transaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Genesis.HashBlock;
            block.Header.Nonce = nonce;
            block.Header.BlockTime = DateTimeOffset.Now;
            chain.SetTip(block.Header);

            repo.SaveLastSyncedBlock(walletTarget.Name, chain.Tip);
            Wallet WalletResult = repo.GetWalletByName(walletTarget.Name);

            Assert.Equal(chain.Tip.Height, WalletResult.AccountsRoot.FirstOrDefault().LastBlockSyncedHeight);
            Assert.Equal(chain.Tip.HashBlock, WalletResult.AccountsRoot.FirstOrDefault().LastBlockSyncedHash);
        }

        [Fact]
        public void SaveLastSyncedBlock_PersistsLastSynchedBlockHeightAndHash()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var repo = new WalletRepository(dataFolder.WalletPath, coinType);
            Wallet walletTarget = createWallet(dataFolder, repo);

            var chain = new ConcurrentChain(Network.BRhodiumMain);
            var nonce = RandomUtils.GetUInt32();
            var block = new Block();
            block.AddTransaction(new Transaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Genesis.HashBlock;
            block.Header.Nonce = nonce;
            block.Header.BlockTime = DateTimeOffset.Now;
            chain.SetTip(block.Header);

            repo.SaveLastSyncedBlock(walletTarget.Name, chain.Tip);
            var result = repo.GetLastSyncedBlock();

            Assert.Equal(chain.Tip.Height, result.Height);
            Assert.Equal(chain.Tip.HashBlock, result.BlockHash);
        }
        [Fact]
        public void GetFirstWalletBlockLocator_ReturnsListBlockHashes()
        {
            List<string> testWallets = new List<string>();
            DataFolder dataFolder = CreateDataFolder(this);
            var repo = new WalletRepository(dataFolder.WalletPath, coinType);
            Wallet walletTarget = createWallet(dataFolder, repo, Guid.NewGuid().ToString());
          

            var result = repo.GetFirstWalletBlockLocator();
            foreach (var item in walletTarget.BlockLocator)
            {
                Assert.Equal(true, result.Contains(item));
            }
        }


        [Fact]
        public void GetAllWalletNames_ReturnsListOfAllWalletNames()
        {
            List<string> testWallets = new List<string>();
            DataFolder dataFolder = CreateDataFolder(this);
            var repo = new WalletRepository(dataFolder.WalletPath, coinType);
            Wallet walletTarget = createWallet(dataFolder, repo, Guid.NewGuid().ToString());
            Wallet walletTarget1 = createWallet(dataFolder, repo, Guid.NewGuid().ToString());
            Wallet walletTarget2 = createWallet(dataFolder, repo, Guid.NewGuid().ToString());
            testWallets.Add(walletTarget.Name);
            testWallets.Add(walletTarget1.Name);
            testWallets.Add(walletTarget2.Name);

            var result = repo.GetAllWalletNames();
            foreach (var item in testWallets)
            {
                Assert.Equal(true, result.Contains(item));
            }
        }

        [Fact]
        public void GetEarliestWalletHeight_LastBlockHeight()
        {
            List<string> testWallets = new List<string>();
            DataFolder dataFolder = CreateDataFolder(this);
            var repo = new WalletRepository(dataFolder.WalletPath, coinType);
            Wallet walletTarget = createWallet(dataFolder, repo, Guid.NewGuid().ToString());
            Wallet walletTarget1 = createWallet(dataFolder, repo, Guid.NewGuid().ToString());
            Wallet walletTarget2 = createWallet(dataFolder, repo, Guid.NewGuid().ToString());
            testWallets.Add(walletTarget.Name);
            testWallets.Add(walletTarget1.Name);
            testWallets.Add(walletTarget2.Name);

            var result = repo.GetEarliestWalletHeight();
            int? h = walletTarget.AccountsRoot.FirstOrDefault<AccountRoot>().LastBlockSyncedHeight;
            Assert.Equal(h,result);
        }
        [Fact]
        public void GetOldestWalletCreationTime_LastBlockHeight()
        {
            List<string> testWallets = new List<string>();
            DataFolder dataFolder = CreateDataFolder(this);
            var repo = new WalletRepository(dataFolder.WalletPath, coinType);
            Wallet walletTarget = createWallet(dataFolder, repo, Guid.NewGuid().ToString());
            Wallet walletTarget1 = createWallet(dataFolder, repo, Guid.NewGuid().ToString());
            Wallet walletTarget2 = createWallet(dataFolder, repo, Guid.NewGuid().ToString());
            testWallets.Add(walletTarget.Name);
            testWallets.Add(walletTarget1.Name);
            testWallets.Add(walletTarget2.Name);

            var result = repo.GetOldestWalletCreationTime();

            Assert.Equal(walletTarget.CreationTime, result);
        }
        [Fact]
        public void GetLastUpdatedWalletName_ReturnsWalletName()
        {
            List<string> testWallets = new List<string>();
            DataFolder dataFolder = CreateDataFolder(this);
            var repo = new WalletRepository(dataFolder.WalletPath, coinType);
            Wallet walletTarget = createWallet(dataFolder, repo, Guid.NewGuid().ToString());
            Wallet walletTarget1 = createWallet(dataFolder, repo, Guid.NewGuid().ToString());
            Wallet walletTarget2 = createWallet(dataFolder, repo, Guid.NewGuid().ToString());
            testWallets.Add(walletTarget.Name);
            testWallets.Add(walletTarget1.Name);
            testWallets.Add(walletTarget2.Name);

            var result = repo.GetLastUpdatedWalletName();

            Assert.Equal(walletTarget2.Name, result);
        }
        
        private Wallet createWallet(DataFolder dataFolder, WalletRepository walletRepository,string walletName = "mywallet") {
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

           return walletManager.CreateAndReturnWallet("test", walletName);
        }
    }
}
