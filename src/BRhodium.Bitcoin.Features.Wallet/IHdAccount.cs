using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using NBitcoin;

namespace BRhodium.Bitcoin.Features.Wallet
{

    public interface IHdAccount: ISerializable
    {
        DateTimeOffset CreationTime { get; set; }
        string ExtendedPubKey { get; set; }
        ICollection<HdAddress> ExternalAddresses { get; set; }
        string HdPath { get; set; }
        int Index { get; set; }
        ICollection<HdAddress> InternalAddresses { get; set; }
        string Name { get; set; }

        IEnumerable<HdAddress> CreateAddresses(Network network, int addressesQuantity, bool isChange = false);
        HdAddress CreateAddresses(Network network, string pubKey);
        IEnumerable<HdAddress> FindAddressesForTransaction(Func<TransactionData, bool> predicate);
        CoinType GetCoinType();
        IEnumerable<HdAddress> GetCombinedAddresses();
        HdAddress GetFirstUnusedChangeAddress();
        HdAddress GetFirstUnusedReceivingAddress();
        HdAddress GetLastUsedAddress(bool isChange);
        void GetObjectData(SerializationInfo info, StreamingContext context);
        (Money ConfirmedAmount, Money UnConfirmedAmount, Money Immature) GetSpendableAmount(ConcurrentChain chain);
        IEnumerable<TransactionData> GetSpendableTransactions();
        IEnumerable<UnspentOutputReference> GetSpendableTransactions(Network network, int currentChainHeight, int confirmations = 0);
        IEnumerable<TransactionData> GetTransactionsById(uint256 id);
        HdAddress ImportBase58Address(Network network, string base58Address);
        HdAddress ImportScriptAddress(Network network, string scriptAddress);
    }
}