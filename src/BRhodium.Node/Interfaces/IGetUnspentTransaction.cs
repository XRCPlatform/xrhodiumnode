using System.Threading.Tasks;
using NBitcoin;
using BRhodium.Node.Utilities;

namespace BRhodium.Node.Interfaces
{
    /// <summary>
    /// An interface used to retrieve unspent transactions
    /// </summary>
    public interface IGetUnspentTransaction
    {
        /// <summary>
        /// Returns the unspent outputs for a specific transaction
        /// </summary>
        /// <param name="trxid">Hash of the transaction to query.</param>
        /// <returns>Unspent Outputs</returns>
        Task<UnspentOutputs> GetUnspentTransactionAsync(uint256 trxid);
    }
}
