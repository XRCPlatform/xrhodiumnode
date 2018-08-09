using System;
using System.Collections.Generic;
using System.Security;
using Moq;
using NBitcoin;
using Newtonsoft.Json;
using BRhodium.Node.Base;
using BRhodium.Node.Configuration;
using BRhodium.Bitcoin.Features.Miner.Interfaces;
using BRhodium.Bitcoin.Features.Miner.Models;
using BRhodium.Bitcoin.Features.Miner.Tests.Controllers;
using BRhodium.Bitcoin.Features.RPC;
using BRhodium.Bitcoin.Features.Wallet;
using BRhodium.Bitcoin.Features.Wallet.Interfaces;
using BRhodium.Node.Tests.Common.Logging;
using BRhodium.Node.Tests.Wallet.Common;
using Xunit;
using BRhodium.Node;

namespace BRhodium.Bitcoin.Features.Miner.Tests
{
    public class MiningRPCControllerTest : LogsTestBase, IClassFixture<MiningRPCControllerFixture>
    {
        private MiningRPCController controller;
        private Mock<IFullNode> fullNode;
        private Mock<IPosMinting> posMinting;
        private Mock<IWalletManager> walletManager;
        private Mock<ITimeSyncBehaviorState> timeSyncBehaviorState;
        private MiningRPCControllerFixture fixture;
        private Mock<IPowMining> powMining;

        public MiningRPCControllerTest(MiningRPCControllerFixture fixture)
        {
            this.fixture = fixture;
            this.powMining = new Mock<IPowMining>();
            this.fullNode = new Mock<IFullNode>();
            this.posMinting = new Mock<IPosMinting>();
            this.walletManager = new Mock<IWalletManager>();
            this.timeSyncBehaviorState = new Mock<ITimeSyncBehaviorState>();
            this.fullNode.Setup(f => f.NodeService<IWalletManager>(false))
                .Returns(this.walletManager.Object);

            //this.controller = new MiningRPCController(this.powMining.Object, this.fullNode.Object, this.LoggerFactory.Object, this.walletManager.Object, this.posMinting.Object);
        }


        [Fact]
        public void Generate_BlockCountLowerThanZero_ThrowsRPCServerException()
        {
            Assert.Throws<RPCServerException>(() =>
            {
                this.controller.Generate(-1);
            });
        }

        [Fact]
        public void Generate_NoWalletLoaded_ThrowsRPCServerException()
        {
            Assert.Throws<RPCServerException>(() =>
            {
                this.walletManager.Setup(w => w.GetWalletsNames())
                    .Returns(new List<string>());

                this.controller.Generate(10);
            });
        }

        [Fact]
        public void Generate_WalletWithoutAccount_ThrowsRPCServerException()
        {
            Assert.Throws<RPCServerException>(() =>
            {
                this.walletManager.Setup(w => w.GetWalletsNames())
                    .Returns(new List<string>() {
                        "myWallet"
                    });

                this.walletManager.Setup(w => w.GetAccounts("myWallet"))
                    .Returns(new List<HdAccount>());

                this.controller.Generate(10);
            });
        }

        [Fact]
        public void Generate_UnusedAddressCanBeFoundOnWallet_GeneratesBlocksUsingAddress_ReturnsBlockHeaderHashes()
        {
            this.walletManager.Setup(w => w.GetWalletsNames())
                   .Returns(new List<string>() {
                        "myWallet"
                   });
            this.walletManager.Setup(w => w.GetAccounts("myWallet"))
                .Returns(new List<HdAccount>() {
                    WalletTestsHelpers.CreateAccount("test")
                });
            var address = WalletTestsHelpers.CreateAddress(false);
            this.walletManager.Setup(w => w.GetUnusedAddress(It.IsAny<WalletAccountReference>()))
                .Returns(address);

            this.powMining.Setup(p => p.GenerateBlocks(It.Is<ReserveScript>(r => r.ReserveFullNodeScript == address.Pubkey), 1, int.MaxValue))
                .Returns(new List<NBitcoin.uint256>() {
                    new NBitcoin.uint256(1255632623)
                });

            var result = this.controller.Generate(1);

            Assert.NotEmpty(result);

            Assert.Equal(new NBitcoin.uint256(1255632623), result[0]);
        }

    }

    public class MiningRPCControllerFixture
    {
        public readonly Wallet.Wallet wallet;

        public MiningRPCControllerFixture()
        {
            this.wallet = WalletTestsHelpers.GenerateBlankWallet("myWallet", "password1");
        }
    }
}
