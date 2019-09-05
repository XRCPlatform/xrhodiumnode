using System;
using System.Collections.Generic;
using BRhodium.Node.Utilities;
using DBreeze;
using DBreeze.DataTypes;
using DBreeze.Objects;
using DBreeze.Utils;
using NBitcoin;
using System.Linq;
using System.Data.SQLite;
using NBitcoin.DataEncoders;
using System.Collections.Concurrent;
using System.IO;
using System.Data;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using BRhodium.Bitcoin.Features.Wallet.Helpers;

namespace BRhodium.Bitcoin.Features.Wallet
{
    public class WalletRepository
    {
        private readonly string walletPath;
        private readonly CoinType coinType;
        private readonly object lockObject;
        private readonly Network network;
        private ConcurrentDictionary<string, Wallet> walletCache = new ConcurrentDictionary<string, Wallet>();
        protected string connection;

        private const string WALLET_DB_FILE = "Wallet.db";

        public WalletRepository(string walletPath, CoinType coinType, Network network = null)
        {
            this.lockObject = new object();
            this.coinType = coinType;
            this.walletPath = walletPath;
            this.network = network;
            this.connection = new SQLiteConnectionStringBuilder
            {
                DataSource = Path.Combine(this.walletPath, WALLET_DB_FILE),
                JournalMode = SQLiteJournalModeEnum.Wal,
                Pooling = true
            }
            .ToString();

            EnsureSQLiteDbExists();
        }

        private void EnsureSQLiteDbExists()
        {
            string filePath = Path.Combine(this.walletPath, WALLET_DB_FILE);
            FileInfo fi = new FileInfo(filePath);
            if (!fi.Exists)
            {
                if (!Directory.Exists(this.walletPath))
                {
                    Directory.CreateDirectory(this.walletPath);
                }

                SQLiteConnection.CreateFile(filePath);

                var dbStructureHelper = new CreateDbStructureHelper();
                dbStructureHelper.CreateIt(this.connection);
            }
        }

        public void SaveWallet(string walletName, Wallet wallet, bool saveTransactionsHere = false)
        {
            Guard.NotNull(walletName, nameof(walletName));
            Guard.NotNull(wallet, nameof(wallet));

            lock (this.lockObject)
            {
                FlushWalletCache(wallet.Id);

                //Task task = Task.Run(() =>
                // {

                using (var dbConnection = new SQLiteConnection(this.connection))
                {
                    dbConnection.Open();
                    
                    using (var dbTransaction = dbConnection.BeginTransaction())
                    {
                        if (wallet.Id < 1)
                        {
                            var sql = "INSERT INTO Wallet ( Name, EncryptedSeed, ChainCode, Network, CreationTime, LastBlockSyncedHash, LastBlockSyncedHeight, CoinType, LastUpdated, Blocks) " +
                            "VALUES ( $Name, $EncryptedSeed, $ChainCode, $Network, $CreationTime, $LastBlockSyncedHash, $LastBlockSyncedHeight, $CoinType, $LastUpdated, $Blocks)";

                            using (var insertCommand = new SQLiteCommand(sql, dbConnection, dbTransaction))
                            {
                                insertCommand.Parameters.AddWithValue("$Name", wallet.Name);
                                insertCommand.Parameters.AddWithValue("$EncryptedSeed", wallet.EncryptedSeed);
                                insertCommand.Parameters.AddWithValue("$ChainCode", wallet.ChainCode?.ToBase64String());
                                insertCommand.Parameters.AddWithValue("$Network", wallet.Network.Name);
                                insertCommand.Parameters.AddWithValue("$CreationTime", wallet.CreationTime.ToUnixTimeSeconds());
                                insertCommand.Parameters.AddWithValue("$LastBlockSyncedHash", wallet.AccountsRoot.FirstOrDefault()?.LastBlockSyncedHash);
                                insertCommand.Parameters.AddWithValue("$LastBlockSyncedHeight", wallet.AccountsRoot.FirstOrDefault()?.LastBlockSyncedHeight);
                                insertCommand.Parameters.AddWithValue("$CoinType", (int)wallet.Network.Consensus.CoinType);
                                insertCommand.Parameters.AddWithValue("$LastUpdated", DateTimeOffset.Now.ToUnixTimeSeconds());

                                byte[] bytes = GetBlockLocatorBytes(wallet.BlockLocator);
                                SQLiteParameter prm = new SQLiteParameter("$Blocks", DbType.Binary, bytes.Length, ParameterDirection.Input, false, 0, 0, null, DataRowVersion.Current, bytes);
                                insertCommand.Parameters.Add(prm);

                                insertCommand.ExecuteNonQuery();
                            }

                            sql = "select last_insert_rowid();";
                            using (var selectCommand = new SQLiteCommand(sql, dbConnection, dbTransaction))
                            {
                                wallet.Id = int.Parse(selectCommand.ExecuteScalar().ToString());
                            }
                        }

                        if (wallet.AccountsRoot.FirstOrDefault<AccountRoot>() != null)
                        {
                            foreach (var item in wallet.AccountsRoot.FirstOrDefault<AccountRoot>().Accounts)
                            {
                                SaveAccount(wallet.Id, item, dbTransaction, dbConnection);
                            }

                            foreach (var account in wallet.GetAccountsByCoinType(this.coinType))
                            {
                                foreach (var address in account.ExternalAddresses)
                                {
                                    SaveAddress(wallet.Id, address, dbTransaction, dbConnection, saveTransactionsHere);
                                }
                                foreach (var address in account.InternalAddresses)
                                {
                                    SaveAddress(wallet.Id, address, dbTransaction, dbConnection, saveTransactionsHere);
                                }
                            }
                        }

                        dbTransaction.Commit();

                        //if (wallet != null)
                        //{
                        //    walletCache.TryAdd(wallet.Name,wallet);
                        //    walletCache.TryAdd("wallet_" + wallet.Id,wallet);
                        //}

                    }
                }
                //});
                //return task;
           }


        }

        private byte[] GetBlockLocatorBytes(List<uint256> blob)
        {
            if (blob == null)
            {
                return new byte[0];
            }
            byte[] bytes;
            IFormatter formatter = new BinaryFormatter();
            using (MemoryStream stream = new MemoryStream())
            {
                formatter.Serialize(stream, blob);
                bytes = stream.ToArray();
            }

            return bytes;
        }

        public Wallet GetWalletByName(string name, bool flushcache = false)
        {
            if (this.walletCache.ContainsKey(name) && !flushcache)
            {
                return this.walletCache[name];
            }
            //System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            //sw.Start();

            Wallet wallet = null;
            var sql = "SELECT ROWID,Name,EncryptedSeed,ChainCode,Network,CreationTime," +
                    "LastBlockSyncedHash,LastBlockSyncedHeight, CoinType, Blocks FROM Wallet WHERE Name = $Id";

            wallet = ReadWalletFromDb(wallet, sql, name);

            //sw.Stop();
            //Console.WriteLine($"Elapsed time {sw.ElapsedMilliseconds}");

            if (wallet != null)
            {
                this.walletCache.TryAdd(wallet.Name, wallet);
                this.walletCache.TryAdd("wallet_" + wallet.Id, wallet);
            }

            return wallet;
        }

