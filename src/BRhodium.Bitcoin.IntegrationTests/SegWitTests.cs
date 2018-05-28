using System;
using NBitcoin;
using NBitcoin.RPC;
using BRhodium.Bitcoin.Base.Deployments;
using BRhodium.Bitcoin.Features.Consensus.Interfaces;
using BRhodium.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using BRhodium.Bitcoin.Utilities.Extensions;
using Xunit;

namespace BRhodium.Bitcoin.IntegrationTests
{
    public class SegWitTests
    {
        [Fact]
        public void TestSegwit_MinedOnCore_ActivatedOn_BRhodiumNode()
        {
            using (NodeBuilder builder = NodeBuilder.Create(version: "0.15.1"))
            {
                CoreNode coreNode = builder.CreateBitcoinCoreNode();

                coreNode.ConfigParameters.AddOrReplace("debug", "1");
                coreNode.ConfigParameters.AddOrReplace("printtoconsole", "0");
                coreNode.Start();

                CoreNode BRhodiumNode = builder.CreateBRhodiumPowNode(start: true);

                RPCClient BRhodiumNodeRpc = BRhodiumNode.CreateRPCClient();
                RPCClient coreRpc = coreNode.CreateRPCClient();

                coreRpc.AddNode(BRhodiumNode.Endpoint, false);
                BRhodiumNodeRpc.AddNode(coreNode.Endpoint, false);

                // core (in version 0.15.1) only mines segwit blocks above a certain height on regtest
                // future versions of core will change that behaviour so this test may need to be changed in the future
                // see issue for more details https://github.com/BRhodiumproject/BRhodiumBitcoinFullNode/issues/1028
                BIP9DeploymentsParameters prevSegwitDeployment = Network.RegTest.Consensus.BIP9Deployments[BIP9Deployments.Segwit];
                Network.RegTest.Consensus.BIP9Deployments[BIP9Deployments.Segwit] = new BIP9DeploymentsParameters(1, 0, DateTime.Now.AddDays(50).ToUnixTimestamp());

                try
                {
                    // generate 450 blocks, block 431 will be segwit activated.
                    coreRpc.Generate(450);

                    TestHelper.WaitLoop(() => BRhodiumNode.CreateRPCClient().GetBestBlockHash() == coreNode.CreateRPCClient().GetBestBlockHash());

                    // segwit activation on Bitcoin regtest.
                    // - On regtest deployment state changes every 144 block, the threshold for activating a rule is 108 blocks.
                    // segwit deployment status should be:
                    // - Defined up to block 142.
                    // - Started at block 143 to block 286 .
                    // - LockedIn 287 (as segwit should already be signaled in blocks).
                    // - Active at block 431.

                    IConsensusLoop consensusLoop = BRhodiumNode.FullNode.NodeService<IConsensusLoop>();
                    ThresholdState[] segwitDefinedState = consensusLoop.NodeDeployments.BIP9.GetStates(BRhodiumNode.FullNode.Chain.GetBlock(142));
                    ThresholdState[] segwitStartedState = consensusLoop.NodeDeployments.BIP9.GetStates(BRhodiumNode.FullNode.Chain.GetBlock(143));
                    ThresholdState[] segwitLockedInState = consensusLoop.NodeDeployments.BIP9.GetStates(BRhodiumNode.FullNode.Chain.GetBlock(287));
                    ThresholdState[] segwitActiveState = consensusLoop.NodeDeployments.BIP9.GetStates(BRhodiumNode.FullNode.Chain.GetBlock(431));

                    // check that segwit is got activated at block 431
                    Assert.Equal(ThresholdState.Defined, segwitDefinedState.GetValue((int)BIP9Deployments.Segwit));
                    Assert.Equal(ThresholdState.Started, segwitStartedState.GetValue((int)BIP9Deployments.Segwit));
                    Assert.Equal(ThresholdState.LockedIn, segwitLockedInState.GetValue((int)BIP9Deployments.Segwit));
                    Assert.Equal(ThresholdState.Active, segwitActiveState.GetValue((int)BIP9Deployments.Segwit));
                }
                finally
                {
                    Network.RegTest.Consensus.BIP9Deployments[BIP9Deployments.Segwit] = prevSegwitDeployment;
                }
            }
        }

        private void TestSegwit_MinedOnBRhodiumNode_ActivatedOn_CoreNode()
        {
            // TODO: mine segwit onh a BRhodium node on the bitcoin network
            // write a tests that mines segwit blocks on the BRhodium node 
            // and signals them to a core not, then segwit will get activated on core
        }
    }
}
