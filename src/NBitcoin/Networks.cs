using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;

namespace NBitcoin
{
    public enum CoinType
    {
        /// <summary>
        /// BRhodium
        /// </summary>
        BRhodium = 10291,

        /// <summary>
        /// Testnet (all coins)
        /// </summary>
        Testnet = 1,

        /// <summary>
        /// RegTest
        /// </summary>
        RegTest = 1
    }

    public partial class Network
    {
        /// <summary> Bitcoin maximal value for the calculated time offset. If the value is over this limit, the time syncing feature will be switched off. </summary>
        public const int BitcoinMaxTimeOffsetSeconds = 70 * 60;

        /// <summary> BRhodium maximal value for the calculated time offset. If the value is over this limit, the time syncing feature will be switched off. </summary>
        public const int BRhodiumMaxTimeOffsetSeconds = 25 * 60;

        /// <summary> Bitcoin default value for the maximum tip age in seconds to consider the node in initial block download (24 hours). </summary>
        public const int BitcoinDefaultMaxTipAgeInSeconds = 24 * 60 * 60;

        /// <summary> BRhodium default value for the maximum tip age in seconds to consider the node in initial block download (2 hours). </summary>
        public const int BRhodiumDefaultMaxTipAgeInSeconds = 2 * 60 * 60;

        /// <summary> The name of the root folder containing the different BRhodium blockchains (BRhodiumMain, BRhodiumTest, BRhodiumRegTest). </summary>
        public const string BRhodiumRootFolderName = "BRhodium";

        /// <summary> The default name used for the BRhodium configuration file. </summary>
        public const string BRhodiumDefaultConfigFilename = "BRhodium.conf";

        /// <summary>
        /// Default name for base network
        /// </summary>
        public const string BRhodiumBaseName = "BRhodiumMain";

        public static Network Main => Network.GetNetwork(BRhodiumBaseName) ?? InitBRhodiumMain();

        public static Network TestNet => Network.GetNetwork("BRhodiumTest") ?? InitBRhodiumTest();

        public static Network RegTest => Network.GetNetwork("BRhodiumRegTest") ?? InitBRhodiumRegTest();

        public static Network BRhodiumMain => Network.GetNetwork(BRhodiumBaseName) ?? InitBRhodiumMain();

        public static Network BRhodiumTest => Network.GetNetwork("BRhodiumTest") ?? InitBRhodiumTest();

        public static Network BRhodiumRegTest => Network.GetNetwork("BRhodiumRegTest") ?? InitBRhodiumRegTest();

