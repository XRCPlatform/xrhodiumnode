using System;
using System.Collections.Generic;
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
using System.Linq;

namespace BRhodium.Bitcoin.Features.Wallet
{
    public class WalletRepository
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
            //throw new NotImplementedException();
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
                    breezeTransaction.SynchronizeTables("Wallet", "WalletNames", "Address", "AddressToWalletPair");

                    bool newEntity = false;
                    if (wallet.Id < 1)
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
                    if (newEntity)
                    {
                        //used when we find all wallets, to avoid lifting all wallet blobs
                        breezeTransaction.Insert<long, string>("WalletNames", wallet.Id, wallet.Name);
                    }                   

                    // Index addresses.
                    foreach (var account in wallet.GetAccountsByCoinType(this.coinType))
                    {
                        //sort before storing to db
                        var exAddresses =(IList<HdAddress>) account.ExternalAddresses;
                        exAddresses.OrderBy(a=>a.Index);

                        foreach (var address in exAddresses)
                        {
                            SaveAddress(wallet, breezeTransaction, address);
                        }
                        //sort before storing to db
                        var intAddresses = (IList<HdAddress>)account.InternalAddresses;
                        intAddresses.OrderBy(a => a.Index);

                        foreach (var address in intAddresses)
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
        /// Saves address to breeze db making it queryable by address, ScriptPubKey. It relates to account through HD path. Does not commit transaction itself. Caller controlls transaction and must commit.
        /// </summary>
        /// <param name="wallet"></param>
        /// <param name="breezeTransaction"></param>
        /// <param name="address"></param>
        private static void SaveAddress(Wallet wallet, DBreeze.Transactions.Transaction breezeTransaction, HdAddress address)
        {
            bool newEntity = false;
            if (address.Id < 1)
            {
                address.Id = breezeTransaction.ObjectGetNewIdentity<long>("Address");
                newEntity = true;
            }
            int subIndex = address.IsChangeAddress() ? 1:0;
            breezeTransaction.ObjectInsert("Address", new DBreezeObject<HdAddress>
            {
                NewEntity = newEntity,
                Entity = address,
                Indexes = new List<DBreezeIndex>
                    {
                        new DBreezeIndex(1,wallet.Id, subIndex, address.Index) { PrimaryIndex = true },
                        new DBreezeIndex(2,wallet.Id),
                        new DBreezeIndex(3,address.Address),
                        new DBreezeIndex(4,address.Id),                                
                        new DBreezeIndex(5,address.ScriptPubKey.ToBytes())
                    }
            }, false);
            if (newEntity) {
                //used when we find address in transaction to find right wallet and GetWalletByAddress API
                breezeTransaction.Insert<long, long>("AddressToWalletPair", address.Id, wallet.Id);
            }           
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
            //throw new NotImplementedException();
        }

        public Wallet GetWallet(string name)
        {
            Wallet wallet = null;
            using (DBreeze.Transactions.Transaction breezeTransaction = this.DBreeze.GetTransaction())
            {
                breezeTransaction.ValuesLazyLoadingIsOn = false;
                var obj = breezeTransaction.Select<byte[], byte[]>("Wallet", 1.ToIndex(name)).ObjectGet<JObject>();
                if (obj != null)
                {
                    HdAccount hdAccount = null;
                    wallet = obj.Entity.ToObject<Wallet>();
                    foreach (var row in breezeTransaction.SelectForwardStartsWith<byte[], byte[]>("Address", 2.ToIndex(wallet.Id)))
                    {
                        var temp = row.ObjectGet<JObject>();
                        var address = temp.Entity.ToObject<HdAddress>();
                        if (address != null)
                        {   
                            //if not initialized or different than previous find and cache 
                            if (hdAccount == null || !address.HdPath.Contains(hdAccount.HdPath)) {
                                hdAccount = wallet.GetAccountByHdPathCoinType(address.HdPath, this.coinType);
                            }
                            if (hdAccount != null)
                            {
                                if (address.IsChangeAddress())
                                {
                                    hdAccount.InternalAddresses.Add(address);
                                }
                                else
                                {
                                    hdAccount.ExternalAddresses.Add(address);
                                }
                            }
                        }
                    }
                }
            }
            return wallet;
        }

        internal IEnumerable<HdAddress> GetAllWalletAddressesByCoinType(string walletName, CoinType coinType)
        {
            Wallet wallet = GetWallet(walletName);
            return wallet?.GetAllAddressesByCoinType(this.coinType);
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
        /// <summary>
        /// Finds and returns wallet object based on address
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        internal Wallet GetWalletByAddress(string address)
        {
            Wallet wallet = null;
            using (DBreeze.Transactions.Transaction breezeTransaction = this.DBreeze.GetTransaction())
            {
                breezeTransaction.ValuesLazyLoadingIsOn = false;
                var row = breezeTransaction.SelectForwardStartsWith<byte[], byte[]>("Address", 3.ToIndex(address)).FirstOrDefault<Row<byte[], byte[]>>();
                //var row = breezeTransaction.Select<string, byte[]>("Address", 3.ToIndex(address));
                if (row!=null && row.Exists)
                {
                    var temp = row.ObjectGet<JObject>();
                    var hdAddress = temp.Entity.ToObject<HdAddress>();
                    if (hdAddress != null)
                    {
                        var pairRow = breezeTransaction.Select<long, long>("AddressToWalletPair", hdAddress.Id);
                        if (pairRow.Exists)
                        {
                            long walletId = pairRow.Value;
                            var breezeObject = breezeTransaction.SelectForwardStartsWith<byte[], byte[]>("Wallet", 2.ToIndex(walletId)).FirstOrDefault<Row<byte[], byte[]>>();
                                
                            if (breezeObject != null)
                            {
                                var walletObject = breezeObject.ObjectGet<JObject>();
                                wallet = walletObject.Entity.ToObject<Wallet>();
                            }                            
                        }                        
                    }
                }                
            }
            return wallet;
        }

        /// <summary>
        /// Finds and returns wallet object based on address
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        internal Wallet GetWalletByAddress(long addressId)
        {
            Wallet wallet = null;
            using (DBreeze.Transactions.Transaction breezeTransaction = this.DBreeze.GetTransaction())
            {

                breezeTransaction.ValuesLazyLoadingIsOn = false;

                var row = breezeTransaction.Select<byte[], byte[]>("Address", 2.ToIndex(addressId));
                if (row.Exists)
                {
                    var temp = row.ObjectGet<JObject>();
                    var hdAddress = temp.Entity.ToObject<HdAddress>();
                    if (hdAddress != null)
                    {
                        var pairRow = breezeTransaction.Select<byte[], long>("AddressToWalletPair", 1.ToIndex(hdAddress.Id));
                        if (pairRow.Exists)
                        {
                            long walletId = pairRow.Value;
                            var breezeObject = breezeTransaction.Select<byte[], byte[]>("Wallet", 2.ToIndex(walletId)).ObjectGet<JObject>();
                            if (breezeObject != null)
                            {
                                wallet = breezeObject.Entity.ToObject<Wallet>();
                            }
                        }
                    }
                }
            }
            return wallet;
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