        private Wallet ReadWalletFromDb(Wallet wallet, string sql, string id)
        {
            using (var dbConnection = new SQLiteConnection(this.connection))
            {
                dbConnection.Open();

                using (var selectWalletCmd = new SQLiteCommand(sql, dbConnection))
                {
                    selectWalletCmd.Parameters.AddWithValue("$Id", id);

                    using (var reader = selectWalletCmd.ExecuteReader(CommandBehavior.KeyInfo))
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
                            int? LastBlockSyncedHeight = ExtractNullableInt(reader, 7);

                            int coinType = reader.GetInt16(8);
                            uint256 lastBlockHash = null;
                            if (!string.IsNullOrEmpty(blockHash))
                            {
                                lastBlockHash = new uint256(blockHash);
                            }

                            BlockLocator locator = ExtractBlockLocator(reader, 9);
                            wallet.BlockLocator = locator?.Blocks;

                            BuildAccountRoot(wallet, LastBlockSyncedHeight, coinType, lastBlockHash, dbConnection);
                        }
                    }
                }
            }

            return wallet;
        }

        private BlockLocator ExtractBlockLocator(SQLiteDataReader reader, int index)
        {
            if (reader[index].GetType() != typeof(DBNull))
            {
                const int CHUNK_SIZE = 2 * 1024;
                byte[] buffer = new byte[CHUNK_SIZE];
                long bytesRead;
                long fieldOffset = 0;
                List<uint256> blocks = new List<uint256>();
                using (MemoryStream stream = new MemoryStream())
                {
                    while ((bytesRead = reader.GetBytes(index, fieldOffset, buffer, 0, buffer.Length)) > 0)
                    {
                        stream.Write(buffer, 0, (int)bytesRead);
                        fieldOffset += bytesRead;
                    }

                    stream.Position = 0;
                    var bin = new BinaryFormatter();
                    if (stream.Length > 0)
                    {
                        blocks = (List<uint256>)bin.Deserialize(stream);
                    }
                    bin = null;
                }

                var locator = new BlockLocator()
                {
                    Blocks = blocks
                };

                return locator;
            }
            return new BlockLocator();//retrun empty object
        }

        private int? ExtractNullableInt(SQLiteDataReader reader, int index)
        {
            int? LastBlockSyncedHeight = null;

            if (reader[index].GetType() != typeof(DBNull))
            {
                LastBlockSyncedHeight = reader.GetInt32(index);
            }

            return LastBlockSyncedHeight;
        }

        private void BuildAccountRoot(Wallet wallet, int? lastBlockSyncedHeight, int coinType, uint256 lastBlockHash, SQLiteConnection dbConnection)
        {
            HdAccount hdAccount = null;
            List<HdAccount> accounts = new List<HdAccount>();

            var sql = "SELECT id, HdIndex, Name, HdPath, ExtendedPubKey, CreationTime " +
                " FROM Account WHERE WalletId = $WalletId order by HdIndex asc";
            using (var selectAccountsCmd = new SQLiteCommand(sql, dbConnection))
            {
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
            }

            wallet.AccountsRoot.Add(new AccountRoot()
            {
                LastBlockSyncedHash = lastBlockHash,
                LastBlockSyncedHeight = lastBlockSyncedHeight,
                CoinType = (CoinType)coinType,
                Accounts = accounts
            });

            //in single account scenario logic is simple
            if (accounts.Count == 1)
            {
                hdAccount = accounts[0];
            }

            sql = "SELECT id, WalletId, HdIndex, ScriptPubKey, Pubkey, Address, HdPath " +
                    " FROM Address WHERE WalletId = $WalletId order by HdIndex asc";
            using (var selectAddressesCmd = new SQLiteCommand(sql, dbConnection))
            {
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
                            ScriptPubKey = ExtractScriptFromDb(reader.GetString(3)),
                            Pubkey = ExtractScriptFromDb(reader.GetString(4)),
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
            }

            ReadTransactions(wallet, dbConnection);
        }

        private void ReadTransactions(Wallet wallet, SQLiteConnection dbConnection)
        {
            Dictionary<uint256, List<PaymentDetails>> paymentDetails = null;
            Dictionary<long, SpendingDetails> spendingDetails = null;
            List<TransactionData> transactionData = null;
            Task[] taskArray = new Task[3];

            taskArray[0] = Task.Factory.StartNew(() =>
            {
                paymentDetails = GetPaymentDetailsForThisWallet(wallet, dbConnection);
            });
            taskArray[1] = Task.Factory.StartNew(() =>
            {
                spendingDetails = GetSpendingDetailsForThisWallet(wallet, dbConnection);
            });
            taskArray[2] = Task.Factory.StartNew(() =>
            {
                transactionData = GetTransactionsForThisWallet(wallet, dbConnection);
            });

            Task.WaitAll(taskArray);

            long last_AddressId = 0;
            HdAddress last_hdAddress = null;
            foreach (var transaction in transactionData)
            {
                if (spendingDetails.ContainsKey(transaction.DbId))
                {
                    transaction.SpendingDetails = spendingDetails[transaction.DbId];
                    if (paymentDetails.ContainsKey(transaction.SpendingDetails.TransactionId))
                    {
                        var payment = paymentDetails[transaction.SpendingDetails.TransactionId];
                        transaction.SpendingDetails.Payments = payment;
                    }
                }

                if (transaction.AddressId != last_AddressId)//choose wether to seek for address or add to known one
                {
                    last_hdAddress = AddTransactionToAddressInWallet(wallet, transaction);
                    last_AddressId = transaction.AddressId;
                }
                else
                {
                    if (last_hdAddress != null)
                    {
                        last_hdAddress.Transactions.Add(transaction);
                    }
                }
            }

        }

        private List<TransactionData> GetTransactionsForThisWallet(Wallet wallet, SQLiteConnection dbConnection)
        {
            var data = new List<TransactionData>();
            var sql = "SELECT Id, TxIndex, Hash, Amount, BlockHeight, BlockHash, CreationTime, MerkleProof, ScriptPubKey, Hex, IsPropagated,  AddressId  " +
                " FROM [Transaction] WHERE WalletId = $WalletId ORDER BY BlockHeight, AddressId ASC ";

            using (var selectTrnCommand = new SQLiteCommand(sql, dbConnection))
            {
                selectTrnCommand.Parameters.AddWithValue("$WalletId", wallet.Id);

                using (var reader = selectTrnCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var transaction = new TransactionData()
                        {
                            DbId = reader.GetInt64(0),
                            Index = reader.GetInt32(1),
                            Id = ExtractUint256FromNullableDbField(reader, 2),
                            Amount = new Money(reader.GetInt64(3)),
                            BlockHeight = ExtractNullableInt(reader, 4),
                            BlockHash = ExtractUint256FromNullableDbField(reader, 5),
                            CreationTime = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(6)),
                            MerkleProof = ExtractMerkleTree(reader, 7),
                            ScriptPubKey = ExtractScriptFromDb(reader.GetString(8)),
                            Hex = ExtractStringFromNullableDbField(reader, 9),
                            IsPropagated = (reader.GetInt32(10) == 1),
                            AddressId = reader.GetInt64(11)
                        };
                        data.Add(transaction);
                    }
                }
            }

            return data;
        }

        private PartialMerkleTree ExtractMerkleTree(SQLiteDataReader reader, int index)
        {
            PartialMerkleTree merkle = new PartialMerkleTree();
            string merkleString = ExtractStringFromNullableDbField(reader, index);
            if (!String.IsNullOrEmpty(merkleString))
            {
                var bytes = Encoders.Hex.DecodeData(merkleString);
                merkle.ReadWrite(bytes);
            }
            else
            {
                merkle = null;
            }

            return merkle;
        }

        private Dictionary<uint256, List<PaymentDetails>> GetPaymentDetailsForThisWallet(Wallet wallet, SQLiteConnection dbConnection)
        {
            var paymentDetailsList = new Dictionary<uint256, List<PaymentDetails>>();

            var sql = "SELECT DISTINCT pd.Id,  tsl.SpendingTransactionId, pd.Amount, " +
                      " pd.DestinationAddress, pd.DestinationScriptPubKey , sd.TransactionHash FROM PaymentDetails pd " +
                      " INNER JOIN SpendingDetails sd ON sd.Id = pd.SpendingTransactionId " +
                      " INNER JOIN TransactionSpendingLinks tsl ON sd.id = tsl.SpendingTransactionId and tsl.WalletId = $WalletId " +
                      " WHERE pd.WalletId = $WalletId";
            using (var selectSpendTrnCmd = new SQLiteCommand(sql, dbConnection))
            {
                selectSpendTrnCmd.Parameters.AddWithValue("$WalletId", wallet.Id);

                using (var reader = selectSpendTrnCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        uint256 trnId = ExtractUint256FromNullableDbField(reader, 5);
                        if (!paymentDetailsList.ContainsKey(trnId))
                        {
                            paymentDetailsList.Add(trnId, new List<PaymentDetails>());
                        }

                        var paymentDetail = new PaymentDetails()
                        {
                            DbId = reader.GetInt64(0),
                            DbSpendingTransactionId = reader.GetInt64(1),
                            Amount = Money.Satoshis(reader.GetInt64(2)),
                            DestinationAddress = reader.GetString(3),
                            DestinationScriptPubKey = ExtractScriptFromDb(reader.GetString(4)),
                            TransactionId = trnId
                        };

                        var list = paymentDetailsList[trnId];
                        list.Add(paymentDetail);
                        paymentDetailsList.AddOrReplace(trnId, list);
                    }
                }
            }

            return paymentDetailsList;
        }

        private Dictionary<long, SpendingDetails> GetSpendingDetailsForThisWallet(Wallet wallet, SQLiteConnection dbConnection)
        {
            var spendingDetailsList = new Dictionary<long, SpendingDetails>();

            var sql = "SELECT sp.Id, sp.TransactionHash, sp.BlockHeight, sp.CreationTime, sp.Hex, tr.Hash as ParentTranscationHash, tsl.TransactionId,  tr.AddressId " +
                " FROM SpendingDetails sp " +
                " INNER JOIN TransactionSpendingLinks tsl ON sp.id = tsl.SpendingTransactionId and tsl.WalletId = $WalletId " +
                " INNER JOIN [Transaction] tr ON tr.Id = tsl.TransactionId  WHERE sp.WalletId = $WalletId ORDER BY sp.BlockHeight ASC";

            using (var selectSpendTrnCmd = new SQLiteCommand(sql, dbConnection))
            {
                selectSpendTrnCmd.Parameters.AddWithValue("$WalletId", wallet.Id);

                using (var reader = selectSpendTrnCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        uint256 TransactionId = ExtractUint256FromNullableDbField(reader, 1);
                        uint256 parentTrnHash = ExtractUint256FromNullableDbField(reader, 5);
                        long parentTransactionDbId = reader.GetInt64(6);
                        var spendingDetails = new SpendingDetails()
                        {
                            DbId = reader.GetInt64(0),
                            TransactionId = TransactionId,
                            BlockHeight = ExtractNullableInt(reader, 2),
                            CreationTime = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(3)),
                            Hex = ExtractStringFromNullableDbField(reader, 4),
                            ParentTransactionHash = parentTrnHash,
                            ParentTransactionDbId = parentTransactionDbId,
                            AddressDbId = reader.GetInt64(7)

                        };
                        //if (paymentDetails.ContainsKey(TransactionId))
                        //{
                        //    var payment = paymentDetails[TransactionId];
                        //    spendingDetails.Payments = payment;
                        //}                    
                        spendingDetailsList.AddOrReplace(parentTransactionDbId, spendingDetails);
                    }
                }
            }

            return spendingDetailsList;
        }

        private HdAddress AddTransactionToAddressInWallet(Wallet wallet, TransactionData transaction)
        {
            if (wallet != null && transaction != null)
            {
                var accounts = wallet.AccountsRoot.FirstOrDefault().Accounts;

                List<HdAddress> allAddresses = new List<HdAddress>();
                foreach (HdAccount account in accounts)
                {
                    foreach (var address in account.ExternalAddresses)
                    {
                        if (address.Id == transaction.AddressId)
                        {
                            address.Transactions.Add(transaction);
                            return address;
                        }
                    }
                    foreach (var address in account.InternalAddresses)
                    {
                        if (address.Id == transaction.AddressId)
                        {
                            address.Transactions.Add(transaction);
                            return address;
                        }
                    }
                }
            }
            return null;
        }

        private Script ExtractScriptFromDb(string value)
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

        public void FlushWalletCache(long id)
        {
            string cacheKey = "wallet_" + id;

            if (this.walletCache.ContainsKey(cacheKey))
            {
                var x = this.walletCache[cacheKey];
                string name = x.Name;

                this.walletCache.TryRemove(name, out x);
                this.walletCache.TryRemove(cacheKey, out x);
            }
        }

        public Wallet GetWalletById(long id, bool flushcache = false)
        {
            if (id < 1)
            {
                return null;
            }
            string cacheKey = "wallet_" + id;
            if (flushcache)
            {
                FlushWalletCache(id);
            }
            if (this.walletCache.ContainsKey(cacheKey))
            {
                return this.walletCache[cacheKey];
            }
            //TODO implement wallet specific locking to ensure single thread per wallet id re-entry
            Wallet wallet = null;

            var sql = "SELECT ROWID,Name,EncryptedSeed,ChainCode,Network,CreationTime," +
                    "LastBlockSyncedHash,LastBlockSyncedHeight, CoinType, Blocks FROM Wallet WHERE id = $id";

            wallet = ReadWalletFromDb(wallet, sql, id.ToString());

            if (wallet != null)
            {
                this.walletCache[wallet.Name] = wallet;
                this.walletCache["wallet_" + wallet.Id] = wallet;
            }

            return wallet;
        }

        public long SaveAddress(long walletId, HdAddress address, bool saveTransactionsHere = false)
        {
            using (var dbConnection = new SQLiteConnection(this.connection))
            {
                dbConnection.Open();

                using (var dbTransaction = dbConnection.BeginTransaction())
                {
                    var retval = SaveAddress(walletId, address, dbTransaction, dbConnection, saveTransactionsHere);
                    try
                    {
                        dbTransaction.Commit();
                    }
                    catch
                    {
                        //swallow as transactions can only be commited if there was more than one batch ,  single batch gets commited automaticaly
                    }

                    return retval;
                }
            }
        }

        /// <summary>
        /// Saves address to db making it queryable by address, ScriptPubKey. It relates to account through HD path. Does not commit transaction itself. Caller controlls transaction and must commit.
        /// </summary>
        private long SaveAddress(long walletId, HdAddress address, SQLiteTransaction dbTransaction, SQLiteConnection dbConnection, bool saveTransactionsHere = false)
        {
            if (address.Id < 1)
            {
                var sql = "INSERT INTO Address (WalletId, HdIndex, ScriptPubKey, Pubkey,ScriptPubKeyHash, Address, HdPath ) " +
                        " VALUES ( $WalletId, $Index, $ScriptPubKey, $Pubkey,$ScriptPubKeyHash, $Address, $HdPath )";

                using (var insertCommand = new SQLiteCommand(sql, dbConnection, dbTransaction))
                {
                    insertCommand.Parameters.AddWithValue("$WalletId", walletId);
                    insertCommand.Parameters.AddWithValue("$Index", address.Index);
                    insertCommand.Parameters.AddWithValue("$ScriptPubKey", PackageSriptToString(address.ScriptPubKey));
                    insertCommand.Parameters.AddWithValue("$Pubkey", PackageSriptToString(address.Pubkey));
                    insertCommand.Parameters.AddWithValue("$ScriptPubKeyHash", address.ScriptPubKey.Hash.ToString());
                    insertCommand.Parameters.AddWithValue("$Address", address.Address);
                    insertCommand.Parameters.AddWithValue("$HdPath", address.HdPath);

                    insertCommand.ExecuteNonQuery();
                }

                sql = "select last_insert_rowid();";
                using (var selectCommand = new SQLiteCommand(sql, dbConnection, dbTransaction))
                {
                    address.Id = int.Parse(selectCommand.ExecuteScalar().ToString());
                }
            }

            if (saveTransactionsHere)
            {
                DeleteAddressTransactions(walletId, address, dbTransaction, dbConnection);
                //to save transactions we need to drop all transactions for this address.
                //and then re-add the one's that are left.
                foreach (var chainTran in address.Transactions)
                {
                    chainTran.DbId = SaveTransaction(walletId, address, chainTran, dbTransaction, dbConnection);
                }
            }

            address.WalletId = walletId;
            return address.Id;
        }

        private void DeleteAddressTransactions(long walletId, HdAddress address, SQLiteTransaction dbTransaction, SQLiteConnection dbConnection)
        {
            // we must not delete spending transaction because it could be spending many outputs
            //spending transcations and payment details are unlinked to single addresses

            //var deletPaymentDetailCmd = this.connection.CreateCommand();
            //deletPaymentDetailCmd.Transaction = dbTransaction;
            //deletPaymentDetailCmd.CommandText = "DELETE FROM PaymentDetails pd " +
            //    "INNER JOIN [TransactionSpendingLinks] tsl ON pd.SpendingTransactionId = tsl.SpendingTransactionId AND pd.WalletId = 1 " +
            //    "INNER JOIN [Transaction] trx ON tsl.TransactionId = trx.Id " +
            //    "WHERE trx.AddressId = $AddressId AND trx.WalletId = $WalletId";
            //deletPaymentDetailCmd.Parameters.AddWithValue("$WalletId", walletId);
            //deletPaymentDetailCmd.Parameters.AddWithValue("$AddressId", address.Id);
            //deletPaymentDetailCmd.ExecuteNonQuery();

            var sql = "DELETE FROM TransactionSpendingLinks WHERE TransactionId in ( " +
                      "SELECT DISTINCT Id FROM [Transaction] " +
                      "WHERE AddressId = $AddressId AND WalletId = $WalletId )";
            using (var deletPaymentDetailCmd = new SQLiteCommand(sql, dbConnection, dbTransaction))
            {
                deletPaymentDetailCmd.Parameters.AddWithValue("$WalletId", walletId);
                deletPaymentDetailCmd.Parameters.AddWithValue("$AddressId", address.Id);
                deletPaymentDetailCmd.ExecuteNonQuery();
            }

            sql = "DELETE FROM [Transaction] " +
                  "WHERE AddressId = $AddressId AND WalletId = $WalletId";

            using (var deleteTransactionCmd = new SQLiteCommand(sql, dbConnection, dbTransaction))
            {
                deleteTransactionCmd.Parameters.AddWithValue("$WalletId", walletId);
                deleteTransactionCmd.Parameters.AddWithValue("$AddressId", address.Id);
                deleteTransactionCmd.ExecuteNonQuery();
            }
        }

        private long SaveAccount(long walletId, HdAccount account, SQLiteTransaction dbTransaction, SQLiteConnection dbConnection)
        {
            if (account.Id < 1)
            {
                var sql = "INSERT INTO Account (WalletId, HdIndex, Name, HdPath, ExtendedPubKey, CreationTime ) " +
                    " VALUES ( $WalletId, $Index, $Name, $HdPath, $ExtendedPubKey, $CreationTime )";
                using (var insertCommand = new SQLiteCommand(sql, dbConnection, dbTransaction))
                {
                    insertCommand.Parameters.AddWithValue("$WalletId", walletId);
                    insertCommand.Parameters.AddWithValue("$Index", account.Index);
                    insertCommand.Parameters.AddWithValue("$Name", account.Name);
                    insertCommand.Parameters.AddWithValue("$HdPath", account.HdPath);
                    insertCommand.Parameters.AddWithValue("$ExtendedPubKey", account.ExtendedPubKey);
                    insertCommand.Parameters.AddWithValue("$CreationTime", account.CreationTime.ToUnixTimeSeconds());

                    insertCommand.ExecuteNonQuery();
                }

                sql = "select last_insert_rowid();";
                using (var selectCommand = new SQLiteCommand(sql, dbConnection, dbTransaction))
                {
                    account.Id = int.Parse(selectCommand.ExecuteScalar().ToString());
                }
            }
            return account.Id;
        }

        public long SaveTransaction(long walletId, HdAddress address, TransactionData trx)
        {
            if (walletId < 1)
            {
                walletId = GetWalletIdByAddress(address.Address);
            }
            if (walletId < 1)
            {
                throw new Exception("Atempting to add transaction to wallet that was not saved.");
            }

            return -1;
            //using (var dbConnection = new SQLiteConnection(this.connection))
            //{
            //    dbConnection.Open();

            //    using (var dbTransaction = dbConnection.BeginTransaction())
            //    {
            //        long retval = SaveTransaction(walletId, address, trx, dbTransaction, dbConnection);
            //        dbTransaction.Commit();
            //        return retval;
            //    }
            //}
        }

        private long SaveTransaction(long walletId, HdAddress address, TransactionData trx, SQLiteTransaction dbTransaction, SQLiteConnection dbConnection)
        {
            //one transaction can be saved in multiple addresses. 
            if (trx == null)
            {
                return 0;
            }

            trx.DbId = GetTransactionDbId(walletId, trx, address.Id, dbTransaction, dbConnection);

            var sql = string.Empty;
            if (trx.DbId < 1)
            {
                sql = "INSERT INTO [Transaction]  (WalletId, AddressId, TxIndex, Hash, Amount, BlockHeight, BlockHash, CreationTime, MerkleProof, ScriptPubKey , Hex, IsPropagated, IsSpent) " +
                          " VALUES ( $WalletId, $AddressId, $TxIndex, $Hash, $Amount, $BlockHeight, $BlockHash, $CreationTime, $MerkleProof, $ScriptPubKey , $Hex, $IsPropagated, $IsSpent )";

                using (var insertCommand = new SQLiteCommand(sql, dbConnection, dbTransaction))
                {
                    insertCommand.Parameters.AddWithValue("$WalletId", walletId);
                    insertCommand.Parameters.AddWithValue("$AddressId", address.Id);
                    insertCommand.Parameters.AddWithValue("$TxIndex", trx.Index);
                    insertCommand.Parameters.AddWithValue("$Hash", trx.Id);
                    insertCommand.Parameters.AddWithValue("$Amount", trx.Amount.Satoshi);
                    insertCommand.Parameters.AddWithValue("$BlockHeight", trx.BlockHeight);
                    insertCommand.Parameters.AddWithValue("$BlockHash", trx.BlockHash);
                    insertCommand.Parameters.AddWithValue("$CreationTime", trx.CreationTime.ToUnixTimeSeconds());
                    insertCommand.Parameters.AddWithValue("$MerkleProof", (trx.MerkleProof != null) ? PackPartialMerkleTree(trx.MerkleProof) : null);
                    insertCommand.Parameters.AddWithValue("$ScriptPubKey", PackageSriptToString(trx.ScriptPubKey));
                    insertCommand.Parameters.AddWithValue("$Hex", trx.Hex);
                    insertCommand.Parameters.AddWithValue("$IsPropagated", trx.IsPropagated);
                    insertCommand.Parameters.AddWithValue("$IsSpent", (trx.SpendingDetails != null) ? true : false);

                    insertCommand.ExecuteNonQuery();
                }

                sql = "select last_insert_rowid();";
                using (var selectCommand = new SQLiteCommand(sql, dbConnection, dbTransaction))
                {
                    trx.DbId = int.Parse(selectCommand.ExecuteScalar().ToString());
                }
            }
            else
            {
                sql = "UPDATE [Transaction]  set BlockHeight = $BlockHeight , BlockHash = $BlockHash, IsPropagated = $IsPropagated, IsSpent= $IsSpent  WHERE WalletId = $WalletId AND Hash = $Hash AND AddressId = $AddressId";

                using (var updateTrxCommand = new SQLiteCommand(sql, dbConnection, dbTransaction))
                {
                    updateTrxCommand.Parameters.AddWithValue("$WalletId", walletId);
                    updateTrxCommand.Parameters.AddWithValue("$AddressId", address.Id);
                    updateTrxCommand.Parameters.AddWithValue("$Hash", trx.Id);
                    updateTrxCommand.Parameters.AddWithValue("$BlockHeight", trx.BlockHeight);
                    updateTrxCommand.Parameters.AddWithValue("$BlockHash", trx.BlockHash);
                    updateTrxCommand.Parameters.AddWithValue("$IsPropagated", trx.IsPropagated);
                    updateTrxCommand.Parameters.AddWithValue("$IsSpent", (trx.SpendingDetails != null) ? true : false);
                    updateTrxCommand.ExecuteNonQuery();
                }
            }

            if (trx.SpendingDetails != null)
            {
                trx.SpendingDetails.DbId = GetTransactionSpendingDbId(walletId, trx, dbTransaction, dbConnection);
                var spendTrx = trx.SpendingDetails;
                if (spendTrx.DbId < 1)
                {
                    sql = "INSERT INTO SpendingDetails  (WalletId, TransactionHash, BlockHeight, CreationTime, Hex ) " +
                    " VALUES ( $WalletId,  $TransactionHash, $BlockHeight, $CreationTime, $Hex ) ";
                    using (var insertSpendCommand = new SQLiteCommand(sql, dbConnection, dbTransaction))
                    {
                        insertSpendCommand.Parameters.AddWithValue("$WalletId", walletId);
                        insertSpendCommand.Parameters.AddWithValue("$TransactionHash", spendTrx.TransactionId);
                        insertSpendCommand.Parameters.AddWithValue("$BlockHeight", spendTrx.BlockHeight);
                        insertSpendCommand.Parameters.AddWithValue("$CreationTime", spendTrx.CreationTime.ToUnixTimeSeconds());
                        insertSpendCommand.Parameters.AddWithValue("$Hex", spendTrx.Hex);

                        insertSpendCommand.ExecuteNonQuery();
                    }

                    sql = "select last_insert_rowid();";
                    using (var selectCommand = new SQLiteCommand(sql, dbConnection, dbTransaction))
                    {
                        spendTrx.DbId = int.Parse(selectCommand.ExecuteScalar().ToString());
                    }

                    //var deletPaymentDetailCmd = this.connection.CreateCommand();
                    //deletPaymentDetailCmd.Transaction = dbTransaction;
                    //deletPaymentDetailCmd.CommandText = "DELETE FROM PaymentDetails  WHERE SpendingTransactionId = $SpendingTransactionId AND WalletId = $WalletId";
                    //deletPaymentDetailCmd.Parameters.AddWithValue("$WalletId", walletId);
                    //deletPaymentDetailCmd.Parameters.AddWithValue("$SpendingTransactionId", spendTrx.DbId);
                    //deletPaymentDetailCmd.ExecuteNonQuery();

                    foreach (var item in trx.SpendingDetails.Payments)
                    {
                        sql = "INSERT INTO PaymentDetails  (WalletId, SpendingTransactionId, Amount, DestinationAddress, DestinationScriptPubKey) " +
                        " VALUES ( $WalletId,  $SpendingTransactionId, $Amount, $DestinationAddress, $DestinationScriptPubKey) ";
                        using (var insertPaymentDetailCommand = new SQLiteCommand(sql, dbConnection, dbTransaction))
                        {
                            insertPaymentDetailCommand.Parameters.AddWithValue("$WalletId", walletId);
                            insertPaymentDetailCommand.Parameters.AddWithValue("$SpendingTransactionId", spendTrx.DbId);
                            insertPaymentDetailCommand.Parameters.AddWithValue("$Amount", item.Amount.Satoshi);
                            insertPaymentDetailCommand.Parameters.AddWithValue("$DestinationAddress", item.DestinationAddress);
                            insertPaymentDetailCommand.Parameters.AddWithValue("$DestinationScriptPubKey", PackageSriptToString(item.DestinationScriptPubKey));

                            insertPaymentDetailCommand.ExecuteNonQuery();
                        }
                    }
                }
                else
                {
                    sql = "UPDATE SpendingDetails  set BlockHeight = $BlockHeight WHERE WalletId = $WalletId AND TransactionHash = $TransactionHash ";
                    using (var updateTrxCommand = new SQLiteCommand(sql, dbConnection, dbTransaction))
                    {
                        updateTrxCommand.Parameters.AddWithValue("$WalletId", walletId);
                        updateTrxCommand.Parameters.AddWithValue("$TransactionHash", spendTrx.TransactionId);
                        updateTrxCommand.Parameters.AddWithValue("$BlockHeight", spendTrx.BlockHeight);

                        updateTrxCommand.ExecuteNonQuery();
                    }
                }

                sql = "INSERT OR REPLACE INTO TransactionSpendingLinks (WalletId, TransactionId,SpendingTransactionId) " +
                " VALUES ( $WalletId, $TransactionId, $SpendingTransactionId )";
                using (var insertCommand = new SQLiteCommand(sql, dbConnection, dbTransaction))
                {
                    insertCommand.Parameters.AddWithValue("$WalletId", walletId);
                    insertCommand.Parameters.AddWithValue("$TransactionId", trx.DbId);
                    insertCommand.Parameters.AddWithValue("$SpendingTransactionId", spendTrx.DbId);

                    insertCommand.ExecuteNonQuery();
                }
            }

            FlushWalletCache(walletId);
            //rebuild cache async
            //Task task = Task.Run(() =>
            //    {
            //        GetWalletById(walletId);
            //    }
            //);
            
            return trx.DbId;
        }

        private string PackPartialMerkleTree(PartialMerkleTree value)
        {
            if (value == null)
            {
                return null;
            }
            return Encoders.Hex.EncodeData(value.ToBytes());
        }

        private long GetTransactionSpendingDbId(long walletId, TransactionData trx, SQLiteTransaction dbTransaction, SQLiteConnection dbConnection)
        {
            var sql = "SELECT Id FROM SpendingDetails WHERE WalletId = $WalletId  AND TransactionHash = $TransactionHash"; //
            using (var selectCmd = new SQLiteCommand(sql, dbConnection, dbTransaction))
            {
                selectCmd.Parameters.AddWithValue("$TransactionHash", trx.SpendingDetails.TransactionId);
                selectCmd.Parameters.AddWithValue("$WalletId", walletId);
                using (var reader = selectCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        trx.SpendingDetails.DbId = reader.GetInt64(0);
                    }
                }
            }

            return trx.SpendingDetails.DbId;
        }

        private long GetTransactionDbId(long walletId, TransactionData trx, long AddressId, SQLiteTransaction dbTransaction, SQLiteConnection dbConnection)
        {
            var sql = "SELECT id  FROM [Transaction] WHERE Hash = $Hash AND WalletId = $WalletId AND AddressId = $AddressId";

            using (var selectCmd = new SQLiteCommand(sql, dbConnection, dbTransaction))
            {
                selectCmd.Parameters.AddWithValue("$Hash", trx.Id);
                selectCmd.Parameters.AddWithValue("$WalletId", walletId);
                selectCmd.Parameters.AddWithValue("$AddressId", AddressId);
                using (var reader = selectCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        trx.DbId = reader.GetInt64(0);
                    }
                }
            }

            return trx.DbId;
        }

        private string PackageSriptToString(Script value)
        {
            if (value == null)
            {
                return null;
            }
            return Encoders.Hex.EncodeData(((Script)value).ToBytes(false));
        }

        public void SaveLastSyncedBlock(string walletName, ChainedHeader chainedHeader)
        {
            Guard.NotNull(walletName, nameof(walletName));

            using (var dbConnection = new SQLiteConnection(this.connection))
            {
                dbConnection.Open();

                var sql = "UPDATE Wallet set LastBlockSyncedHash = $LastBlockSyncedHash, LastBlockSyncedHeight = $LastBlockSyncedHeight,  LastUpdated = $LastUpdated, Blocks = $Blocks WHERE Name = $Name";
                using (var insertCommand = new SQLiteCommand(sql, dbConnection))
                {
                    insertCommand.Parameters.AddWithValue("$Name", walletName);
                    insertCommand.Parameters.AddWithValue("$LastBlockSyncedHash", chainedHeader.HashBlock);
                    insertCommand.Parameters.AddWithValue("$LastBlockSyncedHeight", chainedHeader.Height);
                    insertCommand.Parameters.AddWithValue("$LastUpdated", DateTimeOffset.Now.ToUnixTimeSeconds());

                    byte[] bytes = GetBlockLocatorBytes(chainedHeader.GetLocator().Blocks);
                    SQLiteParameter prm = new SQLiteParameter("$Blocks", DbType.Binary, bytes.Length, ParameterDirection.Input, false, 0, 0, null, DataRowVersion.Current, bytes);
                    insertCommand.Parameters.Add(prm);
                    insertCommand.ExecuteNonQuery();
                }

                UpdateLastSychedInMemory(walletName, chainedHeader);
            }
        }

        public void SaveLastSyncedBlock(ChainedHeader chainedHeader)
        {
            using (var dbConnection = new SQLiteConnection(this.connection))
            {
                dbConnection.Open();

                //global batch update
                var sql = "UPDATE Wallet set LastBlockSyncedHash = $LastBlockSyncedHash, LastBlockSyncedHeight = $LastBlockSyncedHeight,  LastUpdated = $LastUpdated , Blocks = $Blocks";

                using (var insertCommand = new SQLiteCommand(sql, dbConnection))
                {
                    insertCommand.Parameters.AddWithValue("$LastBlockSyncedHash", chainedHeader.HashBlock);
                    insertCommand.Parameters.AddWithValue("$LastBlockSyncedHeight", chainedHeader.Height);
                    insertCommand.Parameters.AddWithValue("$LastUpdated", DateTimeOffset.Now.ToUnixTimeSeconds());

                    byte[] bytes = GetBlockLocatorBytes(chainedHeader.GetLocator().Blocks);
                    SQLiteParameter prm = new SQLiteParameter("$Blocks", DbType.Binary, bytes.Length, ParameterDirection.Input, false, 0, 0, null, DataRowVersion.Current, bytes);
                    insertCommand.Parameters.Add(prm);

                    insertCommand.ExecuteNonQuery();
                }

                foreach (var walletName in this.walletCache.Keys)
                {
                    UpdateLastSychedInMemory(walletName, chainedHeader);
                }
            }
        }

        private void UpdateLastSychedInMemory(string walletName, ChainedHeader chainedHeader)
        {
            var acct_cache = GetWalletByName(walletName)?.AccountsRoot?.FirstOrDefault();
            if (acct_cache != null)
            {
                acct_cache.LastBlockSyncedHash = chainedHeader.HashBlock;
                acct_cache.LastBlockSyncedHeight = chainedHeader.Height;
            }
            var wallet = GetWalletByName(walletName);
            if (wallet != null)
            {
                wallet.BlockLocator = chainedHeader.GetLocator().Blocks;
            }
        }

        public WalletSyncPosition GetLastSyncedBlock()
        {
            WalletSyncPosition syncPosition = null;

            using (var dbConnection = new SQLiteConnection(this.connection))
            {
                dbConnection.Open();

                var sql = "SELECT LastBlockSyncedHeight, LastBlockSyncedHash FROM Wallet ORDER BY LastUpdated desc LIMIT 1";

                using (var selectWalletCmd = new SQLiteCommand(sql, dbConnection))
                {
                    using (var reader = selectWalletCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int? LastBlockSyncedHeight = ExtractNullableInt(reader, 0);

                            uint256 BlockHashLocal = ExtractUint256FromNullableDbField(reader, 1);
                            if (LastBlockSyncedHeight.HasValue)
                            {
                                syncPosition = new WalletSyncPosition()
                                {
                                    Height = LastBlockSyncedHeight.Value,
                                    BlockHash = BlockHashLocal
                                };
                            }
                        }
                    }
                }
            }

            return syncPosition;
        }

        private uint256 ExtractUint256FromNullableDbField(SQLiteDataReader reader, int index)
        {
            uint256 BlockHashLocal = null;
            string LastBlockSyncedHash = reader[index] as string;

            if (!string.IsNullOrEmpty(LastBlockSyncedHash))
            {
                BlockHashLocal = uint256.Parse(LastBlockSyncedHash);
            }

            return BlockHashLocal;
        }

        private string ExtractStringFromNullableDbField(SQLiteDataReader reader, int index)
        {
            return reader[index] as string;
        }

        private WalletSyncPosition GetLastSyncedBlock(string walletName)
        {
            WalletSyncPosition syncPosition = null;

            using (var dbConnection = new SQLiteConnection(this.connection))
            {
                dbConnection.Open();

                var sql = "SELECT LastBlockSyncedHeight , LastBlockSyncedHash FROM Wallet WHERE walletName = $Name ";
                using (var selectWalletCmd = new SQLiteCommand(sql, dbConnection))
                {
                   selectWalletCmd.Parameters.AddWithValue("$Name", walletName);

                    using (var reader = selectWalletCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int? LastBlockSyncedHeight = ExtractNullableInt(reader, 0);

                            uint256 BlockHashLocal = ExtractUint256FromNullableDbField(reader, 1);
                            if (LastBlockSyncedHeight.HasValue)
                            {
                                syncPosition = new WalletSyncPosition()
                                {
                                    Height = LastBlockSyncedHeight.Value,
                                    BlockHash = BlockHashLocal
                                };
                            }
                        }
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

        public List<string> GetAllWalletNames() //TODO: implement caching
        {
            var result = new List<string>();

            using (var dbConnection = new SQLiteConnection(this.connection))
            {
                dbConnection.Open();

                var sql = "SELECT Name FROM Wallet";
                using (var selectWalletCmd = new SQLiteCommand(sql, dbConnection))
                {
                    using (var reader = selectWalletCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Add(reader.GetString(0));
                        }
                    }
                }
            }

            return result;
        }

        public List<WalletPointer> GetAllWalletPointers()
        {
            var result = new List<WalletPointer>();

            using (var dbConnection = new SQLiteConnection(this.connection))
            {
                dbConnection.Open();

                var sql = "SELECT id,Name FROM Wallet";
                using (var selectWalletCmd = new SQLiteCommand(sql, dbConnection))
                {
                    using (var reader = selectWalletCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Add(new WalletPointer(reader.GetInt32(0), reader.GetString(1)));
                        }
                    }
                }
            }

            return result;
        }


        public ICollection<uint256> GetFirstWalletBlockLocator()
        {
            var locator = new BlockLocator();

            using (var dbConnection = new SQLiteConnection(this.connection))
            {
                dbConnection.Open();

                var sql = "SELECT RowId, Blocks FROM Wallet ORDER BY CreationTime DESC LIMIT 1";
                using (var selectWalletCmd = new SQLiteCommand(sql, dbConnection))
                {
                    using (var reader = selectWalletCmd.ExecuteReader(CommandBehavior.KeyInfo))
                    {
                        while (reader.Read())
                        {
                            locator = ExtractBlockLocator(reader, 1);
                        }
                    }
                }
            }

            return locator.Blocks;
        }

        public BlockLocator GetWalletBlockLocator(long walletId)
        {
            var locator = new BlockLocator();

            using (var dbConnection = new SQLiteConnection(this.connection))
            {
                dbConnection.Open();

                var sql = "SELECT RowId, Blocks FROM Wallet WHERE WalletId = $WalletId";
                using (var selectLocator = new SQLiteCommand(sql, dbConnection))
                {
                    selectLocator.Parameters.AddWithValue("$WalletId", walletId);
                    using (var reader = selectLocator.ExecuteReader(CommandBehavior.KeyInfo))
                    {
                        while (reader.Read())
                        {
                            locator = ExtractBlockLocator(reader, 1);
                        }
                    }
                }
            }

            return locator;
        }

        internal int? GetEarliestWalletHeight()
        {
            int? result = null;

            using (var dbConnection = new SQLiteConnection(this.connection))
            {
                dbConnection.Open();

                var sql = "SELECT LastBlockSyncedHeight FROM Wallet order by CreationTime DESC LIMIT 1";
                using (var selectWalletCmd = new SQLiteCommand(sql, dbConnection))
                {
                    using (var reader = selectWalletCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result = ExtractNullableInt(reader, 0);
                        }
                    }
                }
            }

            return result;
        }

        internal DateTimeOffset GetOldestWalletCreationTime()
        {
            var result = DateTimeOffset.MinValue;

            using (var dbConnection = new SQLiteConnection(this.connection))
            {
                dbConnection.Open();

                var sql = "SELECT CreationTime FROM Wallet order by CreationTime ASC LIMIT 1";
                using (var selectWalletCmd = new SQLiteCommand(sql, dbConnection))
                {
                    using (var reader = selectWalletCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(0));
                        }
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

            using (var dbConnection = new SQLiteConnection(this.connection))
            {
                dbConnection.Open();

                var sql = "SELECT WalletId FROM Address WHERE Address = $Address";
                using (var selectAccountsCmd = new SQLiteCommand(sql, dbConnection))
                {
                    selectAccountsCmd.Parameters.AddWithValue("$Address", address);
                    using (var reader = selectAccountsCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            walletId = reader.GetInt64(0);
                        }
                    }
                }
            }

            wallet = GetWalletById(walletId);

            return wallet;
        }

        internal long GetWalletIdByAddress(string address)
        {
            long walletId = 0;

            using (var dbConnection = new SQLiteConnection(this.connection))
            {
                dbConnection.Open();

                var sql = "SELECT WalletId FROM Address WHERE Address = $Address";
                using (var selectAccountsCmd = new SQLiteCommand(sql, dbConnection))
                {
                    selectAccountsCmd.Parameters.AddWithValue("$Address", address);
                    using (var reader = selectAccountsCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            walletId = reader.GetInt64(0);
                        }
                    }
                }
            }

            return walletId;
        }

        internal Wallet GetWalletByScriptHash(string ScriptPubKeyHash)
        {
            Wallet wallet = null;
            long walletId = 0;

            using (var dbConnection = new SQLiteConnection(this.connection))
            {
                dbConnection.Open();

                var sql = "SELECT WalletId FROM Address WHERE ScriptPubKeyHash = $ScriptPubKeyHash";
                using (var selectAccountsCmd = new SQLiteCommand(sql, dbConnection))
                {
                    selectAccountsCmd.Parameters.AddWithValue("$ScriptPubKeyHash", ScriptPubKeyHash);

                    using (var reader = selectAccountsCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            walletId = reader.GetInt64(0);
                        }
                    }
                }
            }

            wallet = GetWalletById(walletId);

            return wallet;
        }


        internal void RemoveTransactionFromHdAddress(HdAddress hdAddress, uint256 id)
        {
            hdAddress.Transactions.Remove(hdAddress.Transactions.Single(t => t.Id == id));
            this.SaveAddress(hdAddress.WalletId, hdAddress, true);
        }

        internal void RemoveTransactionSpendingDetailsFromHdAddress(HdAddress hdAddress, uint256 id)
        {
            hdAddress.Transactions.Remove(hdAddress.Transactions.Single(t => t.SpendingDetails.TransactionId == id));
            this.SaveAddress(hdAddress.WalletId, hdAddress,true);
        }

        internal uint256 GetLastUpdatedBlockHash()
        {
            uint256 result = null;

            using (var dbConnection = new SQLiteConnection(this.connection))
            {
                dbConnection.Open();

                var sql = "SELECT LastBlockSyncedHash FROM Wallet order by LastUpdated DESC, LastBlockSyncedHeight DESC LIMIT 1";
                using (var selectWalletCmd = new SQLiteCommand(sql, dbConnection))
                {
                    using (var reader = selectWalletCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result = ExtractUint256FromNullableDbField(reader, 0);
                        }
                    }
                }
            }

            return result;
        }

        internal string GetLastUpdatedWalletName()
        {
            string result = null;

            using (var dbConnection = new SQLiteConnection(this.connection))
            {
                dbConnection.Open();

                var sql = "SELECT Name FROM Wallet order by LastUpdated DESC LIMIT 1";
                using (var selectWalletCmd = new SQLiteCommand(sql, dbConnection))
                {
                    using (var reader = selectWalletCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result = reader.GetString(0);
                        }
                    }
                }
            }

            return result;
        }


        public bool HasWallets()
        {
            return !string.IsNullOrEmpty(GetLastUpdatedWalletName());
        }

        public void RemoveWallet(string walletName)
        {
            long walletId = 0;
            using (var dbConnection = new SQLiteConnection(this.connection))
            {
                dbConnection.Open();

                using (var dbTransaction = dbConnection.BeginTransaction())
                {
                    var sql = "SELECT id FROM Wallet WHERE Name = $Name";
                    using (var selectCommand = new SQLiteCommand(sql, dbConnection, dbTransaction))
                    {
                        selectCommand.Parameters.AddWithValue("$Name", walletName);
                        using (var reader = selectCommand.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                walletId = reader.GetInt32(0);
                            }
                        }
                    }

                    sql = "DELETE  FROM Account WHERE WalletId = $id";
                    using (var deleteAccountCmd = new SQLiteCommand(sql, dbConnection, dbTransaction))
                    {
                        deleteAccountCmd.Parameters.AddWithValue("$id", walletId);
                        deleteAccountCmd.ExecuteNonQuery();
                    }

                    sql = "DELETE FROM Address WHERE WalletId = $id";
                    using (var deleteAddressCmd = new SQLiteCommand(sql, dbConnection, dbTransaction))
                    {
                        deleteAddressCmd.Parameters.AddWithValue("$id", walletId);
                        deleteAddressCmd.ExecuteNonQuery();
                    }

                    sql = "DELETE FROM PaymentDetails WHERE WalletId = $id";
                    using (var deletePaymentDetailsCmd = new SQLiteCommand(sql, dbConnection, dbTransaction))
                    {
                        deletePaymentDetailsCmd.Parameters.AddWithValue("$id", walletId);
                        deletePaymentDetailsCmd.ExecuteNonQuery();
                    }

                    sql = "DELETE FROM SpendingDetails WHERE WalletId = $id";
                    using (var deleteSpendingDetailsCmd = new SQLiteCommand(sql, dbConnection, dbTransaction))
                    {
                        deleteSpendingDetailsCmd.Parameters.AddWithValue("$id", walletId);
                        deleteSpendingDetailsCmd.ExecuteNonQuery();
                    }

                    sql = "DELETE  FROM [Transaction] WHERE WalletId = $id";
                    using (var deleteTransactionCmd = new SQLiteCommand(sql, dbConnection, dbTransaction))
                    {
                        deleteTransactionCmd.Parameters.AddWithValue("$id", walletId);
                        deleteTransactionCmd.ExecuteNonQuery();
                    }

                    sql = "DELETE FROM TransactionSpendingLinks WHERE WalletId = $id";
                    using (var deleteTransactionSpendingLinksCmd = new SQLiteCommand(sql, dbConnection, dbTransaction))
                    {
                        deleteTransactionSpendingLinksCmd.Parameters.AddWithValue("$id", walletId);
                        deleteTransactionSpendingLinksCmd.ExecuteNonQuery();
                    }

                    sql = "DELETE FROM Wallet WHERE Name = $Name";
                    using (var deleteWalletCmd = new SQLiteCommand(sql, dbConnection, dbTransaction))
                    {
                        deleteWalletCmd.Parameters.AddWithValue("$Name", walletName);
                        deleteWalletCmd.ExecuteNonQuery();
                    }
                    dbTransaction.Commit();
                }
            }

            FlushWalletCache(walletId);
        }
    }
}