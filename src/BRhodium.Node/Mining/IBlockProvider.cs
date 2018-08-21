using NBitcoin;

namespace BRhodium.Node.Mining
{
    /// <summary>
    /// The block provider class is called <see cref="PowMining"/>
    /// to create a block based on whether or not the node is mining.
    /// <para>
    /// The create block logic is abstracted away from the miner so that
    /// different implementations can be injected via dependency injection.
    /// </para>
    /// </summary>
    public interface IBlockProvider
    {
        /// <summary>Builds a proof of work block.</summary>
        BlockTemplate BuildPowBlock(ChainedHeader chainTip, Script script);
    }
}