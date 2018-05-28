using NBitcoin;
using BRhodium.Bitcoin.Builder;
using BRhodium.Bitcoin.Configuration;
using BRhodium.Bitcoin.Features.Consensus;
using BRhodium.Bitcoin.Features.RPC;
using BRhodium.Bitcoin.Tests.Common;
using Xunit;

namespace BRhodium.Bitcoin.IntegrationTests.RPC
{
    public class RPCSettingsTest : TestBase
    {
        public RPCSettingsTest() : base(Network.Main)
        {
        }

        [Fact]
        public void CanSpecifyRPCSettings()
        {
            var dir = CreateTestDir(this);

            NodeSettings nodeSettings = new NodeSettings(args:new string[] { $"-datadir={dir}" });

            var node = new FullNodeBuilder()
                .UseNodeSettings(nodeSettings)
                .UsePowConsensus()
                .AddRPC(x =>
                {
                    x.RpcUser = "abc";
                    x.RpcPassword = "def";
                    x.RPCPort = 91;
                })
                .Build();

            var settings = node.NodeService<RpcSettings>();

            settings.Load(nodeSettings);

            Assert.Equal("abc", settings.RpcUser);
            Assert.Equal("def", settings.RpcPassword);
            Assert.Equal(91, settings.RPCPort);
        }
    }
}
