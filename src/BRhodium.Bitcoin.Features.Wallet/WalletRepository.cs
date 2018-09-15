using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using BRhodium.Node.Utilities;
using DBreeze;
using DBreeze.DataTypes;
using DBreeze.Objects;
using DBreeze.Utils;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace BRhodium.Bitcoin.Features.Wallet
{
    internal class WalletRepository
    {
        private readonly string walletPath;
        private readonly CoinType coinType;
        /// <summary>Access to DBreeze database.</summary>
        protected readonly DBreezeEngine DBreeze;

        public WalletRepository(string walletPath, CoinType coinType)
        {
            this.coinType = coinType;
            this.walletPath = walletPath;
            this.DBreeze = new DBreezeEngine(walletPath);
            //move to binary serialization/ will require further work
            CustomSerializator.ByteArraySerializator = (object o) => 
            {
                return JsonConvert.SerializeObject(o).To_UTF8Bytes();
            };
            CustomSerializator.ByteArrayDeSerializator = (byte[] bt, Type t) => 
            {
                return JsonConvert.DeserializeObject(bt.ToUTF8String());
            };
        }


        internal void SaveBlockLocator(string walletName, List<uint256> blocks)
        {// stores block list for future use
            //case insensitive keys => transform to lower case 
            throw new NotImplementedException();
        }
        public Task SaveWallet(string walletName, Wallet wallet)
        {
            //case insensitive keys => transform to lower case 
            Guard.NotNull(walletName, nameof(walletName));
            Guard.NotNull(wallet, nameof(wallet));

            Task task = Task.Run(() =>
            {
                using (DBreeze.Transactions.Transaction breezeTransaction = this.DBreeze.GetTransaction())
                {
                    breezeTransaction.SynchronizeTables("Wallet");

                    bool newEntity = false;
                    if (wallet.Id<1)
                    {
                        wallet.Id = breezeTransaction.ObjectGetNewIdentity<long>("Wallet");
                        newEntity = true;
                    }
                    breezeTransaction.ObjectInsert("Wallet", new DBreezeObject<Wallet>
                    {
                        NewEntity = newEntity,
                        Entity = wallet,
                        Indexes = new List<DBreezeIndex>
                            {
                                new DBreezeIndex(1,wallet.Name) { PrimaryIndex = true },
                                new DBreezeIndex(2,wallet.Id) 
                            }
                    }, false);

                    //var addresses = new List<(HdAddress, Wallet)>();

                    //var byteListComparer = new ByteListComparer();
                    //addresses.Sort((pair1, pair2) => byteListComparer.Compare(pair1.Item1.ScriptPubKey.ToBytes(), pair2.Item1.ScriptPubKey.ToBytes()));

                    // Index addresses.
                    foreach (var account in wallet.GetAccountsByCoinType(this.coinType))
                    {
                        foreach (var address in account.ExternalAddresses)
                        {
                            SaveAddress(wallet, breezeTransaction, address);
                        }
                        foreach (var address in account.InternalAddresses)
                        {
                            SaveAddress(wallet, breezeTransaction, address);
                        }
                    }


                    breezeTransaction.Commit();
                }
            });


            return task;
        }
        /// <summary>
        /// Saves address to breeze db making it queryable by address, ScriptPubKey. It relates to account through HD path.
        /// </summary>
        /// <param name="wallet"></param>
        /// <param name="breezeTransaction"></param>
        /// <param name="address"></param>
        private static void SaveAddress(Wallet wallet, DBreeze.Transactions.Transaction breezeTransaction, HdAddress address)
        {
            //breezeTransaction.Insert<byte[], long>("Address", address.ScriptPubKey.ToBytes(), wallet.Id);

            bool newEntity = false;
            if (address.Id < 1)
            {
                address.Id = breezeTransaction.ObjectGetNewIdentity<long>("Address");
                newEntity = true;
            }
            breezeTransaction.ObjectInsert("Address", new DBreezeObject<HdAddress>
            {
                NewEntity = newEntity,
                Entity = address,
                Indexes = new List<DBreezeIndex>
                            {
                                new DBreezeIndex(1,address.ScriptPubKey) { PrimaryIndex = true },
                                new DBreezeIndex(2,address.Address),
                                new DBreezeIndex(3,address.Id),
                                new DBreezeIndex(4,wallet.Id)
                            }
            }, false);
            breezeTransaction.Commit();

        }

        private static byte[] StringToBytes(string stringToConvert)
        {
            return Encoding.UTF8.GetBytes(stringToConvert);
        }

        internal void SaveLastSyncedBlock(string walletName, ChainedHeader chainedHeader)
        {
            //case insensitive keys => transform to lower case 

            // Update the wallets with the last processed block height.
            //foreach (AccountRoot accountRoot in wallet.AccountsRoot.Where(a => a.CoinType == this.coinType))
            //{
            //    accountRoot.LastBlockSyncedHeight = chainedHeader.Height;
            //    accountRoot.LastBlockSyncedHash = chainedHeader.HashBlock;
            //}
            throw new NotImplementedException();
        }

        internal Wallet GetWallet(string name)
        {
            Wallet res = null;
            using (DBreeze.Transactions.Transaction breezeTransaction = this.DBreeze.GetTransaction())
            {
                breezeTransaction.ValuesLazyLoadingIsOn = false;               
                var obj = breezeTransaction.Select<byte[], byte[]>("Wallet", 1.ToIndex(name)).ObjectGet<JObject>();
                if (obj != null)
                {
                    HdAccount hdAccount;
                    res = obj.Entity.ToObject<Wallet>();
                    foreach (var row in breezeTransaction.SelectForwardStartsWith<byte[], byte[]>("Address",4.ToIndex(res.Id)))
                    {
                        var address = row.ObjectGet<HdAddress>();
                        if (address != null) {
                            //TODO:optimise the account retrieval with in memory cache for this loop
                            hdAccount = res.GetAccountByHdPathCoinType(address.Entity.HdPath, this.coinType);
                            if (hdAccount != null) {
                                if (address.Entity.IsChangeAddress())
                                {
                                    hdAccount.InternalAddresses.Add(address.Entity);
                                }
                                else {
                                    hdAccount.ExternalAddresses.Add(address.Entity);
                                }
                            }
                            //find account by hd path and add address
                            //call method to add address to account
                        }
                    }
                }
                

            }
            return res;
        }

        internal IEnumerable<HdAddress> GetAllWalletAddressesByCoinType(string walletName, CoinType coinType)
        {
            throw new NotImplementedException();
        }

        internal IEnumerable<string> GetAllWalletNames()
        {
            throw new NotImplementedException();
        }

        internal ICollection<uint256> GetFirstWalletBlockLocator()
        {
            throw new NotImplementedException();
        }

        internal int? GetEarliestWalletHeight()
        {
            throw new NotImplementedException();
            //return this.Wallets.Min(w => w.AccountsRoot.Single(a => a.CoinType == this.coinType).LastBlockSyncedHeight);
        }

        internal DateTimeOffset GetOldestWalletCreationTime()
        {//         return this.Wallets.Min(w => w.CreationTime);
            throw new NotImplementedException();
        }

        internal Wallet GetWalletByAddress(string address)
        {
            throw new NotImplementedException();
        }

        internal void SaveHdAddress(HdAddress hdAddress)
        {
            throw new NotImplementedException();
        }

        internal void RemoveTransactionFromHdAddress(HdAddress hdAddress, uint256 id)
        {
            throw new NotImplementedException();
        }

        internal void RemoveTransactionSpendingDetailsFromHdAddress(HdAddress hdAddress, uint256 id)
        {
            throw new NotImplementedException();
        }
    }
}