        private static Network InitBRhodiumMain()
        {
            var messageStart = new byte[4];
            messageStart[0] = 0x33;
            messageStart[1] = 0x33;
            messageStart[2] = 0x34;
            messageStart[3] = 0x35;
            var magic = BitConverter.ToUInt32(messageStart, 0);

            Network network = new Network
            {
                Name = "BRhodiumMain",
                RootFolderName = BRhodiumRootFolderName,
                DefaultConfigFilename = BRhodiumDefaultConfigFilename,
                Magic = magic,
                DefaultPort = 37270,
                RPCPort = 19660,
                MaxTimeOffsetSeconds = BRhodiumMaxTimeOffsetSeconds,
                MaxTipAge = 604800, //one week
                MinTxFee = 1000,
                FallbackFee = 20000,
                MinRelayTxFee = 1000
            };

            network.Consensus.SubsidyHalvingInterval = 210000;
            network.Consensus.MajorityEnforceBlockUpgrade = 750;
            network.Consensus.MajorityRejectBlockOutdated = 950;
            network.Consensus.MajorityWindow = 1000;
            network.Consensus.BuriedDeployments[BuriedDeployments.BIP34] = 0;
            network.Consensus.BuriedDeployments[BuriedDeployments.BIP65] = 0;
            network.Consensus.BuriedDeployments[BuriedDeployments.BIP66] = 0;
            network.Consensus.BIP34Hash = new uint256("0x000000000000024b89b42a942fe0d9fea3bb44ab7bd1b19115dd6a759c0808b8");
            network.Consensus.PowLimit = new Target(new uint256("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
            network.Consensus.PowLimit2 = new Target(new uint256("0000000000092489000000000000000000000000000000000000000000000000"));
            network.Consensus.PowLimit2Height = 1648;
            network.Consensus.PowDigiShieldX11Height = 136135;

            network.Consensus.PowTargetTimespan = TimeSpan.FromSeconds(14 * 24 * 60 * 60); // two weeks
            network.Consensus.PowTargetSpacing = TimeSpan.FromSeconds(10 * 60);
            network.Consensus.PowAllowMinDifficultyBlocks = false;
            network.Consensus.PowNoRetargeting = false;
            network.Consensus.RuleChangeActivationThreshold = 1916; // 95% of 2016
            network.Consensus.MinerConfirmationWindow = 2016; // nPowTargetTimespan / nPowTargetSpacing
            network.Consensus.CoinType = (int)CoinType.BRhodium;
            network.Consensus.DefaultAssumeValid = null; // turn off assumevalid for regtest.
            network.Consensus.ConsensusFactory = new ConsensusFactory() { Consensus = network.Consensus};

            network.Checkpoints.Add(2015, new CheckpointInfo(new uint256("574605587514315bf8dac135c093a50e5982cb26e47ac78f2a712b9289f5cc7e")));
            network.Checkpoints.Add(10079, new CheckpointInfo(new uint256("a960cf32c570de76b4a2035831608bf884c3b8dad7a6e77d6a40b5dcb7f84f5e")));
            network.Checkpoints.Add(18143, new CheckpointInfo(new uint256("fb2df6739907716b4a9c20d45f7db968481b76d97a4bd279a14d19d4dad2a18a")));
            network.Checkpoints.Add(26207, new CheckpointInfo(new uint256("90034dfe536ef2c692d9fad3fc95ea16d0b3a004cb23677eb0cc6ba51b38fc40")));
            network.Checkpoints.Add(34271, new CheckpointInfo(new uint256("f8e3cf72102112a26a7af75fff195321226023a2e2617723b5c6259d63d419da")));
            network.Checkpoints.Add(42335, new CheckpointInfo(new uint256("8bbeb434aba05f41ed2f4d4091289d7c6cd4f6e6168dfc207361b3b53d885970")));
            network.Checkpoints.Add(50399, new CheckpointInfo(new uint256("07e3d655eb39be8e1297ff1835aa09ebe68ca2a1c31d9b412ac029f9066e75e1")));
            network.Checkpoints.Add(58463, new CheckpointInfo(new uint256("88b714a59faa29037b1cf63eb35bcd243a60768bb2cc21cfb500c77fe67d3369")));
            network.Checkpoints.Add(66527, new CheckpointInfo(new uint256("113d337fe7b6aa8d059a674bc339506fa9f69e0c390e978582253c6dd9dcd5b6")));
            network.Checkpoints.Add(74591, new CheckpointInfo(new uint256("0ef81cb39624d5d0c5b0696aed93d97aac5cf342af569485b28ca1e2afb85afa")));
            network.Checkpoints.Add(82655, new CheckpointInfo(new uint256("1254dc1e830853650c3ca41a7487510a632e85b8e8b31e4a87205edc0b373397")));
            network.Checkpoints.Add(90719, new CheckpointInfo(new uint256("782ac4559002e425cc63fe71bb1cb89e03305cc1270d2846baa451d4d4bf9c43")));
            network.Checkpoints.Add(98783, new CheckpointInfo(new uint256("53505abcda5dff8278113d67949b260ce6a79a01dd6b775e6cfc50619d7d0656")));
            network.Checkpoints.Add(106847, new CheckpointInfo(new uint256("6661f25d3850a2cb95a2dd3c1eb7752a7ab9f780c745a8b8fd5ce9fba5acfdbf")));
            network.Checkpoints.Add(114911, new CheckpointInfo(new uint256("f343f45fdff7bede9db8bb10ab1c00ebcd7c173823ef6e49e493ed86e71d2f27")));
            network.Checkpoints.Add(122975, new CheckpointInfo(new uint256("2181671223c47b11f67512e3bc3040eb562da25d5fcbe33cb53d1862cb7bf0dc")));
            network.Checkpoints.Add(131039, new CheckpointInfo(new uint256("81aa79d04b430fc536592f4b6017fae8506869b84b208df655b3d4fe733f5204")));
            network.Checkpoints.Add(136082, new CheckpointInfo(new uint256("2755d2940a031cd27631ad9529ddc96bbbabb4bd0b34be2aa92f92c070d0d417")));

            var pubKeyMain = "04ffff0f1e01041a52656c6561736520746865204b72616b656e212121205a657573";
            Block genesis = CreateBRhodiumGenesisBlock(network.Consensus.ConsensusFactory, 1512043200, 0, network.Consensus.PowLimit.ToCompact(), 45, network, pubKeyMain);
            network.genesis = genesis;
            network.Consensus.HashGenesisBlock = genesis.GetHash(network);

            network.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (61) };
            network.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (123) };
            network.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (100) };
            network.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_NO_EC] = new byte[] { 0x01, 0x42 };
            network.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_EC] = new byte[] { 0x01, 0x43 };
            network.Base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY] = new byte[] { (0x04), (0x88), (0xB2), (0x1E) };
            network.Base58Prefixes[(int)Base58Type.EXT_SECRET_KEY] = new byte[] { (0x04), (0x88), (0xAD), (0xE4) };
            network.Base58Prefixes[(int)Base58Type.PASSPHRASE_CODE] = new byte[] { 0x2C, 0xE9, 0xB3, 0xE1, 0xFF, 0x39, 0xE2 };
            network.Base58Prefixes[(int)Base58Type.CONFIRMATION_CODE] = new byte[] { 0x64, 0x3B, 0xF6, 0xA8, 0x9A };
            network.Base58Prefixes[(int)Base58Type.STEALTH_ADDRESS] = new byte[] { 0x2a };
            network.Base58Prefixes[(int)Base58Type.ASSET_ID] = new byte[] { 23 };
            network.Base58Prefixes[(int)Base58Type.COLORED_ADDRESS] = new byte[] { 0x13 };

            var encoder = new Bech32Encoder("rh");
            network.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = encoder;
            network.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = encoder;

            network.DNSSeeds.AddRange(new[]
            {
                new DNSSeedData("dns.btrmine.com", "dns.btrmine.com"),
                new DNSSeedData("dns2.btrmine.com", "dns2.btrmine.com"),
            });

            Network.Register(network);

            return network;
        }

        private static Network InitBRhodiumTest()
        {
            var messageStart = new byte[4];
            messageStart[0] = 0x39;
            messageStart[1] = 0x33;
            messageStart[2] = 0x34;
            messageStart[3] = 0x35;
            var magic = BitConverter.ToUInt32(messageStart, 0); // 0xefc0f2cd

            Network network = new Network
            {
                Name = "BRhodiumTest",
                RootFolderName = BRhodiumRootFolderName,
                DefaultConfigFilename = BRhodiumDefaultConfigFilename,
                Magic = magic,
                DefaultPort = 16665,
                RPCPort = 16661,
                MaxTimeOffsetSeconds = BRhodiumMaxTimeOffsetSeconds,
                MaxTipAge = 604800, //one week
                MinTxFee = 10000,
                FallbackFee = 60000,
                MinRelayTxFee = 10000
            };

            network.Consensus.SubsidyHalvingInterval = 210000;
            network.Consensus.MajorityEnforceBlockUpgrade = 750;
            network.Consensus.MajorityRejectBlockOutdated = 950;
            network.Consensus.MajorityWindow = 1000;
            network.Consensus.BuriedDeployments[BuriedDeployments.BIP34] = 0;
            network.Consensus.BuriedDeployments[BuriedDeployments.BIP65] = 0;
            network.Consensus.BuriedDeployments[BuriedDeployments.BIP66] = 0;
            network.Consensus.BIP34Hash = new uint256("0x000000000000024b89b42a942fe0d9fea3bb44ab7bd1b19115dd6a759c0808b8");
            network.Consensus.PowLimit = new Target(uint256.Parse("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
            network.Consensus.PowLimit2 = network.Consensus.PowLimit;
            network.Consensus.PowDigiShieldX11Height = 16290;
            network.Consensus.PowTargetTimespan = TimeSpan.FromSeconds(14 * 24 * 60 * 60); // two weeks
            network.Consensus.PowTargetSpacing = TimeSpan.FromSeconds(10 * 60);
            network.Consensus.PowAllowMinDifficultyBlocks = false;
            network.Consensus.PowNoRetargeting = false;
            network.Consensus.RuleChangeActivationThreshold = 1916; // 95% of 2016
            network.Consensus.MinerConfirmationWindow = 2016; // nPowTargetTimespan / nPowTargetSpacing
            network.Consensus.CoinType = (int)CoinType.Testnet;
            network.Consensus.DefaultAssumeValid = null; // turn off assumevalid for regtest.
            network.Consensus.ConsensusFactory = new ConsensusFactory() { Consensus = network.Consensus };

            var prodTEST = "04ffff0f1e01041a52656c6561736520746865204b72616b656e212121205a657573";
            Block genesis = CreateBRhodiumGenesisBlock(network.Consensus.ConsensusFactory, 1527811200, 0, network.Consensus.PowLimit.ToCompact(), 45, network, prodTEST);
            genesis.Header.Bits = network.Consensus.PowLimit;
            network.genesis = genesis;
            network.Consensus.HashGenesisBlock = genesis.GetHash(network);

            network.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (65) };
            network.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (128) };
            network.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (0xef) };
            network.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_NO_EC] = new byte[] { 0x01, 0x42 };
            network.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_EC] = new byte[] { 0x01, 0x43 };
            network.Base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY] = new byte[] { (0x04), (0x88), (0xB2), (0x1E) };
            network.Base58Prefixes[(int)Base58Type.EXT_SECRET_KEY] = new byte[] { (0x04), (0x88), (0xAD), (0xE4) };
            network.Base58Prefixes[(int)Base58Type.PASSPHRASE_CODE] = new byte[] { 0x2C, 0xE9, 0xB3, 0xE1, 0xFF, 0x39, 0xE2 };
            network.Base58Prefixes[(int)Base58Type.CONFIRMATION_CODE] = new byte[] { 0x64, 0x3B, 0xF6, 0xA8, 0x9A };
            network.Base58Prefixes[(int)Base58Type.STEALTH_ADDRESS] = new byte[] { 0x2a };
            network.Base58Prefixes[(int)Base58Type.ASSET_ID] = new byte[] { 23 };
            network.Base58Prefixes[(int)Base58Type.COLORED_ADDRESS] = new byte[] { 0x13 };

            var encoder = new Bech32Encoder("th");
            network.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = encoder;
            network.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = encoder;

            Network.Register(network);

            return network;
        }

        private static Network InitBRhodiumRegTest()
        {
            var messageStart = new byte[4];
            messageStart[0] = 0x34;
            messageStart[1] = 0x33;
            messageStart[2] = 0x34;
            messageStart[3] = 0x35;
            var magic = BitConverter.ToUInt32(messageStart, 0); // 0xefc0f2cd

            Network network = new Network
            {
                Name = "BRhodiumRegTest",
                RootFolderName = BRhodiumRootFolderName,
                DefaultConfigFilename = BRhodiumDefaultConfigFilename,
                Magic = magic,
                DefaultPort = 16665,
                RPCPort = 16661,
                MaxTimeOffsetSeconds = BRhodiumMaxTimeOffsetSeconds,
                MaxTipAge = BRhodiumDefaultMaxTipAgeInSeconds
            };

            network.Consensus.SubsidyHalvingInterval = 210000;
            network.Consensus.MajorityEnforceBlockUpgrade = 750;
            network.Consensus.MajorityRejectBlockOutdated = 950;
            network.Consensus.MajorityWindow = 1000;
            network.Consensus.BuriedDeployments[BuriedDeployments.BIP34] = 0;
            network.Consensus.BuriedDeployments[BuriedDeployments.BIP65] = 0;
            network.Consensus.BuriedDeployments[BuriedDeployments.BIP66] = 0;
            network.Consensus.BIP34Hash = new uint256("0x000000000000024b89b42a942fe0d9fea3bb44ab7bd1b19115dd6a759c0808b8");
            network.Consensus.PowLimit = new Target(uint256.Parse("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")); //0.00000000046565418188
            network.Consensus.PowLimit2 = network.Consensus.PowLimit;
            network.Consensus.PowDigiShieldX11Height = 0;
            network.Consensus.PowTargetTimespan = TimeSpan.FromSeconds(14 * 24 * 60 * 60); // two weeks
            network.Consensus.PowTargetSpacing = TimeSpan.FromSeconds(10 * 60);
            network.Consensus.PowAllowMinDifficultyBlocks = true;
            network.Consensus.PowNoRetargeting = false;
            network.Consensus.RuleChangeActivationThreshold = 1916; // 95% of 2016
            network.Consensus.MinerConfirmationWindow = 2016; // nPowTargetTimespan / nPowTargetSpacing
            network.Consensus.CoinType = (int)CoinType.RegTest;
            network.Consensus.DefaultAssumeValid = null; // turn off assumevalid for regtest.
            network.Consensus.ConsensusFactory = new ConsensusFactory() { Consensus = network.Consensus };

            Block genesis = CreateBRhodiumGenesisBlock(network.Consensus.ConsensusFactory, 1527811200, 0, network.Consensus.PowLimit.ToCompact(), 45, network);
            genesis.Header.Bits = network.Consensus.PowLimit;
            network.genesis = genesis;
            network.Consensus.HashGenesisBlock = genesis.GetHash(network);

            network.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (65) };
            network.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (128) };
            network.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (100) };
            network.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_NO_EC] = new byte[] { 0x01, 0x42 };
            network.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_EC] = new byte[] { 0x01, 0x43 };
            network.Base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY] = new byte[] { (0x04), (0x88), (0xB2), (0x1E) };
            network.Base58Prefixes[(int)Base58Type.EXT_SECRET_KEY] = new byte[] { (0x04), (0x88), (0xAD), (0xE4) };
            network.Base58Prefixes[(int)Base58Type.PASSPHRASE_CODE] = new byte[] { 0x2C, 0xE9, 0xB3, 0xE1, 0xFF, 0x39, 0xE2 };
            network.Base58Prefixes[(int)Base58Type.CONFIRMATION_CODE] = new byte[] { 0x64, 0x3B, 0xF6, 0xA8, 0x9A };
            network.Base58Prefixes[(int)Base58Type.STEALTH_ADDRESS] = new byte[] { 0x2a };
            network.Base58Prefixes[(int)Base58Type.ASSET_ID] = new byte[] { 23 };
            network.Base58Prefixes[(int)Base58Type.COLORED_ADDRESS] = new byte[] { 0x13 };

            var encoder = new Bech32Encoder("th");
            network.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = encoder;
            network.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = encoder;

            Network.Register(network);

            return network;
        }

        private static Block CreateBRhodiumGenesisBlock(ConsensusFactory consensusFactory, uint nTime, uint nNonce, uint nBits, int nVersion, Network network, string hexNew = null)
        {
            string message = "Release the Kraken!!! Zeus";
            return CreateBRhodiumGenesisBlock(consensusFactory, message, nTime, nNonce, nBits, nVersion, network, hexNew);
        }

        private static Block CreateBRhodiumGenesisBlock(ConsensusFactory consensusFactory, string message, uint nTime, uint nNonce, uint nBits, int nVersion, Network network, string pubKeyHexNew = null)
        {
            //nTime = 1512043200 => Thursday, November 30, 2017 12:00:00 PM (born XRC)
            //nTime = 1527811200 => Friday, Jun 1, 2017 12:00:00 PM (born TestXRC)
            //nBits = 0x1d00ffff (it is exactly 0x1b = 27 bytes long) => 0x00ffff0000000000000000000000000000000000000000000000000000 => 1
            //nNonce = XTimes to trying to find a genesis block
            var pubKeyHex = "2103d1b6cd5f956ccedf5877c89843a438bfb800468133fb2e73946e1452461a9b1aac";
            if (pubKeyHexNew != null) pubKeyHex = pubKeyHexNew;

            Transaction txNew = consensusFactory.CreateTransaction();
            txNew.Version = 2;
            txNew.Time = nTime;
            txNew.AddInput(new TxIn()
            {
                ScriptSig = new Script(Op.GetPushOp(nBits), new Op()
                {
                    Code = (OpcodeType)0x1,
                    PushData = new[] { (byte)4 }
                }, Op.GetPushOp(Encoders.ASCII.DecodeData(message)))
            });
            txNew.AddOutput(new TxOut()
            {
                Value = Money.Zero,
                ScriptPubKey = Script.FromBytesUnsafe(Encoders.Hex.DecodeData(pubKeyHex))
            });

            Block genesis = consensusFactory.CreateBlock();
            genesis.Header.BlockTime = Utils.UnixTimeToDateTime(nTime);
            genesis.Header.Bits = nBits;
            genesis.Header.Nonce = nNonce;
            genesis.Header.Version = nVersion;
            genesis.Transactions.Add(txNew);
            genesis.Header.HashPrevBlock = uint256.Zero;
            genesis.UpdateMerkleRoot();
            return genesis;
        }
    }
}
