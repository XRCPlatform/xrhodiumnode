using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace BRhodium.Bitcoin.Features.Consensus.Models
{
    public class BlockModel
    {
        public string Hash { get; set; }

        public Int32 Confirmations { get; set; }

        public Int32 Size { get; set; }

        public Int32 Height { get; set; }

        public Int32 Version { get; set; }

        /// <summary>
        /// Every transaction has a hash associated with it. In a block, all of the transaction hashes in the block are themselves hashed (sometimes several times -- the exact process is complex), and the result is the Merkle root. In other words, the Merkle root is the hash of all the hashes of all the transactions in the block. The Merkle root is included in the block header. With this scheme, it is possible to securely verify that a transaction has been accepted by the network (and get the number of confirmations) by downloading just the tiny block headers and Merkle tree -- downloading the entire block chain is unnecessary.
        /// </summary>
        public string MerkleRoot { get; set; }

        public List<string> Tx { get; set; }

        public Int32 Time { get; set; }

        public UInt32 Nonce { get; set; }

        public string Bits { get; set; }

        public double Difficulty { get; set; }

        public string NextBlockHash { get; set; }
        public BlockModel()
        {
            Tx = new List<string>();
        }

    }
}
