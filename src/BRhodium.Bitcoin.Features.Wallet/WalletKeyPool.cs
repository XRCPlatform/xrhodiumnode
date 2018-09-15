using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using BRhodium.Bitcoin.Features.Wallet.Interfaces;
using BRhodium.Node.Configuration;
using BRhodium.Node.Utilities;
using NBitcoin;

namespace BRhodium.Bitcoin.Features.Wallet
{
    /// <summary>
    /// Wallet Key Pool Class
    /// </summary>
    /// <seealso cref="BRhodium.Bitcoin.Features.Wallet.Interfaces.IWalletKeyPool" />
    public class WalletKeyPool : IWalletKeyPool
    {
        /// <summary>
        /// The key pool
        /// </summary>
        private ConcurrentDictionary<BitcoinPubKeyAddress, bool> keyPool;

        /// <summary>
        /// The key pool size
        /// </summary>
        private int keyPoolSize;

        /// <summary>
        /// The key pool date stamp
        /// </summary>
        private DateTime keyPoolDateStamp;

        /// <summary>
        /// Specification of the network the node runs on - regtest/testnet/mainnet.
        /// </summary>
        private readonly Network network;

        /// <summary>
        /// Initializes a new instance of the <see cref="WalletKeyPool"/> class.
        /// </summary>
        /// <param name="network">The network.</param>
        /// <param name="nodeSettings">The node settings.</param>
        public WalletKeyPool(Network network, NodeSettings nodeSettings)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(nodeSettings, nameof(nodeSettings));

            this.keyPool = new ConcurrentDictionary<BitcoinPubKeyAddress, bool>();
            this.network = network;

            var config = nodeSettings.ConfigReader;
            this.keyPoolSize = config.GetOrDefault<int>("keypool", 100);

            this.ReFill();
        }

        /// <inheritdoc />
        public void ReFill(int? newSize = null, Network network = null)
        {
            this.keyPool = new ConcurrentDictionary<BitcoinPubKeyAddress, bool>();
            this.keyPoolDateStamp = DateTime.UtcNow;

            if (newSize.HasValue)
            {
                this.keyPoolSize = newSize.Value;
            }

            for (int i = 0; i < this.keyPoolSize; i++)
            {
                var key = new Key();
                var pubKey = key.PubKey;

                var address = pubKey.GetAddress(network == null ? this.network : network);

                this.keyPool.AddOrReplace(address, false);
            }
        }

        /// <inheritdoc />
        public BitcoinPubKeyAddress GetUnunsedKey()
        {
            foreach (var itemKey in this.keyPool)
            {
                if (itemKey.Value == false)
                {
                    return itemKey.Key;
                }
            }

            return null;
        }

        /// <inheritdoc />
        public int GetKeyPoolSize()
        {
            return this.keyPoolSize;
        }

        /// <inheritdoc />
        public long GetKeyPoolTimeStamp()
        {
            return Utils.DateTimeToUnixTime(this.keyPoolDateStamp);
        }
    }
}
