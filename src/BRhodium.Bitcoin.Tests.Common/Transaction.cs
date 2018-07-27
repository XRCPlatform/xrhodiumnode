using FluentAssertions;
using NBitcoin;

namespace BRhodium.Node.Tests.Common
{
    public class Transactions
    {
        public static Transaction BuildNewTransactionFromExistingTransaction(Transaction inputTransaction, int index = 0)
        {
            var transaction = new Transaction();
            var outPoint = new OutPoint(inputTransaction, index);
            transaction.Inputs.Add(new TxIn(outPoint));
            var outValue = Money.Satoshis(inputTransaction.TotalOut.Satoshi / 4);
            outValue.Should().NotBe(Money.Zero, "just to have an actual out");
            var outScript = (new Key()).ScriptPubKey;
            transaction.Outputs.Add(new TxOut(outValue, outScript));
            return transaction;
        }
    }
}