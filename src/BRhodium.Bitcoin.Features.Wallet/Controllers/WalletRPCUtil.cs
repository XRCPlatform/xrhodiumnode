using System;
using System.Linq;
using NBitcoin;

namespace BRhodium.Bitcoin.Features.Wallet.Controllers
{
    public class WalletRPCUtil
    {
        /// <summary>
        /// Wallet used if no wallet is specified.
        /// </summary>
        public const string DEFAULT_WALLET = "default.wallet";
        /// <summary>
        /// The default account used if nothing is specified. Uses the first derivation
        /// path
        /// </summary>
        public const string DEFAULT_ACCOUNT = "account 0";

        /// <summary>
        /// Find an account from a wallet name.
        ///
        /// The deprecated account field in various Bitcoin wallet APIs use an account as a reference to
        /// the name of some list of keys stored in a wallet file. We do not use classic wallets, but instead
        /// HD wallets. So, when an account name is not available, we refer to the default wallet. If an
        /// HD account is not specified, it defaults to "account 0".
        /// </summary>
        /// <param name="walletManager"></param>
        /// <param name="network"></param>
        /// <param name="walletName"></param>
        /// <returns></returns>
        public static HdAccount GetAccountFromWalletForDeprecatedRpcs(
            WalletManager walletManager,
            Network network,
            string walletName)
        {
            if (string.IsNullOrEmpty(walletName)) walletName = DEFAULT_WALLET;
            var wallet = walletManager.GetWalletByName(walletName);
            if (wallet != null)
            {
                return wallet.GetAccountByCoinType(DEFAULT_ACCOUNT, (CoinType)network.Consensus.CoinType);
            }
            else
                return null;
        }
    }
}
