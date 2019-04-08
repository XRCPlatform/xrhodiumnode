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
using BRhodium.Bitcoin.Features.Miner.Controllers;

namespace BRhodium.Bitcoin.Features.Miner.Tests
{
    public class MiningRPCControllerTest : LogsTestBase, IClassFixture<MiningRPCControllerFixture>
    {
        private MiningRPCController controller;
        private Mock<IFullNode> fullNode;
        private Mock<IWalletManager> walletManager;
        private Mock<ITimeSyncBehaviorState> timeSyncBehaviorState;
        private MiningRPCControllerFixture fixture;
        private Mock<IPowMining> powMining;

        public MiningRPCControllerTest(MiningRPCControllerFixture fixture)
        {
            this.fixture = fixture;
            this.powMining = new Mock<IPowMining>();
            this.fullNode = new Mock<IFullNode>();
            this.walletManager = new Mock<IWalletManager>();
            this.timeSyncBehaviorState = new Mock<ITimeSyncBehaviorState>();
            this.fullNode.Setup(f => f.NodeService<IWalletManager>(false))
                .Returns(this.walletManager.Object);

            //this.controller = new MiningRPCController(this.powMining.Object, this.fullNode.Object, this.LoggerFactory.Object, this.walletManager.Object, this.posMinting.Object);
        }
    }

    public class MiningRPCControllerFixture
    {
        public readonly Wallet.Wallet wallet;

        public MiningRPCControllerFixture()
        {
            this.wallet = WalletTestsHelpers.GenerateBlankWallet("myWallet", "password1", Network.BRhodiumRegTest);
        }
    }
}
