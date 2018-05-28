using System;
using System.Threading.Tasks;
using NBitcoin;
using BRhodium.Bitcoin.Features.Wallet.Broadcasting;

namespace BRhodium.Bitcoin.Features.Wallet.Interfaces
{
    public interface IBroadcasterManager
    {
        Task BroadcastTransactionAsync(Transaction transaction);

        event EventHandler<TransactionBroadcastEntry> TransactionStateChanged;

        TransactionBroadcastEntry GetTransaction(uint256 transactionHash);

        void AddOrUpdate(Transaction transaction, State state, string ErrorMessage = "");
    }
}
