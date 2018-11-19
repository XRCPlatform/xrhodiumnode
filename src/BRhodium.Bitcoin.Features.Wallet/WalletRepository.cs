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
using DBreeze.Transactions;

namespace BRhodium.Bitcoin.Features.Wallet
{
    public class WalletRepository
    {
        private readonly string walletPath;
        private readonly CoinType coinType;
        /// <summary>Access to DBreeze database.</summary>
        protected readonly DBreezeEngine DBreeze;
        private readonly Network network;
        private DBreezeSerializer dBreezeSerializer;

        public WalletRepository(string walletPath, CoinType coinType, Network network = null)
        {
            this.coinType = coinType;
            this.walletPath = walletPath;
            this.network = network;
            this.DBreeze = new DBreezeEngine(walletPath);
            this.dBreezeSerializer = new DBreezeSerializer();
            this.dBreezeSerializer.Initialize(network);
        }

        public Task SaveWallet(string walletName, Wallet wallet)
        {
            Guard.NotNull(walletName, nameof(walletName));
            Guard.NotNull(wallet, nameof(wallet));

            walletNames.Clear();//reset cache perhaps a bit brutal but fair

            Task task = Task.Run(() =>
            {
                using (DBreeze.Transactions.Transaction breezeTransaction = this.DBreeze.GetTransaction())
                {
                    breezeTransaction.SynchronizeTables("Wallet", "WalletNames", "Address", "AddressToWalletPair");
                    bool newEntity = false;
                    if (GetLastSyncedBlock(walletName, breezeTransaction) == null)
                    {
                        if (wallet.Id < 1)
                        {
                            wallet.Id = breezeTransaction.ObjectGetNewIdentity<long>("Wallet");
                            newEntity = true;
                        }
                    }
                   
                    DateTime dateTime = wallet.CreationTime.DateTime;
                    

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
                        //reset to not save them as blobs
                        account.ExternalAddresses.Clear();
                        account.InternalAddresses.Clear();
                    }

                    breezeTransaction.ObjectInsert("Wallet", new DBreezeObject<Wallet>
                    {
                        NewEntity = newEntity,
                        Entity = wallet,
                        Indexes = new List<DBreezeIndex>
                            {
                                new DBreezeIndex(1,wallet.Name) { PrimaryIndex = true },
                                new DBreezeIndex(2,wallet.Id),
                                new DBreezeIndex(3,dateTime)
                            }
                    }, false);
                    if (newEntity)
                    {
                        //used when we find all wallets, to avoid lifting all wallet blobs
                        breezeTransaction.Insert<long, string>("WalletNames", wallet.Id, wallet.Name);
                    }

                    if (wallet.BlockLocator != null && wallet.BlockLocator.Count > 0) {
                        SaveBlockLocator(wallet.Name, breezeTransaction, new BlockLocator(){
                            Blocks = wallet.BlockLocator
                        });
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
            address.WalletId = wallet.Id;
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
            //think about how to remove these in case of fork as saveAddress now does not 
            foreach (var transaction in address.Transactions)
            {
                breezeTransaction.Insert<byte[], TransactionData>("OutPointLookup", new OutPoint(transaction.Id, transaction.Index).ToBytes(), transaction);
            }
            if (newEntity) {
                //used when we find address in transaction to find right wallet and GetWalletByAddress API
                breezeTransaction.Insert<string, string>("AddressToWalletPair", address.Address, wallet.Name);
                breezeTransaction.Insert<byte[], string>("ScriptToWalletPair", address.ScriptPubKey.Hash.ToBytes(), wallet.Name);
            }           
        }
        public void SaveAddress(long walletId, HdAddress address)
        {
            using (DBreeze.Transactions.Transaction breezeTransaction = this.DBreeze.GetTransaction())
            {
                bool newEntity = false;
                if (address.Id < 1)
                {
                    address.Id = breezeTransaction.ObjectGetNewIdentity<long>("Address");
                    newEntity = true;
                }
                address.WalletId = walletId;
                int subIndex = address.IsChangeAddress() ? 1 : 0;
                breezeTransaction.ObjectInsert("Address", new DBreezeObject<HdAddress>
                {
                    NewEntity = newEntity,
                    Entity = address,
                    Indexes = new List<DBreezeIndex>
                    {
                        new DBreezeIndex(1,walletId, subIndex, address.Index) { PrimaryIndex = true },
                        new DBreezeIndex(2,walletId),
                        new DBreezeIndex(3,address.Address),
                        new DBreezeIndex(4,address.Id),
                        new DBreezeIndex(5,address.ScriptPubKey.ToBytes())
                    }
                }, false);

                foreach (var transaction in address.Transactions)
                {
                    breezeTransaction.Insert<byte[], TransactionData>("OutPointLookup", new OutPoint(transaction.Id, transaction.Index).ToBytes(), transaction);
                }

                breezeTransaction.Commit();
            }
        }
        public void SaveLastSyncedBlock(string walletName, ChainedHeader chainedHeader)
        {
            Guard.NotNull(walletName, nameof(walletName));

            using (DBreeze.Transactions.Transaction breezeTransaction = this.DBreeze.GetTransaction())
            {
                WalletSyncPosition syncPosition = new WalletSyncPosition() {
                    Height= chainedHeader.Height,
                    BlockHash = chainedHeader.HashBlock
                };
                breezeTransaction.Insert<string, WalletSyncPosition>("WalletLastBlockSyncedBlock", walletName, syncPosition);
                breezeTransaction.Commit();
            }
        }

        public WalletSyncPosition GetLastSyncedBlock(string walletName)
        {
            Guard.NotNull(walletName, nameof(walletName));
            WalletSyncPosition syncPosition = null;
            using (DBreeze.Transactions.Transaction breezeTransaction = this.DBreeze.GetTransaction())
            {
                syncPosition = GetLastSyncedBlock(walletName,  breezeTransaction);
            }
            return syncPosition;
        }

        private  WalletSyncPosition GetLastSyncedBlock(string walletName,  DBreeze.Transactions.Transaction breezeTransaction)
        {
            WalletSyncPosition syncPosition = null;
            var row = breezeTransaction.Select<string, WalletSyncPosition>("WalletLastBlockSyncedBlock", walletName);
            if (row.Exists)
            {
                syncPosition = row.Value;              
            }

            return syncPosition;
        }

        public Wallet GetWallet(string name)
        {
            Wallet wallet = null;
            using (DBreeze.Transactions.Transaction breezeTransaction = this.DBreeze.GetTransaction())
            {
                breezeTransaction.ValuesLazyLoadingIsOn = false;
                var obj = breezeTransaction.Select<byte[], Wallet>("Wallet", 1.ToIndex(name)).ObjectGet<Wallet>();
                if (obj != null)
                {
                    HdAccount hdAccount = null;
                    wallet = obj.Entity;
                    var position = GetLastSyncedBlock(name, breezeTransaction);
                    if (position != null)
                    {
                        // Update the wallets with the last processed block height.
                        foreach (AccountRoot accountRoot in wallet.AccountsRoot.Where(a => a.CoinType == this.coinType))
                        {
                            accountRoot.LastBlockSyncedHeight = position.Height;
                            accountRoot.LastBlockSyncedHash = position.BlockHash;
                        }
                    }

                    foreach (var row in breezeTransaction.SelectForwardStartsWith<byte[], byte[]>("Address", 2.ToIndex(wallet.Id)))
                    {
                        var temp = row.ObjectGet<HdAddress>();
                        var address = temp.Entity;
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
                var blockLocator = breezeTransaction.Select<string, BlockLocator>("WalletBlockLocator", name, false);
                if (blockLocator.Exists)
                {
                    if (wallet.BlockLocator == null) {
                        wallet.BlockLocator = new List<uint256>();
                    }
                    wallet.BlockLocator = blockLocator.Value.Blocks;
                }
            }
            return wallet;
        }

        internal IEnumerable<HdAddress> GetAllWalletAddressesByCoinType(string walletName, CoinType coinType)
        {
            Wallet wallet = GetWallet(walletName);
            return wallet?.GetAllAddressesByCoinType(this.coinType);
        }

        List<string> walletNames = new List<string>();
        public IEnumerable<string> GetAllWalletNames()
        {
            List<string> result = new List<string>();
            if (walletNames.Count < 1)
            {
                using (DBreeze.Transactions.Transaction breezeTransaction = this.DBreeze.GetTransaction())
                {
                    foreach (var row in breezeTransaction.SelectForward<long, string>("WalletNames"))
                    {
                        if (row.Exists)
                        {
                            result.Add(row.Value);
                        }
                    }
                }
            }
            
            walletNames = result;
            return result;
        }

        public IEnumerable<WalletPointer> GetAllWalletPointers()
        {
            List<WalletPointer> result = new List<WalletPointer>();
            using (DBreeze.Transactions.Transaction breezeTransaction = this.DBreeze.GetTransaction())
            {
                foreach (var row in breezeTransaction.SelectForward<long, string>("WalletNames"))
                {
                    if (row.Exists)
                    {
                        result.Add(new WalletPointer(row.Key,row.Value));
                    }
                }
            }
            return result;
        }
        private void SaveBlockLocator(string walletName, DBreeze.Transactions.Transaction breezeTransaction, BlockLocator blockLocator)
        {
            breezeTransaction.Insert<string, BlockLocator>("WalletBlockLocator", walletName, blockLocator);
        }
        /// <summary>
        /// Stores blocks for future use.
        /// </summary>
        /// <param name="walletName"></param>
        /// <param name="blocks"></param>
        public void SaveBlockLocator(string walletName, BlockLocator blocks)
        {
            Guard.NotNull(walletName, nameof(walletName));

            using (DBreeze.Transactions.Transaction breezeTransaction = this.DBreeze.GetTransaction())
            {
                SaveBlockLocator(walletName, breezeTransaction, blocks);
                breezeTransaction.Commit();
            }
        }

        public ICollection<uint256> GetFirstWalletBlockLocator()
        {
            List<uint256> result = new List<uint256>();
            using (DBreeze.Transactions.Transaction breezeTransaction = this.DBreeze.GetTransaction())
            {
                foreach (var row in breezeTransaction.SelectForward<int, BlockLocator>("WalletBlockLocator"))
                {
                    if (row.Exists)
                    {
                        result = row.Value.Blocks;
                    }
                    break;
                }
            }
            return result;
            
        }

        internal int? GetEarliestWalletHeight()
        {
            int? result = null;
            using (DBreeze.Transactions.Transaction breezeTransaction = this.DBreeze.GetTransaction())
            {

                foreach (var row in breezeTransaction.SelectForwardFromTo<byte[], byte[]>("Wallet",
                    3.ToIndex(DateTime.MinValue, long.MinValue), true,
                    3.ToIndex(DateTime.MaxValue, long.MaxValue), true))
                {
                    if (row.Exists)
                    {
                        var r = row.ObjectGet<Wallet>();
                        var wallet = r.Entity;
                        if (wallet != null) {
                            var pos = GetLastSyncedBlock(wallet.Name, breezeTransaction);
                            if (pos!= null)
                            {
                                result = pos.Height;
                            }
                        }
                    }
                    break;
                }
            }
            return result;
        }
        internal DateTimeOffset GetOldestWalletCreationTime()
        {
            DateTimeOffset result = DateTimeOffset.MinValue;
            using (DBreeze.Transactions.Transaction breezeTransaction = this.DBreeze.GetTransaction())
            {
                foreach (var row in breezeTransaction.SelectForwardFromTo<byte[], byte[]>("Wallet",
                    3.ToIndex(DateTime.MinValue, long.MinValue), true,
                    3.ToIndex(DateTime.MaxValue, long.MaxValue), true))
                {
                    if (row.Exists)
                    {
                        var r = row.ObjectGet<Wallet>();
                        var wallet = r.Entity;
                        if (wallet != null)
                        {
                            result = wallet.CreationTime.ToUniversalTime();
                        }
                    }
                    break;
                }
            }
            return result;
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
                var pairRow = breezeTransaction.Select<string, string>("AddressToWalletPair", address);
                if (pairRow.Exists)
                {
                    string walletName = pairRow.Value;
                    wallet = GetWallet(walletName);
                }
            }
            return wallet;
        }

        
        internal void RemoveTransactionFromHdAddress(HdAddress hdAddress, uint256 id)
        {
            hdAddress.Transactions.Remove(hdAddress.Transactions.Single(t => t.Id == id));
            this.SaveAddress(hdAddress.WalletId, hdAddress);
        }

        internal void RemoveTransactionSpendingDetailsFromHdAddress(HdAddress hdAddress, uint256 id)
        {
            hdAddress.Transactions.Remove(hdAddress.Transactions.Single(t => t.SpendingDetails.TransactionId == id));
            this.SaveAddress(hdAddress.WalletId, hdAddress);
        }

        internal string GetLastUpdatedWalletName()
        {
            string result = null;
            using (DBreeze.Transactions.Transaction breezeTransaction = this.DBreeze.GetTransaction())
            {
                foreach (var row in breezeTransaction.SelectBackwardFromTo<byte[], byte[]>("Wallet",
                   3.ToIndex(DateTime.MaxValue, long.MaxValue), true,
                   3.ToIndex(DateTime.MinValue, long.MinValue), true))
                {
                    if (row.Exists)
                    {
                        var r = row.ObjectGet<Wallet>();
                        var wallet = r.Entity;
                        if (wallet != null)
                        {
                            result = wallet.Name;
                        }
                    }
                    break;
                }
            }
            return result;
        }

        internal Wallet GetWalletByScriptHash(ScriptId hash)
        {
            Wallet wallet = null;
            using (DBreeze.Transactions.Transaction breezeTransaction = this.DBreeze.GetTransaction())
            {
                breezeTransaction.ValuesLazyLoadingIsOn = false;
                var pairRow = breezeTransaction.Select<byte[], string>("ScriptToWalletPair", hash.ToBytes());
                if (pairRow.Exists)
                {
                    string walletName = pairRow.Value;
                    wallet = GetWallet(walletName);
                }
            }
            return wallet;
        }

        public bool HasWallets()
        {
            return !String.IsNullOrEmpty(GetLastUpdatedWalletName());
        }
    }
}