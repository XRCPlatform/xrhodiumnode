using NBitcoin;

namespace BRhodium.Bitcoin.Features.Wallet.Interfaces
{
    public interface IWalletFeePolicy
    {
        void Start();

        void Stop();

        Money GetRequiredFee(int txBytes);

        Money GetMinimumFee(int txBytes, int confirmTarget);

        Money GetMinimumFee(int txBytes, int confirmTarget, Money targetFee);

        FeeRate GetFeeRate(int confirmTarget);

        void SetPayTxFee(Money feePerK);

        FeeRate GetPayTxFee();
    }
}
