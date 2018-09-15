using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace BRhodium.Bitcoin.Features.Wallet.Interfaces
{
    /// <summary>
    /// Wallet key pool interface
    /// </summary>
    public interface IWalletKeyPool
    {
        /// <summary>
        /// Clear poll and fill it with new PubKeyAddress.
        /// </summary>
        /// <param name="newSize">The new size.</param>
        /// <param name="network">The network.</param>
        void ReFill(int? newSize = null, Network network = null);

        /// <summary>
        /// Gets the ununsed key.
        /// </summary>
        /// <returns>BitcoinPubKeyAddress object.</returns>
        BitcoinPubKeyAddress GetUnunsedKey();

        /// <summary>
        /// Gets the size of the key pool.
        /// </summary>
        /// <returns>Size of pool.</returns>
        int GetKeyPoolSize();

        /// <summary>
        /// Gets the key pool time stamp.
        /// </summary>
        /// <returns></returns>
        long GetKeyPoolTimeStamp();
    }
}
