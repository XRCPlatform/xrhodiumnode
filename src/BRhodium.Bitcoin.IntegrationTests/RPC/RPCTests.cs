using System;
using System.Net;
using NBitcoin;
using NBitcoin.RPC;
using BRhodium.Node.Connection;
using BRhodium.Bitcoin.Features.Wallet;
using BRhodium.Bitcoin.Features.Wallet.Interfaces;
using BRhodium.Node.IntegrationTests.EnvironmentMockUpHelpers;
using Xunit;
using System.Linq;

namespace BRhodium.Node.IntegrationTests.RPC
{
    /// <summary>
    /// BRhodium test fixture for RPC tests.
    /// </summary>
    public class RpcTestFixtureBRhodium : RpcTestFixtureBase
    {
        /// <inheritdoc />
        protected override void InitializeFixture()
        {
            this.Builder = NodeBuilder.Create();
            this.Node = this.Builder.CreateBRhodiumPowNode();
            this.Builder.StartAll();
            this.RpcClient = this.Node.CreateRPCClient();
            this.NetworkPeerClient = this.Node.CreateNetworkPeerClient();
            this.NetworkPeerClient.VersionHandshakeAsync().GetAwaiter().GetResult();

            var walletManager = this.Node.FullNode.WalletManager();
            walletManager.CreateWallet("test", "wallet1");
            this.TestWallet = walletManager.GetWalletByName("wallet1");
            var hdAddress = this.TestWallet.AccountsRoot.FirstOrDefault().Accounts.FirstOrDefault().ExternalAddresses.FirstOrDefault();

            var key = this.TestWallet.GetExtendedPrivateKeyForAddress("test", hdAddress).PrivateKey;
            this.Node.SetDummyMinerSecret(new BitcoinSecret(key, this.Node.FullNode.Network));
        }
    }

    public class RpcTests : IClassFixture<RpcTestFixtureBRhodium>
    {
        private readonly RpcTestFixtureBRhodium rpcTestFixture;

        public RpcTests(RpcTestFixtureBRhodium RpcTestFixture)
        {
            this.rpcTestFixture = RpcTestFixture;
        }

        /// <summary>
        /// Tests whether the RPC method "addnode" adds a network peer to the connection manager.
        /// </summary>
        [Fact]
        public void CanAddNodeToConnectionManager()
        {
            var connectionManager = this.rpcTestFixture.Node.FullNode.NodeService<IConnectionManager>();
            Assert.Empty(connectionManager.ConnectionSettings.AddNode);

            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);
            this.rpcTestFixture.RpcClient.AddNode(endpoint);

            Assert.Single(connectionManager.ConnectionSettings.AddNode);
        }

        [Fact]
        public void CheckRPCFailures()
        {
            var hash = this.rpcTestFixture.RpcClient.GetBestBlockHash();

            try
            {
                this.rpcTestFixture.RpcClient.SendCommand("lol");
                Assert.True(false, "should throw");
            }
            catch (RPCException ex)
            {
                Assert.Equal(RPCErrorCode.RPC_METHOD_NOT_FOUND, ex.RPCCode);
            }
            Assert.Equal(hash, Network.RegTest.GetGenesis().GetHash());
            var oldClient = this.rpcTestFixture.RpcClient;
            var client = new RPCClient("abc:def", this.rpcTestFixture.RpcClient.Address, this.rpcTestFixture.RpcClient.Network);
            try
            {
                client.GetBestBlockHash();
            }
            catch (Exception ex)
            {
                Assert.Contains("401", ex.Message);
            }
            client = oldClient;
            Assert.Throws<WebException>(() => client.SendCommand("addnode", "regreg", "addr"));//bad request
        }

        /// <summary>
        /// Tests RPC get genesis block hash.
        /// </summary>
        [Fact]
        public void CanGetGenesisBlockHashFromRPC()
        {
            RPCResponse response = this.rpcTestFixture.RpcClient.SendCommand(RPCOperations.getblockhash, 0);

            string actualGenesis = (string)response.Result;
            Assert.Equal(Network.RegTest.GetGenesis().GetHash().ToString(), actualGenesis);
        }

