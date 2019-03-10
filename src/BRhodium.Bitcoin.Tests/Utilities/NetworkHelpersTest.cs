using System;
using NBitcoin;
using BRhodium.Node.Utilities;
using Xunit;

namespace BRhodium.Node.Tests.Utilities
{
    public class NetworkHelpersTest
    {
        [Fact]
        public void GetMainNetworkReturnsNetworkMain()
        {
            Network main = Network.Main;
            Network network = NetworkHelpers.GetNetwork("BRhodiumMain");
            Assert.Equal(Network.Main, network);
        }

        [Fact]
        public void GetTestNetworkReturnsNetworkTest()
        {
            Network test = Network.TestNet;
            Network network = NetworkHelpers.GetNetwork("BRhodiumTest");
            Assert.Equal(Network.TestNet, network);
        }

        [Fact]
        public void WrongNetworkThrowsArgumentException()
        {
            var exception = Record.Exception(() => NetworkHelpers.GetNetwork("myNetwork"));
            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
        }
    }
}
