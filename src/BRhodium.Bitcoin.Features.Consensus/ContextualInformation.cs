using System;
using NBitcoin;
using BRhodium.Node.Base.Deployments;
using BRhodium.Node.Utilities;

namespace BRhodium.Bitcoin.Features.Consensus
{
    public class ContextBlockInformation
    {
        public BlockHeader Header { get; set; }

        public int Height { get; set; }

        public DateTimeOffset MedianTimePast { get; set; }

        public ContextBlockInformation()
        {
        }

        public ContextBlockInformation(ChainedHeader bestBlock, NBitcoin.Consensus consensus)
        {
            Guard.NotNull(bestBlock, nameof(bestBlock));

            this.Header = bestBlock.Header;
            this.Height = bestBlock.Height;
            this.MedianTimePast = bestBlock.GetMedianTimePast();
        }
    }

    /// <summary>
    /// Context that contains variety of information regarding blocks validation and execution.
    /// </summary>
    public class RuleContext
    {
        public NBitcoin.Consensus Consensus { get; set; }

        public DateTimeOffset Time { get; set; }

        public ContextBlockInformation BestBlock { get; set; }

        public Target NextWorkRequired { get; set; }

        public BlockValidationContext BlockValidationContext { get; set; }

        public DeploymentFlags Flags { get; set; }

        public UnspentOutputSet Set { get; set; }

        public bool CheckMerkleRoot { get; set; }

        public bool CheckPow { get; set; }

        /// <summary>Whether to skip block validation for this block due to either a checkpoint or assumevalid hash set.</summary>
        public bool SkipValidation { get; set; }

        /// <summary>The current tip of the chain that has been validated.</summary>
        public ChainedHeader ConsensusTip { get; set; }

        public RuleContext()
        {
        }

        public RuleContext(BlockValidationContext blockValidationContext, NBitcoin.Consensus consensus, ChainedHeader consensusTip)
        {
            Guard.NotNull(blockValidationContext, nameof(blockValidationContext));
            Guard.NotNull(consensus, nameof(consensus));

            this.BlockValidationContext = blockValidationContext;
            this.Consensus = consensus;
            this.ConsensusTip = consensusTip;

            // TODO: adding flags to determine the flow of logic is not ideal
            // a re-factor is in debate on moving to a consensus rules engine
            // this will remove the need for flags as validation will only use
            // the required rules (i.e if the check pow rule will be omitted form the flow)
            this.CheckPow = true;
            this.CheckMerkleRoot = true;
        }

        public void SetBestBlock(DateTimeOffset now)
        {
            this.BestBlock = new ContextBlockInformation(this.BlockValidationContext.ChainedHeader.Previous, this.Consensus);
            this.Time = now;
        }
    }
}
