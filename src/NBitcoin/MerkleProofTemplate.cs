using System.Collections.Generic;

namespace NBitcoin
{
    public class MerkleProofTemplate
    {
        public MerkleProofTemplate(Block block)
        {
            this.Block = block;
            this.Matches = new bool[block.Transactions.Count];
            this.Hashes = new uint256[block.Transactions.Count];
            for (int i = 0; i < block.Transactions.Count; i++)
            {
                this.Hashes[i]= block.Transactions[i].GetHash();
                this.Matches[i] = false;
            }
        }

        public bool[] Matches { get; internal set; }
        public uint256[] Hashes { get; internal set; }
        public Block Block { get; }
    }
}