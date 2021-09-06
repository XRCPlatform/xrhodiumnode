using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using NBitcoin;

namespace BRhodium.Bitcoin.Features.Wallet
{
    public class AccountRootSurrogate : ISerializationSurrogate
    {
        public void GetObjectData(object obj, SerializationInfo info, StreamingContext context)
        {
            var item = (AccountRoot)obj;
            info.AddValue("coinType", item.CoinType);
            if (item.LastBlockSyncedHeight != null) info.AddValue("lastBlockSyncedHeight", item.LastBlockSyncedHeight);
            if (item.LastBlockSyncedHash != null) info.AddValue("lastBlockSyncedHash", item.LastBlockSyncedHash != null ? item.LastBlockSyncedHash.ToString() : null);

            ICollection<HdAccount> accountsList = new List<HdAccount>();
            foreach (var x in item.Accounts)
            {
                accountsList.Add((HdAccount)x);
            }
            info.AddValue("accountsSurrogate", accountsList, typeof(ICollection<HdAccount>));
            info.AddValue("accounts", item.Accounts);
        }

        public object SetObjectData(object obj, SerializationInfo info, StreamingContext context, ISurrogateSelector selector)
        {
            var item = (AccountRoot)obj;

            foreach (SerializationEntry entry in info)
            {
                switch (entry.Name)
                {
                    case "coinType":
                        item.CoinType = (CoinType)info.GetValue("coinType", typeof(CoinType));
                        break;
                    case "lastBlockSyncedHeight":
                        item.LastBlockSyncedHeight = (int?)info.GetValue("lastBlockSyncedHeight", typeof(int?));
                        break;
                    case "lastBlockSyncedHash":
                        var lastBlockSyncedHash = info.GetString("lastBlockSyncedHash");
                        item.LastBlockSyncedHash = lastBlockSyncedHash != null ? new uint256(lastBlockSyncedHash) : null;
                        break;
                    case "accountsSurrogate":
                        //ICollection<IHdAccount> accounts = new List<IHdAccount>();
                        //foreach (var item in (ICollection<HdAccount>)info.GetValue("accountsSurrogate", typeof(ICollection<HdAccount>)))
                        //{
                        //    accounts.Add(item);
                        //};
                        //item.AccountsSurrogate = (ICollection<HdAccount>)info.GetValue("accountsSurrogate", typeof(ICollection<HdAccount>));
                        break;
                    case "accounts":
                        ICollection<IHdAccount> accountsreal = new List<IHdAccount>();
                        foreach (var x in (ICollection<HdAccount>)info.GetValue("accounts", typeof(ICollection<HdAccount>)))
                        {
                            accountsreal.Add(x);
                        };
                        item.Accounts = accountsreal;
                        break;
                }
            }

            return item;
        }
    }
}
