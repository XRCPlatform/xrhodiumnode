using System;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;

namespace NBitcoin
{
    /// <summary>
    /// The consensus factory for creating POW protocol types.
    /// </summary>
    public class PowConsensusFactory : ConsensusFactory
    {
        /// <inheritdoc />
        public override Block CreateBlock()
        {
            return new PowBlock(this.CreateBlockHeader());
        }

        /// <inheritdoc />
        public override BlockHeader CreateBlockHeader()
        {
            return new PowBlockHeader();
        }

        /// <inheritdoc />
        public override Transaction CreateTransaction()
        {
            return new PowTransaction();
        }
    }

    /// <summary>
    /// A POW block header, this will create a work hash based on the X13 hash algos.
    /// </summary>
    public class PowBlockHeader : BlockHeader
    {
        /// <summary>Current header version.</summary>
        public override int CurrentVersion => 45;

        public override uint256 GetHash()
        {
            return this.GetHash(null);
        }

        /// <inheritdoc />
        public uint256 GetHash(Network network = null)
        {
            uint256 hash = null;
            uint256[] innerHashes = this.hashes;

            if (innerHashes != null)
                hash = innerHashes[0];

            if (hash != null)
                return hash;

            hash = Hashes.Hash256(this.ToBytes(ProtocolVersion.BTR_PROTOCOL_VERSION, network));

            innerHashes = this.hashes;
            if (innerHashes != null)
            {
                innerHashes[0] = hash;
            }

            return hash;
        }

        /// <summary>
        /// Generate a has based on the X13 algorithms.
        /// </summary>
        /// <returns></returns>
        public override uint256 GetPoWHash(int height, int powLimit2Height)
        {
            if (height > powLimit2Height)
            {
                return new HashX13LibMultihash().Hash(this.ToBytes());
            }
            else
            {
                return HashX13.Instance.Hash(this.ToBytes());
            }
        }
    }

    /// <summary>
    /// A POW block that contains the additional block signature serialization.
    /// </summary>
    public class PowBlock : Block
    {
        public new const uint MaxBlockSize = 4 * 1000 * 1000;

        /// <summary>
        /// A block signature - signed by one of the coin base txout[N]'s owner.
        /// </summary>
        private BlockSignature blockSignature = new BlockSignature();

        public PowBlock(BlockHeader blockHeader) : base(blockHeader)
        {
        }

        /// <summary>
        /// The additional serialization of the block.
        /// </summary>
        public override void ReadWrite(BitcoinStream stream)
        {
            base.ReadWrite(stream);
        }
    }

    /// <summary>
    /// A Proof Of Work transaction.
    /// </summary>
    /// <remarks>
    /// </remarks>
    public class PowTransaction : Transaction
    {
        public PowTransaction() : base()
        {
        }

        public PowTransaction(string hex, ProtocolVersion version = ProtocolVersion.BTR_PROTOCOL_VERSION) : this()
        {
            this.FromBytes(Encoders.Hex.DecodeData(hex), version);
        }

        public PowTransaction(byte[] bytes) : this()
        {
            this.FromBytes(bytes);
        }
    }
}