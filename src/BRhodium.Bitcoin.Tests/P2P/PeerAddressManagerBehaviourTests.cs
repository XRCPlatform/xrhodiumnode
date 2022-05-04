using System;
using System.IO;
using System.Net;
using Moq;
using NBitcoin;
using BRhodium.Node.Configuration.Logging;
using BRhodium.Node.P2P;
using BRhodium.Node.P2P.Peer;
using BRhodium.Node.P2P.Protocol;
using BRhodium.Node.P2P.Protocol.Payloads;
using BRhodium.Node.Tests.Common.Logging;
using BRhodium.Node.Utilities;
using Xunit;

namespace BRhodium.Node.Tests.P2P
{
    public sealed class PeerAddressManagerBehaviourTests : LogsTestBase
    {
        private readonly ExtendedLoggerFactory extendedLoggerFactory;
        private readonly Network network;
        private readonly INetworkPeerFactory networkPeerFactory;

        public PeerAddressManagerBehaviourTests()
        {
            this.extendedLoggerFactory = new ExtendedLoggerFactory();

            this.network = Network.Main;
            this.networkPeerFactory = new NetworkPeerFactory(this.network, DateTimeProvider.Default, this.extendedLoggerFactory, new PayloadProvider().DiscoverPayloads(), new SelfEndpointTracker());
        }

        [Fact]
        public void PeerAddressManagerBehaviour_ReceivedPing_UpdateLastSeen()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);

            var peerFolder = CreateDataFolder(this);
            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.LoggerFactory.Object, new SelfEndpointTracker());
            addressManager.AddPeer(endpoint, IPAddress.Loopback);

            var networkPeer = new Mock<INetworkPeer>();
            networkPeer.SetupGet(n => n.PeerEndPoint).Returns(endpoint);
            networkPeer.SetupGet(n => n.State).Returns(NetworkPeerState.HandShaked);

            var messageReceived = new AsyncExecutionEvent<INetworkPeer, IncomingMessage>();
            networkPeer.SetupGet(n => n.MessageReceived).Returns(messageReceived);

            var stateChanged = new AsyncExecutionEvent<INetworkPeer, NetworkPeerState>();
            networkPeer.SetupGet(n => n.StateChanged).Returns(stateChanged);

            var behaviour = new PeerAddressManagerBehaviour(DateTimeProvider.Default, addressManager) { Mode = PeerAddressManagerBehaviourMode.AdvertiseDiscover };
            behaviour.Attach(networkPeer.Object);

            var incomingMessage = new IncomingMessage();
            incomingMessage.Message = new Message(new PayloadProvider().DiscoverPayloads())
            {
                Magic = this.network.Magic,
                Payload = new PingPayload(),
            };

            //Trigger the event handler
            networkPeer.Object.MessageReceived.ExecuteCallbacksAsync(networkPeer.Object, incomingMessage).GetAwaiter().GetResult();

            var peer = addressManager.FindPeer(endpoint);
            Assert.Equal(DateTimeProvider.Default.GetUtcNow().Date, peer.LastSeen.Value.Date);
        }

        [Fact]
        public void PeerAddressManagerBehaviour_ReceivedPong_UpdateLastSeen()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);

            var peerFolder = CreateDataFolder(this);
            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.LoggerFactory.Object, new SelfEndpointTracker());
            addressManager.AddPeer(endpoint, IPAddress.Loopback);

            var networkPeer = new Mock<INetworkPeer>();
            networkPeer.SetupGet(n => n.PeerEndPoint).Returns(endpoint);
            networkPeer.SetupGet(n => n.State).Returns(NetworkPeerState.HandShaked);

            var messageReceived = new AsyncExecutionEvent<INetworkPeer, IncomingMessage>();
            networkPeer.SetupGet(n => n.MessageReceived).Returns(messageReceived);

            var stateChanged = new AsyncExecutionEvent<INetworkPeer, NetworkPeerState>();
            networkPeer.SetupGet(n => n.StateChanged).Returns(stateChanged);

            var behaviour = new PeerAddressManagerBehaviour(DateTimeProvider.Default, addressManager) { Mode = PeerAddressManagerBehaviourMode.AdvertiseDiscover };
            behaviour.Attach(networkPeer.Object);

            var incomingMessage = new IncomingMessage();
            incomingMessage.Message = new Message(new PayloadProvider().DiscoverPayloads())
            {
                Magic = this.network.Magic,
                Payload = new PingPayload(),
            };

            //Trigger the event handler
            networkPeer.Object.MessageReceived.ExecuteCallbacksAsync(networkPeer.Object, incomingMessage).GetAwaiter().GetResult();

            var peer = addressManager.FindPeer(endpoint);
            Assert.Equal(DateTimeProvider.Default.GetUtcNow().Date, peer.LastSeen.Value.Date);
        }
    }
}