        /// <summary>
        /// Tests RPC getbestblockhash.
        /// </summary>
        [Fact]
        public void CanGetGetBestBlockHashFromRPC()
        {
            uint256 expected = this.rpcTestFixture.Node.FullNode.Chain.Tip.Header.GetHash();

            uint256 response = this.rpcTestFixture.RpcClient.GetBestBlockHash();

            Assert.Equal(expected, response);
        }

        /// <summary>
        /// Tests RPC getblockheader.
        /// </summary>
        [Fact]
        public void CanGetBlockHeaderFromRPC()
        {
            uint256 hash = this.rpcTestFixture.RpcClient.GetBlockHash(0);
            BlockHeader expectedHeader = this.rpcTestFixture.Node.FullNode.Chain?.GetBlock(hash)?.Header;
            BlockHeader actualHeader = this.rpcTestFixture.RpcClient.GetBlockHeader(0);

            // Assert block header fields match.
            Assert.Equal(expectedHeader.Version, actualHeader.Version);
            Assert.Equal(expectedHeader.HashPrevBlock, actualHeader.HashPrevBlock);
            Assert.Equal(expectedHeader.HashMerkleRoot, actualHeader.HashMerkleRoot);
            Assert.Equal(expectedHeader.Time, actualHeader.Time);
            Assert.Equal(expectedHeader.Bits, actualHeader.Bits);
            Assert.Equal(expectedHeader.Nonce, actualHeader.Nonce);

            // Assert header hash matches genesis hash.
            Assert.Equal(Network.RegTest.GenesisHash, actualHeader.GetHash());
        }

        /// <summary>
        /// Tests whether the RPC method "getpeersinfo" can be called and returns a non-empty result.
        /// </summary>
        [Fact]
        public void CanGetPeersInfo()
        {
            PeerInfo[] peers = this.rpcTestFixture.RpcClient.GetPeersInfo();
            Assert.NotEmpty(peers);
        }

        /// <summary>
        /// Tests whether the RPC method "getpeersinfo" can be called and returns a string result suitable for console output.
        /// We are also testing whether all arguments can be passed as strings.
        /// </summary>
        [Fact]
        public void CanGetPeersInfoByStringArgs()
        {
            var resp = this.rpcTestFixture.RpcClient.SendCommand("getpeerinfo").ResultString;
            Assert.StartsWith("[" + Environment.NewLine + "  {" + Environment.NewLine + "    \"id\": 0," + Environment.NewLine + "    \"addr\": \"[", resp);
        }

        /// <summary>
        /// Tests whether the RPC method "getblockhash" can be called and returns the expected string result suitable for console output.
        /// We are also testing whether all arguments can be passed as strings.
        /// </summary>
        [Fact]
        public void CanGetBlockHashByStringArgs()
        {
            var resp = this.rpcTestFixture.RpcClient.SendCommand("getblockhash", "0").ResultString;
            Assert.Equal("a485961c1554fdcd947bac07be3f1991b41ee842552007bd0a39c55e1310b872", resp);
        }

        /// <summary>
        /// Tests whether the RPC method "generate" can be called and returns a string result suitable for console output.
        /// We are also testing whether all arguments can be passed as strings.
        /// </summary>
        [Fact(Skip ="Skiping as works inconsitenly.")]
        public void CanGenerateByStringArgs()
        {
            var hdAddress = this.rpcTestFixture.TestWallet.AccountsRoot.FirstOrDefault().Accounts.FirstOrDefault().ExternalAddresses.FirstOrDefault();
            string resp = this.rpcTestFixture.RpcClient.SendCommand("generate",  "1", hdAddress.Address.ToString()).ResultString;
            Assert.StartsWith("[" + Environment.NewLine + "  \"", resp);
        }
    }
}