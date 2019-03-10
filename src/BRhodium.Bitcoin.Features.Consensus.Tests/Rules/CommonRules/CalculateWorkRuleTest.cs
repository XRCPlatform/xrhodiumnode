using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using BRhodium.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace BRhodium.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class CalculateWorkRuleTest : TestConsensusRulesUnitTestBase
    {
        public CalculateWorkRuleTest()
        {
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkBlock_DoNotCheckPow_SetsNextWorkRequiredAsync()
        {
            this.network = Network.RegTest;
            this.concurrentChain = MineChainWithHeight(2, this.network);
            this.consensusRules = this.InitializeConsensusRules();

            this.ruleContext.BlockValidationContext.ChainedHeader = this.concurrentChain.Tip;
            this.ruleContext.BlockValidationContext.Block = TestRulesContextFactory.MineBlock(this.network, this.concurrentChain);
            this.ruleContext.CheckPow = false;
            this.ruleContext.Consensus = this.network.Consensus;

            await this.consensusRules.RegisterRule<CalculateWorkRule>().RunAsync(this.ruleContext);

            Assert.Equal(0.000000000465, this.ruleContext.NextWorkRequired.Difficulty);
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkBlock_CheckPow_InValidPow_ThrowsHighHashConsensusErrorExceptionAsync()
        {
            this.network = Network.RegTest;
            this.concurrentChain = MineChainWithHeight(2, this.network);
            this.ruleContext.BlockValidationContext = new BlockValidationContext()
            {
                Block = new Block(new BlockHeader())
                {
                    Transactions = new List<Transaction>()
                        {
                            new NBitcoin.Transaction()
                        }
                },
                ChainedHeader = this.concurrentChain.GetBlock(2)
            };
            this.ruleContext.CheckPow = true;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CalculateWorkRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.HighHash, exception.ConsensusError);
        }
    }
}
