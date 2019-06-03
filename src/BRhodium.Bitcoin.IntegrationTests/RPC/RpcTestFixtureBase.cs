using System;
using System.IO;
using NBitcoin.RPC;
using BRhodium.Node.IntegrationTests.EnvironmentMockUpHelpers;
using BRhodium.Node.P2P.Peer;

namespace BRhodium.Node.IntegrationTests.RPC
{
    /// <summary>
    /// Abstract base class for RPC Test Fixtures for both Bitcoin and BRhodium networks.
    /// </summary>
    public abstract class RpcTestFixtureBase : IDisposable
    {
        /// <summary>Node builder for the test fixture.</summary>
        protected NodeBuilder Builder { get; set; }

        /// <summary>The node for the test fixture.</summary>
        public CoreNode Node { get; protected set; }

        /// <summary>The RPC client for the test fixture.</summary>
        public RPCClient RpcClient { get; protected set; }

        /// <summary>The network peer client for the test fixture.</summary>
        public INetworkPeer NetworkPeerClient { get; protected set; }
        //wallet
        public Bitcoin.Features.Wallet.Wallet TestWallet { get; protected set; }
        //wallet
        public Bitcoin.Features.Wallet.WalletManager WalletManager { get; protected set; }
        /// <summary>
        /// Constructs the test fixture by calling initialize which should initialize the properties of the fixture.
        /// </summary>
        public RpcTestFixtureBase()
        {
            this.InitializeFixture();
        }

        /// <summary>
        /// Initializes the test fixtures properties as approriate for the network.
        /// </summary>
        protected abstract void InitializeFixture();

        /// <summary>
        /// Disposes of the test fixtures resources.
        /// Note: do not call this dispose in the class itself xunit will handle it.
        /// </summary>
        public void Dispose()
        {
            this.Builder.Dispose();
            this.NetworkPeerClient.Dispose();
        }
    }
}
