using NBitcoin;

namespace BRhodium.Bitcoin.Features.Wallet
{
    /// <summary>
    /// A class that represents the balance of an account.
    /// </summary>
    public class AccountBalance
    {
        /// <summary>
        /// The account for which the balance is calculated.
        /// </summary>
        public IHdAccount Account { get; set; }

        /// <summary>
        /// The balance of confirmed transactions.
        /// </summary>
        public Money AmountConfirmed { get; set; }

        /// <summary>
        /// The balance of unconfirmed transactions.
        /// </summary>
        public Money AmountUnconfirmed { get; set; }

        /// <summary>
        /// The balance of imature coinbase transactions 
        /// </summary>
        public Money AmountImmature { get; set; }
    }
}
