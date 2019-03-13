using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using BRhodium.Node.Configuration;
using BRhodium.Bitcoin.Features.RPC.Controllers;
using BRhodium.Bitcoin.Features.RPC.Models;
using BRhodium.Node.Interfaces;
using Xunit;
using BRhodium.Node.Utilities.JsonContract;
using Microsoft.AspNetCore.Mvc;

namespace BRhodium.Node.IntegrationTests.RPC
{
    public class GetInfoActionTests : BaseRPCControllerTest
    {
        [Fact]
        public void CallWithDependencies()
        {
            string dir = CreateTestDir(this);
            IFullNode fullNode = this.BuildServicedNode(dir);
            FullNodeRPCController controller = fullNode.Services.ServiceProvider.GetService<FullNodeRPCController>();

            Assert.NotNull(fullNode.NodeService<INetworkDifficulty>(true));

            JsonResult jsonResult = (JsonResult)controller.GetInfo();
            ResultModel resultModel  = (ResultModel)jsonResult.Value;
            GetInfoModel info = (GetInfoModel)resultModel.Result;


            NodeSettings nodeSettings = NodeSettings.Default();
            uint expectedProtocolVersion = (uint)nodeSettings.ProtocolVersion;
            var expectedRelayFee = nodeSettings.MinRelayTxFeeRate.FeePerK.ToUnit(NBitcoin.MoneyUnit.XRC);
            Assert.NotNull(info);
            Assert.Equal(0, info.Blocks);
            Assert.NotEqual<uint>(0, info.Version);
            Assert.Equal(expectedProtocolVersion, info.ProtocolVersion);
            Assert.Equal(0, info.TimeOffset);
            Assert.Equal(0, info.Connections);
            Assert.NotNull(info.Proxy);
            Assert.Equal(0, info.Difficulty);
            Assert.False(info.Testnet);
            Assert.Equal(expectedRelayFee, info.RelayFee);
            Assert.Empty(info.Errors);
        }
    }
}
