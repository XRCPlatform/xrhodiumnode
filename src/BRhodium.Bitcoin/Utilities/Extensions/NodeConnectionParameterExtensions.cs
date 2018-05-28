using BRhodium.Bitcoin.P2P;
using BRhodium.Bitcoin.P2P.Peer;

namespace BRhodium.Bitcoin.Utilities.Extensions
{
    public static class NodeConnectionParameterExtensions
    {
        public static PeerAddressManagerBehaviour PeerAddressManagerBehaviour(this NetworkPeerConnectionParameters parameters)
        {
            return parameters.TemplateBehaviors.Find<PeerAddressManagerBehaviour>();
        }
    }
}