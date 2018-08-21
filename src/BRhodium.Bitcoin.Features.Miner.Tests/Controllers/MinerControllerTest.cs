using System;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NBitcoin;
using Newtonsoft.Json;
using BRhodium.Node.Base;
using BRhodium.Bitcoin.Features.Miner.Controllers;
using BRhodium.Bitcoin.Features.Miner.Interfaces;
using BRhodium.Bitcoin.Features.Miner.Models;
using BRhodium.Bitcoin.Features.Wallet;
using BRhodium.Bitcoin.Features.Wallet.Interfaces;
using BRhodium.Node.Tests.Common.Logging;
using BRhodium.Node.Tests.Wallet.Common;
using BRhodium.Node.Utilities.JsonErrors;
using Xunit;
using BRhodium.Node;
using BRhodium.Node.Configuration;

namespace BRhodium.Bitcoin.Features.Miner.Tests.Controllers
{
    public class MinerControllerTest : LogsTestBase
    {
        private MinerController controller;
        private Mock<IFullNode> fullNode;
        private Mock<IWalletManager> walletManager;
        private Mock<ITimeSyncBehaviorState> timeSyncBehaviorState;

        public MinerControllerTest()
        {
            this.fullNode = new Mock<IFullNode>();
            this.walletManager = new Mock<IWalletManager>();
            this.timeSyncBehaviorState = new Mock<ITimeSyncBehaviorState>();

            this.controller = new MinerController(this.fullNode.Object, this.LoggerFactory.Object, this.walletManager.Object);
        }
    }
}