using NBitcoin;

namespace BRhodium.Node.Interfaces
{
    public interface INetworkDifficulty
    {
        Target GetNetworkDifficulty();
    }
}
