using System;
using System.Collections.Generic;
using BRhodium.Node.Utilities;
using DBreeze;
using DBreeze.DataTypes;
using DBreeze.Objects;
using DBreeze.Utils;
using NBitcoin;
using System.Linq;
//using DBreeze.Transactions;
using System.Data.SQLite;
using NBitcoin.DataEncoders;

namespace BRhodium.Bitcoin.Features.Wallet
{
    public class WalletRepository
    {
        private readonly string walletPath;
        private readonly CoinType coinType;
        /// <summary>Access to DBreeze database.</summary>
        //protected readonly DBreezeEngine dBreeze;
        private readonly Network network;
        private Dictionary<string, Wallet> walletCache = new Dictionary<string, Wallet>();
        protected readonly SQLiteConnection connection;

        public WalletRepository(string walletPath, CoinType coinType, Network network = null)
        {
            this.coinType = coinType;
            this.walletPath = walletPath;
            this.network = network;
            //this.dBreeze = new DBreezeEngine(walletPath);
            this.connection = new SQLiteConnection(new SQLiteConnectionStringBuilder
            {
                DataSource = $"{walletPath}\\Wallet.db"
            }
            .ToString());
            this.connection.Open();
        }

        public void SaveWallet(string walletName, Wallet wallet)
        {
            Guard.NotNull(walletName, nameof(walletName));
            Guard.NotNull(wallet, nameof(wallet));

            //reset cache so that get all the references from db rebuilt
            walletCache.Remove(wallet.Name);
            walletCache.Remove("wallet_" + wallet.Id);

            //Task task = Task.Run(() =>
            // {
            using (var dbTransaction = this.connection.BeginTransaction())
            {
                bool newEntity = false;
                if (wallet.Id < 1)
                {
                    var insertCommand = connection.CreateCommand();
                    insertCommand.Transaction = dbTransaction;
                    insertCommand.CommandText = "INSERT INTO Wallet ( Name, EncryptedSeed, ChainCode, Network, CreationTime, LastBlockSyncedHash, LastBlockSyncedHeight, CoinType, LastUpdated) " +
                    "VALUES ( $Name, $EncryptedSeed, $ChainCode, $Network, $CreationTime, $LastBlockSyncedHash, $LastBlockSyncedHeight, $CoinType, $LastUpdated)";
                    insertCommand.Parameters.AddWithValue("$Name", wallet.Name);
                    insertCommand.Parameters.AddWithValue("$EncryptedSeed", wallet.EncryptedSeed);
                    insertCommand.Parameters.AddWithValue("$ChainCode", wallet.ChainCode.ToBase64String());
                    insertCommand.Parameters.AddWithValue("$Network", wallet.Network.Name);
                    insertCommand.Parameters.AddWithValue("$CreationTime", wallet.CreationTime.ToUnixTimeSeconds());
                    insertCommand.Parameters.AddWithValue("$LastBlockSyncedHash", wallet.AccountsRoot.FirstOrDefault()?.LastBlockSyncedHash);
                    insertCommand.Parameters.AddWithValue("$LastBlockSyncedHeight", wallet.AccountsRoot.FirstOrDefault()?.LastBlockSyncedHeight);
                    insertCommand.Parameters.AddWithValue("$CoinType", (int)wallet.AccountsRoot.FirstOrDefault().CoinType);
                    insertCommand.Parameters.AddWithValue("$LastUpdated", DateTimeOffset.Now.ToUnixTimeSeconds());

                    insertCommand.ExecuteNonQuery();

                    var selectCommand = connection.CreateCommand();
                    selectCommand.Transaction = dbTransaction;
                    selectCommand.CommandText = "SELECT id FROM Wallet WHERE Name = $Name";
                    selectCommand.Parameters.AddWithValue("$Name", wallet.Name);
                    using (var reader = selectCommand.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            wallet.Id = reader.GetInt32(0);
                        }
                    }
                    newEntity = true;
                }

                foreach (var item in wallet.AccountsRoot.FirstOrDefault<AccountRoot>().Accounts)
                {
                    SaveAccount(wallet.Id, dbTransaction, item);
                }

                // Index addresses.
                foreach (var account in wallet.GetAccountsByCoinType(this.coinType))
                {
                    foreach (var address in account.ExternalAddresses)
                    {
                        SaveAddress(wallet.Id, dbTransaction, address);
                    }
                    foreach (var address in account.InternalAddresses)
                    {
                        SaveAddress(wallet.Id, dbTransaction, address);
                    }
                }
                
                if (wallet.BlockLocator != null && wallet.BlockLocator.Count > 0) {
                    SaveBlockLocator(wallet.Name, new BlockLocator(){
                        Blocks = wallet.BlockLocator
                    });
                }

                dbTransaction.Commit();

            }
            //});
            //return task;
        }


