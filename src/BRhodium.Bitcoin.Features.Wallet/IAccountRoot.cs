using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using NBitcoin;

namespace BRhodium.Bitcoin.Features.Wallet
{
    public interface IAccountRoot : ISerializable
    {
        ICollection<IHdAccount> Accounts { get; set; }
        CoinType CoinType { get; set; }
        uint256 LastBlockSyncedHash { get; set; }
        int? LastBlockSyncedHeight { get; set; }

        HdAccount AddNewAccount(string password, string encryptedSeed, byte[] chainCode, Network network, DateTimeOffset accountCreationTime);
        HdAccount GetAccountByName(string accountName);
        HdAccount GetFirstUnusedAccount();
        void GetObjectData(SerializationInfo info, StreamingContext context);
    }
}