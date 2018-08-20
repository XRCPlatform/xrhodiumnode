using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using BRhodium.Node.Mining;

namespace BRhodium.Bitcoin.Features.Miner
{
    /// <inheritdoc/>
    public sealed class BlockProvider : IBlockProvider
    {
        /// <summary>Defines how proof of work blocks are built.</summary>
        private readonly PowBlockDefinition powBlockDefinition;

        /// <param name="definitions">A list of block definitions that the builder can utilize.</param>
        public BlockProvider(IEnumerable<BlockDefinition> definitions)
        {
            this.powBlockDefinition = definitions.OfType<PowBlockDefinition>().FirstOrDefault();
        }

        /// <inheritdoc/>
        public BlockTemplate BuildPowBlock(ChainedHeader chainTip, Script script)
        {
            return this.powBlockDefinition.Build(chainTip, script);
        }
    }
}