        public Wallet GetWalletByName(string name)
        {
            if (walletCache.ContainsKey(name))
            {
                return walletCache[name];
            }

            Wallet wallet = null;

            using (var dbTransaction = this.connection.BeginTransaction())
            {
                var selectWalletCmd = connection.CreateCommand();
                selectWalletCmd.Transaction = dbTransaction;
                selectWalletCmd.CommandText = "SELECT id,Name,EncryptedSeed,ChainCode,Network,CreationTime," +
                    "LastBlockSyncedHash,LastBlockSyncedHeight, CoinType FROM Wallet WHERE Name = $Name";
                selectWalletCmd.Parameters.AddWithValue("$Name", name);

                wallet = ReadWalletFromDb(wallet, dbTransaction, selectWalletCmd);
            }

            if (wallet != null)
            {
                walletCache[name] = wallet;
            }
            return wallet;
        }

        private Wallet ReadWalletFromDb(Wallet wallet, SQLiteTransaction dbTransaction, SQLiteCommand selectWalletCmd)
        {
            using (var reader = selectWalletCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    wallet = new Wallet();
                    wallet.Id = reader.GetInt32(0);
                    wallet.Name = reader.GetString(1);
                    wallet.EncryptedSeed = reader.GetString(2);
                    wallet.ChainCode = Convert.FromBase64String(reader.GetString(3));
                    wallet.Network = NetworkHelpers.GetNetwork(reader.GetString(4));
                    wallet.CreationTime = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(5));
                    string blockHash = reader[6] as string;
                    int LastBlockSyncedHeight = 0;

                    if (reader[7].GetType() != typeof(DBNull))
                    {
                        LastBlockSyncedHeight = (int)reader[7];
                    }

                    int coinType = reader.GetInt16(8);
                    uint256 lastBlockHash = null;
                    if (!String.IsNullOrEmpty(blockHash))
                    {
                        lastBlockHash = new uint256(reader.GetString(4));
                    }
                    BuildAccountRoot(wallet, dbTransaction, LastBlockSyncedHeight, coinType, lastBlockHash);
                }
            }

            return wallet;
        }

        private void BuildAccountRoot(Wallet wallet, SQLiteTransaction dbTransaction, int LastBlockSyncedHeight, int coinType, uint256 lastBlockHash)
        {
            HdAccount hdAccount = null;
            List<HdAccount> accounts = new List<HdAccount>();
            var selectAccountsCmd = connection.CreateCommand();
            selectAccountsCmd.Transaction = dbTransaction;
            selectAccountsCmd.CommandText = "SELECT id, HdIndex, Name, HdPath, ExtendedPubKey, CreationTime " +
                " FROM Account WHERE WalletId = $WalletId order by HdIndex asc";
            selectAccountsCmd.Parameters.AddWithValue("$WalletId", wallet.Id);
            using (var reader = selectAccountsCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    accounts.Add(new HdAccount()
                    {
                        Id = reader.GetInt32(0),
                        Index = reader.GetInt32(1),
                        Name = reader.GetString(2),
                        HdPath = reader.GetString(3),
                        ExtendedPubKey = reader.GetString(4),
                        CreationTime = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(5))
                    });
                }
            }

            wallet.AccountsRoot.Add(new AccountRoot()
            {
                LastBlockSyncedHash = lastBlockHash,
                LastBlockSyncedHeight = EvalNullableInt(LastBlockSyncedHeight),
                CoinType = (CoinType)coinType,
                Accounts = accounts
            });

            //in single account scenario logic is simple
            if (accounts.Count == 1)
            {
                hdAccount = accounts[0];
            }

            var selectAddressesCmd = connection.CreateCommand();
            selectAddressesCmd.Transaction = dbTransaction;
            selectAddressesCmd.CommandText = "SELECT id, WalletId, HdIndex, ScriptPubKey, Pubkey, Address, HdPath " +
                " FROM Address WHERE WalletId = $WalletId order by HdIndex asc";
            selectAddressesCmd.Parameters.AddWithValue("$WalletId", wallet.Id);
            using (var reader = selectAddressesCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var address = new HdAddress()
                    {
                        Id = reader.GetInt32(0),
                        WalletId = reader.GetInt32(1),
                        Index = reader.GetInt32(2),
                        ScriptPubKey = EvalScript(reader.GetString(3)),
                        Pubkey = EvalScript(reader.GetString(4)),
                        Address = reader.GetString(5),
                        HdPath = reader.GetString(6)
                    };

                    //if not initialized or different than previous find and cache 
                    if (hdAccount == null || !address.HdPath.Contains(hdAccount.HdPath))
                    {
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
                    else
                    {
                        throw new Exception("Could not locate account");
                    }
                }
            }

            List<uint256> locator = new List<uint256>();

            var selectWalletCmd = connection.CreateCommand();
            selectWalletCmd.Transaction = dbTransaction;
            selectWalletCmd.CommandText = "SELECT  BlockHash FROM BlockLocator WHERE WalletId = $WalletId";
            selectAddressesCmd.Parameters.AddWithValue("$WalletId", wallet.Id);
            using (var reader = selectWalletCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    locator.Add(new uint256(reader.GetString(0)));
                }
            }
            wallet.BlockLocator = locator;
        }

        private Script EvalScript(string value)
        {
            return Script.FromBytesUnsafe(Encoders.Hex.DecodeData(value));
        }

        private int? EvalNullableInt(int lastBlockSyncedHeight)
        {
            if (lastBlockSyncedHeight > 0)
            {
                return lastBlockSyncedHeight;
            }
            return null;
        }

        public Wallet GetWalletById(long id)
        {
            if (id < 1)
            {
                return null;
            }
            string cacheKey = "wallet_" + id;
            if (walletCache.ContainsKey(cacheKey))
            {
                return walletCache[cacheKey];
            }

            Wallet wallet = null;

            using (var dbTransaction = this.connection.BeginTransaction())
            {
                var selectWalletCmd = connection.CreateCommand();
                selectWalletCmd.Transaction = dbTransaction;
                selectWalletCmd.CommandText = "SELECT id,Name,EncryptedSeed,ChainCode,Network,CreationTime," +
                    "LastBlockSyncedHash,LastBlockSyncedHeight, CoinType FROM Wallet WHERE id = $id";
                selectWalletCmd.Parameters.AddWithValue("$id", id);

                wallet = ReadWalletFromDb(wallet, dbTransaction, selectWalletCmd);
            }

            if (wallet != null)
            {
                walletCache[wallet.Name] = wallet;
                walletCache["wallet_" + wallet.Id] = wallet;
            }

            return wallet;
        }




        public long SaveAddress(long walletId, HdAddress address)
        {
            using (var dbTransaction = this.connection.BeginTransaction())
            {
                var retval = SaveAddress(walletId, dbTransaction, address);
                dbTransaction.Commit();
                return retval;
            }
        }

        /// <summary>
        /// Saves address to db making it queryable by address, ScriptPubKey. It relates to account through HD path. Does not commit transaction itself. Caller controlls transaction and must commit.
        /// </summary>
        /// <param name="wallet"></param>
        /// <param name="dbTransaction"></param>
        /// <param name="address"></param>
        private long SaveAddress(long walletId, SQLiteTransaction dbTransaction, HdAddress address)
        {
            if (address.Id < 1)
            {
                var insertCommand = this.connection.CreateCommand();
                insertCommand.Transaction = dbTransaction;
                insertCommand.CommandText = "INSERT INTO Address (WalletId, HdIndex, ScriptPubKey, Pubkey, Address, HdPath ) " +
                " VALUES ( $WalletId, $Index, $ScriptPubKey, $Pubkey, $Address, $HdPath )";
                insertCommand.Parameters.AddWithValue("$WalletId", walletId);
                insertCommand.Parameters.AddWithValue("$Index", address.Index);
                insertCommand.Parameters.AddWithValue("$ScriptPubKey", PackageSriptToString(address.ScriptPubKey));
                insertCommand.Parameters.AddWithValue("$Pubkey", PackageSriptToString(address.Pubkey));
                insertCommand.Parameters.AddWithValue("$Address", address.Address);
                insertCommand.Parameters.AddWithValue("$HdPath", address.HdPath);

                insertCommand.ExecuteNonQuery();

                var selectAccountsCmd = connection.CreateCommand();
                selectAccountsCmd.Transaction = dbTransaction;
                selectAccountsCmd.CommandText = "SELECT id  FROM Address WHERE Address = $Address";
                selectAccountsCmd.Parameters.AddWithValue("$Address", address.Address);
                using (var reader = selectAccountsCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        address.Id = reader.GetInt64(0);
                    }
                }
            }
            address.WalletId = walletId;
            return address.Id;
        }


        private long SaveAccount(long walletId, SQLiteTransaction dbTransaction, HdAccount account)
        {
            if (account.Id < 1)
            {
                var insertCommand = this.connection.CreateCommand();
                insertCommand.Transaction = dbTransaction;
                insertCommand.CommandText = "INSERT INTO Account (WalletId, HdIndex, Name, HdPath, ExtendedPubKey, CreationTime ) " +
                " VALUES ( $WalletId, $Index, $Name, $HdPath, $ExtendedPubKey, $CreationTime )";
                insertCommand.Parameters.AddWithValue("$WalletId", walletId);
                insertCommand.Parameters.AddWithValue("$Index", account.Index);
                insertCommand.Parameters.AddWithValue("$Name", account.Name);
                insertCommand.Parameters.AddWithValue("$HdPath", account.HdPath);
                insertCommand.Parameters.AddWithValue("$ExtendedPubKey", account.ExtendedPubKey);
                insertCommand.Parameters.AddWithValue("$CreationTime", account.CreationTime.ToUnixTimeSeconds());

                insertCommand.ExecuteNonQuery();

                var selectAccountsCmd = connection.CreateCommand();
                selectAccountsCmd.Transaction = dbTransaction;
                selectAccountsCmd.CommandText = "SELECT max(id)  FROM Account WHERE WalletId = $WalletId";
                selectAccountsCmd.Parameters.AddWithValue("$WalletId", walletId);
                using (var reader = selectAccountsCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        account.Id = reader.GetInt64(0);
                    }
                }
            }
            return account.Id;
        }

        private string PackageSriptToString(Script value)
        {
            if (value is Script)
            {
                return Encoders.Hex.EncodeData(((Script)value).ToBytes(false));
            }
            if (value is WitScript)
            {
                return ((WitScript)value).ToString();
            }
            return null;
        }

        public void SaveLastSyncedBlock(string walletName, ChainedHeader chainedHeader)
        {
            Guard.NotNull(walletName, nameof(walletName));
            using (var dbTransaction = this.connection.BeginTransaction())
            {
                var insertCommand = connection.CreateCommand();
                insertCommand.Transaction = dbTransaction;
                insertCommand.CommandText = "UPDATE Wallet set LastBlockSyncedHash = $LastBlockSyncedHash, LastBlockSyncedHeight = $LastBlockSyncedHeight,  LastUpdated = $LastUpdated WHERE Name = $Name";

                insertCommand.Parameters.AddWithValue("$Name", walletName);
                insertCommand.Parameters.AddWithValue("$LastBlockSyncedHash", chainedHeader.HashBlock);
                insertCommand.Parameters.AddWithValue("$LastBlockSyncedHeight", chainedHeader.Height);
                insertCommand.Parameters.AddWithValue("$LastUpdated", DateTimeOffset.Now.ToUnixTimeSeconds());

                insertCommand.ExecuteNonQuery();

                dbTransaction.Commit();
            }
        }


        public WalletSyncPosition GetLastSyncedBlock()
        {
            WalletSyncPosition syncPosition = null;
            using (var dbTransaction = this.connection.BeginTransaction())
            {
                var selectWalletCmd = connection.CreateCommand();
                selectWalletCmd.Transaction = dbTransaction;
                selectWalletCmd.CommandText = "SELECT top 1 LastBlockSyncedHeight , LastBlockSyncedHash FROM Wallet WHERE  order by LastUpdated desc";

                using (var reader = selectWalletCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        syncPosition = new WalletSyncPosition()
                        {
                            Height = reader.GetInt32(0),
                            BlockHash = uint256.Parse(reader.GetString(1))
                        };
                    }
                }
            }

            return syncPosition;
        }

        private WalletSyncPosition GetLastSyncedBlock(string walletName)
        {
            WalletSyncPosition syncPosition = null;
            using (var dbTransaction = this.connection.BeginTransaction())
            {
                var selectWalletCmd = connection.CreateCommand();
                selectWalletCmd.Transaction = dbTransaction;
                selectWalletCmd.CommandText = "SELECT LastBlockSyncedHeight , LastBlockSyncedHash FROM Wallet WHERE walletName = $Name ";
                selectWalletCmd.Parameters.AddWithValue("$Name", walletName);

                using (var reader = selectWalletCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        syncPosition = new WalletSyncPosition()
                        {
                            Height = reader.GetInt32(0),
                            BlockHash = uint256.Parse(reader.GetString(1))
                        };
                    }
                }
            }

            return syncPosition;
        }

        internal IEnumerable<HdAddress> GetAllWalletAddressesByCoinType(string walletName, CoinType coinType)
        {
            Wallet wallet = GetWalletByName(walletName);
            return wallet?.GetAllAddressesByCoinType(this.coinType);
        }

        public IEnumerable<string> GetAllWalletNames()//TODO: implement caching
        {
            List<string> result = new List<string>();

            using (var dbTransaction = this.connection.BeginTransaction())
            {
                var selectWalletCmd = connection.CreateCommand();
                selectWalletCmd.Transaction = dbTransaction;
                selectWalletCmd.CommandText = "SELECT Name FROM Wallet";
                using (var reader = selectWalletCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(reader.GetString(1));
                    }
                }
            }
            return result;
        }

        public IEnumerable<WalletPointer> GetAllWalletPointers()
        {
            List<WalletPointer> result = new List<WalletPointer>();

            using (var dbTransaction = this.connection.BeginTransaction())
            {
                var selectWalletCmd = connection.CreateCommand();
                selectWalletCmd.Transaction = dbTransaction;
                selectWalletCmd.CommandText = "SELECT id,Name FROM Wallet";

                using (var reader = selectWalletCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new WalletPointer(reader.GetInt32(0), reader.GetString(1)));
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Stores blocks for future use.
        /// </summary>
        /// <param name="walletName"></param>
        /// <param name="blocks"></param>
        public void SaveBlockLocator(string walletName, BlockLocator blocks)
        {
            Guard.NotNull(walletName, nameof(walletName));
            HashSet<uint256> blocksFromDb = new HashSet<uint256>();
            long walletId = 0;
            using (var dbTransaction = this.connection.BeginTransaction())
            {
                var selectWalletCmd = connection.CreateCommand();
                selectWalletCmd.Transaction = dbTransaction;
                selectWalletCmd.CommandText = "SELECT Wallet.id, Wallet.Name, BlockLocator.BlockHash  FROM Wallet " +
                    "LEFT JOIN BlockLocator ON Wallet.Id = BlockLocator.WalletId  WHERE Name = $Name ";
                selectWalletCmd.Parameters.AddWithValue("$Name", walletName);

                using (var reader = selectWalletCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        walletId = reader.GetInt32(0);
                        blocksFromDb.Add(new uint256(reader.GetString(3)));
                    }
                }

                foreach (var item in blocks.Blocks)
                {
                    if (!blocksFromDb.Contains(item))
                    {
                        var insertCommand = this.connection.CreateCommand();
                        insertCommand.Transaction = dbTransaction;
                        insertCommand.CommandText = "INSERT INTO BlockLocator (WalletId, BlockHash ) " +
                        " VALUES ( $WalletId, $BlockHash )";
                        insertCommand.Parameters.AddWithValue("$WalletId", walletId);
                        insertCommand.Parameters.AddWithValue("$BlockHash", item.ToString());

                        insertCommand.ExecuteNonQuery();
                    }
                }
                dbTransaction.Commit();
            } 
        }

        public ICollection<uint256> GetFirstWalletBlockLocator()
        {
            List<uint256> result = new List<uint256>();

            using (var dbTransaction = this.connection.BeginTransaction())
            {
                var selectWalletCmd = connection.CreateCommand();
                selectWalletCmd.Transaction = dbTransaction;
                selectWalletCmd.CommandText = "SELECT  BlockHash FROM BlockLocator INNER JOIN Wallet ON BlockLocator.WalletId = Wallet.Id order by CreationTime asc";

                using (var reader = selectWalletCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new uint256(reader.GetString(0)));
                    }
                }
            }

            return result;
        }

        internal int? GetEarliestWalletHeight()
        {
            int? result = null;

            using (var dbTransaction = this.connection.BeginTransaction())
            {
                var selectWalletCmd = connection.CreateCommand();
                selectWalletCmd.Transaction = dbTransaction;
                selectWalletCmd.CommandText = "SELECT top 1 LastBlockSyncedHeight FROM Wallet order by CreationTime asc";

                using (var reader = selectWalletCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result = reader.GetInt32(0);
                    }
                }
            }
            return result;
        }
        internal DateTimeOffset GetOldestWalletCreationTime()
        {
            DateTimeOffset result = DateTimeOffset.MinValue;

            using (var dbTransaction = this.connection.BeginTransaction())
            {
                var selectWalletCmd = connection.CreateCommand();
                selectWalletCmd.Transaction = dbTransaction;
                selectWalletCmd.CommandText = "SELECT top 1 CreationTime FROM Wallet order by CreationTime asc";

                using (var reader = selectWalletCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(0));
                    }
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
            long walletId = 0;
            using (var dbTransaction = this.connection.BeginTransaction())
            {
                var selectAccountsCmd = connection.CreateCommand();
                selectAccountsCmd.Transaction = dbTransaction;
                selectAccountsCmd.CommandText = "SELECT WalletId FROM Address WHERE Address = $Address";
                selectAccountsCmd.Parameters.AddWithValue("$Address", address);
                using (var reader = selectAccountsCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        walletId = reader.GetInt64(1);
                    }
                }
            }

            wallet = GetWalletById(walletId);

            return wallet;
        }

        internal Wallet GetWalletByScriptHash(ScriptId hash)
        {
            Wallet wallet = null;
            long walletId = 0;
            using (var dbTransaction = this.connection.BeginTransaction())
            {
                var selectAccountsCmd = connection.CreateCommand();
                selectAccountsCmd.Transaction = dbTransaction;
                selectAccountsCmd.CommandText = "SELECT WalletId FROM Address WHERE ScriptPubKey = $ScriptPubKey";
                selectAccountsCmd.Parameters.AddWithValue("$ScriptPubKey", PackageSriptToString(hash.ScriptPubKey));

                using (var reader = selectAccountsCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        walletId = reader.GetInt64(1);
                    }
                }
            }

            wallet = GetWalletById(walletId);

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

        internal uint256 GetLastUpdatedBlockHash()
        {
            uint256 result = null;


            using (var dbTransaction = this.connection.BeginTransaction())
            {
                var selectWalletCmd = connection.CreateCommand();
                selectWalletCmd.Transaction = dbTransaction;
                selectWalletCmd.CommandText = "SELECT top 1 LastBlockSyncedHash FROM Wallet order by LastUpdated asc";

                using (var reader = selectWalletCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result = uint256.Parse(reader.GetString(0));
                    }
                }
            }
            return result;
        }

        internal string GetLastUpdatedWalletName()
        {
            string result = null;


            using (var dbTransaction = this.connection.BeginTransaction())
            {
                var selectWalletCmd = connection.CreateCommand();
                selectWalletCmd.Transaction = dbTransaction;
                selectWalletCmd.CommandText = "SELECT top 1 Name FROM Wallet order by LastUpdated asc";

                using (var reader = selectWalletCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result = reader.GetString(0);
                    }
                }
            }
            return result;
        }


        public bool HasWallets()
        {
            return !String.IsNullOrEmpty(GetLastUpdatedWalletName());
        }
    }
}