using System;
using NBitcoin;
using BRhodium.Node.P2P.Peer;
using BRhodium.Node.P2P.Protocol.Payloads;

namespace BRhodium.Node.P2P.Protocol.Filters
{
    public class NetworkPeerFiltersCollection : ThreadSafeCollection<INetworkPeerFilter>
    {
        public IDisposable Add(Action<IncomingMessage, Action> onReceiving, Action<INetworkPeer, Payload, Action> onSending = null)
        {
            return base.Add(new ActionFilter(onReceiving, onSending));
        }
    }
}
