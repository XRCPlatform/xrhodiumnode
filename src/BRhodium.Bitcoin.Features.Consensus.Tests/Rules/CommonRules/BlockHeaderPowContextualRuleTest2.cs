using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using BRhodium.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace BRhodium.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class BlockHeaderPowContextualRuleTest2 : TestConsensusRulesUnitTestBase
    {
        public BlockHeaderPowContextualRuleTest2()
        {
            this.network = Network.TestNet; //important for bips
            this.concurrentChain = GenerateChainWithHeight(5, this.network);
            this.consensusRules = this.InitializeConsensusRules();
        }

        [Fact]
        public async Task RunAsync_RequiredProofOfWorkNotMetLower_ThrowsBadDiffBitsConsensusErrorAsync()
        {
            this.ruleContext.BestBlock = new ContextBlockInformation()
            {
                Height = 5
            };
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.BlockValidationContext.Block = new Block();
            this.ruleContext.BlockValidationContext.Block.Header.Bits = new Target(0x1f111114);

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadDiffBits, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_RequiredProofOfWorkNotMetHigher_ThrowsBadDiffBitsConsensusErrorAsync()
        {
            this.ruleContext.BestBlock = new ContextBlockInformation()
            {
                Height = 5
            };
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.BlockValidationContext.Block = new Block();
            this.ruleContext.BlockValidationContext.Block.Header.Bits = new Target(0x1f111116);

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadDiffBits, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_TimeTooOldLower_ThrowsTimeTooOldConsensusErrorAsync()
        {
            this.ruleContext.BestBlock = new ContextBlockInformation()
            {
                MedianTimePast = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0)),
                Height = 5
            };
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.BlockValidationContext.Block = new Block();
            this.ruleContext.BlockValidationContext.Block.Header.Bits = new Target(0x1f111115);
            this.ruleContext.BlockValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 0, 9));

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.TimeTooOld, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_TimeTooOldEqual_ThrowsTimeTooOldConsensusErrorAsync()
        {
            this.ruleContext.BestBlock = new ContextBlockInformation()
            {
                MedianTimePast = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0)),
                Height = 5
            };
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.BlockValidationContext.Block = new Block();
            this.ruleContext.BlockValidationContext.Block.Header.Bits = new Target(0x1f111115);
            this.ruleContext.BlockValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.TimeTooOld, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_TimeTooNew_ThrowsTimeTooNewConsensusErrorAsync()
        {
            this.ruleContext.Time = new DateTime(2016, 12, 31, 10, 0, 0);
            this.ruleContext.BestBlock = new ContextBlockInformation()
            {
                MedianTimePast = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0)),
                Height = 5
            };
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.BlockValidationContext.Block = new Block();
            this.ruleContext.BlockValidationContext.Block.Header.Bits = new Target(0x1f111115);
            this.ruleContext.BlockValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.TimeTooNew, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_GoodVersionHeightBelowBip34_DoesNotThrowExceptionAsync()
        {
            this.ruleContext.Time = new DateTime(2017, 1, 1, 0, 0, 0);
            this.ruleContext.BestBlock = new ContextBlockInformation()
            {
                MedianTimePast = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0)),
                // set height lower than bip34
                Height = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP34] - 2
            };
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.BlockValidationContext.Block = new Block();
            this.ruleContext.BlockValidationContext.Block.Header.Bits = new Target(0x1f111115);
            this.ruleContext.BlockValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            this.ruleContext.BlockValidationContext.Block.Header.Version = 1;

            await this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_GoodVersionHeightBelowBip66_DoesNotThrowExceptionAsync()
        {
            this.ruleContext.Time = new DateTime(2017, 1, 1, 0, 0, 0);
            this.ruleContext.BestBlock = new ContextBlockInformation()
            {
                MedianTimePast = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0)),
                // set height lower than bip66
                Height = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66] - 2
            };
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.BlockValidationContext.Block = new Block();
            this.ruleContext.BlockValidationContext.Block.Header.Bits = new Target(0x1f111115);
            this.ruleContext.BlockValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            this.ruleContext.BlockValidationContext.Block.Header.Version = 2;

            await this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_GoodVersionHeightBelowBip65_DoesNotThrowExceptionAsync()
        {
            this.ruleContext.Time = new DateTime(2017, 1, 1, 0, 0, 0);
            this.ruleContext.BestBlock = new ContextBlockInformation()
            {
                MedianTimePast = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0)),
                // set height lower than bip365
                Height = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP65] - 2
            };
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.BlockValidationContext.Block = new Block();
            this.ruleContext.BlockValidationContext.Block.Header.Bits = new Target(0x1f111115);
            this.ruleContext.BlockValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            this.ruleContext.BlockValidationContext.Block.Header.Version = 3;

            await this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_GoodVersionAboveBIPS_DoesNotThrowExceptionAsync()
        {
            this.ruleContext.Time = new DateTime(2017, 1, 1, 0, 0, 0);
            this.ruleContext.BestBlock = new ContextBlockInformation()
            {
                MedianTimePast = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0)),
                // set height higher than bip65
                Height = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP65] + 30
            };
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.BlockValidationContext.Block = new Block();
            this.ruleContext.BlockValidationContext.Block.Header.Bits = new Target(0x1f111115);
            this.ruleContext.BlockValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            this.ruleContext.BlockValidationContext.Block.Header.Version = 4;

            await this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>().RunAsync(this.ruleContext);
        }
    }
}