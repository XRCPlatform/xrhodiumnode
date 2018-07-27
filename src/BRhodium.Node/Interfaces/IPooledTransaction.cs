using System.Threading.Tasks;
using NBitcoin;

namespace BRhodium.Node.Interfaces
{
    public interface IPooledTransaction
    {
        Task<Transaction> GetTransaction(uint256 trxid);
    }
}
