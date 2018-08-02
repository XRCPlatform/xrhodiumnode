using System;
using System.IO;
using System.Linq;
using System.Threading;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.DataEncoders;
using Xunit;

namespace NBitcoin.Tests
{
    public class NetworkTests
    {
        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanGetNetworkFromName()
        {
            Network bitcoinMain = Network.Main;
            Network bitcoinTestnet = Network.TestNet;
            Network bitcoinRegtest = Network.RegTest;
            Network BRhodiumMain = Network.BRhodiumMain;
            Network BRhodiumTestnet = Network.BRhodiumTest;
            Network BRhodiumRegtest = Network.BRhodiumRegTest;

            Assert.Equal(Network.GetNetwork("main"), bitcoinMain);
            Assert.Equal(Network.GetNetwork("mainnet"), bitcoinMain);
            Assert.Equal(Network.GetNetwork("MainNet"), bitcoinMain);
            Assert.Equal(Network.GetNetwork("test"), bitcoinTestnet);
            Assert.Equal(Network.GetNetwork("testnet"), bitcoinTestnet);
            Assert.Equal(Network.GetNetwork("regtest"), bitcoinRegtest);
            Assert.Equal(Network.GetNetwork("reg"), bitcoinRegtest);
            Assert.Equal(Network.GetNetwork("BRhodiummain"), BRhodiumMain);
            Assert.Equal(Network.GetNetwork("BRhodiumMain"), BRhodiumMain);
            Assert.Equal(Network.GetNetwork("BRhodiumTest"), BRhodiumTestnet);
            Assert.Equal(Network.GetNetwork("BRhodiumtest"), BRhodiumTestnet);
            Assert.Equal(Network.GetNetwork("BRhodiumRegTest"), BRhodiumRegtest);
            Assert.Equal(Network.GetNetwork("BRhodiumregtest"), BRhodiumRegtest);
            Assert.Null(Network.GetNetwork("invalid"));
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void RegisterNetworkTwiceFails()
        {
            Network main = Network.Main;
            var error = Assert.Throws<InvalidOperationException>(() => Network.Register(main));
            Assert.Contains("is already registered", error.Message);
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void RegisterNetworkTwiceWithDifferentNamesSucceeds()
        {
            Network main = Network.Main;
            Network main2 = Network.Register(main, "main2");

            Assert.Equal(Network.GetNetwork("main"), Network.GetNetwork("main2"));
        }
        
        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void ReadMagicByteWithFirstByteDuplicated()
        {
            var bytes = Network.Main.MagicBytes.ToList();
            bytes.Insert(0, bytes.First());

            using(var memstrema = new MemoryStream(bytes.ToArray()))
            {
                var found = Network.Main.ReadMagic(memstrema, new CancellationToken());
                Assert.True(found);
            }
        }
    }
}