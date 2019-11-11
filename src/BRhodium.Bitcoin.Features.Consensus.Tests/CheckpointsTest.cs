using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Xunit;

namespace BRhodium.Bitcoin.Features.Consensus.Tests
{
    public class CheckPointsTest
    {
        public CheckPointsTest()
        {
        }

        [Fact]
        public void GetLastCheckPointHeight_WithoutConsensusSettings_ReturnsZero()
        {
            var checkpoints = new Checkpoints();

            var result = checkpoints.GetLastCheckpointHeight();

            Assert.Equal(0, result);
        }

        [Fact]
        public void GetLastCheckPointHeight_SettingsDisabledCheckpoints_DoesNotLoadCheckpoints()
        {
            var checkpoints = new Checkpoints(Network.Main, new ConsensusSettings() { UseCheckpoints = false });

            var result = checkpoints.GetLastCheckpointHeight();

            Assert.Equal(0, result);
        }

        [Fact]
        public void GetLastCheckPointHeight_BRhodiumMainnet_ReturnsLastCheckPointHeight()
        {
            var checkpoints = new Checkpoints(Network.BRhodiumMain, new ConsensusSettings() { UseCheckpoints = true });

            var result = checkpoints.GetLastCheckpointHeight();

            Assert.Equal(43034, result);
        }

        [Fact]
        public void GetLastCheckPointHeight_BRhodiumTestnet_ReturnsLastCheckPointHeight()
        {
            var checkpoints = new Checkpoints(Network.BRhodiumTest, new ConsensusSettings() { UseCheckpoints = true });

            var result = checkpoints.GetLastCheckpointHeight();

            Assert.Equal(0, result);
        }

        [Fact]
        public void GetLastCheckPointHeight_BRhodiumRegTestNet_DoesNotLoadCheckpoints()
        {
            var checkpoints = new Checkpoints(Network.BRhodiumRegTest, new ConsensusSettings() { UseCheckpoints = true });

            var result = checkpoints.GetLastCheckpointHeight();

            Assert.Equal(0, result);
        }

        [Fact]
        public void GetLastCheckPointHeight_CheckpointsEnabledAfterLoad_RetrievesCheckpointsCorrectly()
        {
            var consensusSettings = new ConsensusSettings() { UseCheckpoints = false };
            var checkpoints = new Checkpoints(Network.Main, consensusSettings);

            var result = checkpoints.GetLastCheckpointHeight();
            Assert.Equal(0, result);

            consensusSettings.UseCheckpoints = true;

            result = checkpoints.GetLastCheckpointHeight();
            Assert.Equal(43034, result);
        }

        [Fact]
        public void GetCheckPoint_WithoutConsensusSettings_ReturnsNull()
        {
            var checkpoints = new Checkpoints();

            var result = checkpoints.GetCheckpoint(11111);

            Assert.Null(result);
        }

        [Fact]
        public void GetCheckPoint_CheckpointExists_PoWChain_ReturnsCheckpoint()
        {
            var checkpoints = new Checkpoints(Network.Main, new ConsensusSettings() { UseCheckpoints = true });

            var result = checkpoints.GetCheckpoint(117);

            Assert.Equal(new uint256("0xbf3082be3b2da88187ebeb902548b41dbff3bcac6687352e0c47d902acd28e62"), result.Hash);
        }

        [Fact]
        public void GetCheckPoint_CheckpointDoesNotExist_ReturnsNull()
        {
            var checkpoints = new Checkpoints(Network.Main, new ConsensusSettings() { UseCheckpoints = true });

            var result = checkpoints.GetCheckpoint(11112);

            Assert.Null(result);
        }

        [Fact]
        public void GetCheckPoint_CheckpointsEnabledAfterLoad_RetrievesCheckpointsCorrectly()
        {
            var consensusSettings = new ConsensusSettings() { UseCheckpoints = false };
            var checkpoints = new Checkpoints(Network.Main, consensusSettings);

            var result = checkpoints.GetCheckpoint(11112);
            Assert.Null(result);

            consensusSettings.UseCheckpoints = true;

            result = checkpoints.GetCheckpoint(117);
            Assert.Equal(new uint256("0xbf3082be3b2da88187ebeb902548b41dbff3bcac6687352e0c47d902acd28e62"), result.Hash);
        }

        [Fact]
        public void CheckHardened_CheckpointsEnabledAfterLoad_RetrievesCheckpointsCorrectly()
        {
            var consensusSettings = new ConsensusSettings() { UseCheckpoints = false };
            var checkpoints = new Checkpoints(Network.Main, consensusSettings);

            var result = checkpoints.CheckHardened(117, new uint256("0x0000000059e244f73d78e8fd29ba2fd2ed618bd6fa2ee92559f542fdb26e7c1e"));
            Assert.True(result);

            consensusSettings.UseCheckpoints = true;

            result = checkpoints.CheckHardened(117, new uint256("0x0000000059e244f73d78e8fd29ba2fd2ed618bd6fa2ee92559f542fdb26e7c1e")); // invalid hash
            Assert.False(result);
        }

        [Fact]
        public void CheckHardened_CheckpointExistsWithHashAtHeight_ReturnsTrue()
        {
            var checkpoints = new Checkpoints(Network.Main, new ConsensusSettings() { UseCheckpoints = true });

            var result = checkpoints.CheckHardened(11111, new uint256("0x0000000069e244f73d78e8fd29ba2fd2ed618bd6fa2ee92559f542fdb26e7c1d"));

            Assert.True(result);
        }

        [Fact]
        public void CheckHardened_CheckpointExistsWithDifferentHashAtHeight_ReturnsTrue()
        {
            var checkpoints = new Checkpoints(Network.Main, new ConsensusSettings() { UseCheckpoints = true });

            var result = checkpoints.CheckHardened(117, new uint256("0x0000000069e244f73d78e8fd29ba2fd2ed618bd6fa2ee92559f542fdb26e7c1d"));

            Assert.False(result);
        }

        [Fact]
        public void CheckHardened_CheckpointDoesNotExistAtHeight_ReturnsTrue()
        {
            var checkpoints = new Checkpoints(Network.Main, new ConsensusSettings() { UseCheckpoints = true });

            var result = checkpoints.CheckHardened(11112, new uint256("0x7d61c139a471821caa6b7635a4636e90afcfe5e195040aecbc1ad7d24924db1e"));

            Assert.True(result);
        }

        [Fact]
        public void CheckHardened_WithoutConsensusSettings_ReturnsTrue()
        {
            var checkpoints = new Checkpoints();

            var result = checkpoints.CheckHardened(11111, new uint256("0x0000000069e244f73d78e8fd29ba2fd2ed618bd6fa2ee92559f542fdb26e7c1d"));

            Assert.True(result);
        }

        private void VerifyCheckpoints(Checkpoints checkpoints, Dictionary<int, CheckpointInfo> checkpointValues)
        {
            foreach (var checkpoint in checkpointValues)
            {
                var result = checkpoints.GetCheckpoint(checkpoint.Key);

                Assert.Equal(checkpoint.Value.Hash, result.Hash);
            }
        }
    }
}
