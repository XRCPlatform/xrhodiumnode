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

namespace BRhodium.Bitcoin.Features.Wallet
{
    public class WalletRepository
    {
        private readonly string walletPath;
        private readonly CoinType coinType;

        private readonly Network network;
        private ConcurrentDictionary<string, Wallet> walletCache = new ConcurrentDictionary<string, Wallet>();
        protected readonly SQLiteConnection connection;

        public WalletRepository(string walletPath, CoinType coinType, Network network = null)
        {
            this.coinType = coinType;
            this.walletPath = walletPath;
            this.network = network;
            EnsureSQLiteDbExists();
            this.connection = new SQLiteConnection(new SQLiteConnectionStringBuilder
            {
                DataSource = $"{walletPath}\\Wallet.db"
            }
            .ToString());
            this.connection.Open();
        }

        private void EnsureSQLiteDbExists()
        {
            string filePath = $"{walletPath}\\Wallet.db";
            FileInfo fi = new FileInfo(filePath);
            if (!fi.Exists)
            {
                //not sure if this is best way to find working directory
                var current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                try
                {
                    if (!Directory.Exists(fi.DirectoryName))
                    {
                        Directory.CreateDirectory(fi.DirectoryName);
                    }
                    current.GetDirectories(@"Db").FirstOrDefault().GetFiles("Wallet.db").FirstOrDefault().CopyTo(filePath,true);
                    //current.Parent.GetDirectories(@"\Db").FirstOrDefault().GetFiles("Wallet.db").FirstOrDefault().CopyTo(filePath);
                }
                catch (DirectoryNotFoundException)
                {
                    current.Parent.Parent.Parent.Parent.GetDirectories(@"BRhodium.Bitcoin.Features.Wallet\Db").FirstOrDefault().GetFiles("Wallet.db").FirstOrDefault().CopyTo(filePath);
                }
            }
        }

        public void SaveWallet(string walletName, Wallet wallet, bool saveTransactionsHere = false)
        {
            Guard.NotNull(walletName, nameof(walletName));
            Guard.NotNull(wallet, nameof(wallet));

            Wallet placeholder;

            FlushWalletCache(wallet.Id);

            //Task task = Task.Run(() =>
            // {
            using (var dbTransaction = this.connection.BeginTransaction())
            {
                bool newEntity = false;
                if (wallet.Id < 1)
                {
                    var insertCommand = connection.CreateCommand();
                    insertCommand.Transaction = dbTransaction;
                    insertCommand.CommandText = "INSERT INTO Wallet ( Name, EncryptedSeed, ChainCode, Network, CreationTime, LastBlockSyncedHash, LastBlockSyncedHeight, CoinType, LastUpdated, Blocks) " +
                    "VALUES ( $Name, $EncryptedSeed, $ChainCode, $Network, $CreationTime, $LastBlockSyncedHash, $LastBlockSyncedHeight, $CoinType, $LastUpdated, $Blocks)";

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

                if (wallet.AccountsRoot.FirstOrDefault<AccountRoot>() != null)
                {
                    foreach (var item in wallet.AccountsRoot.FirstOrDefault<AccountRoot>().Accounts)
                    {
                        SaveAccount(wallet.Id, dbTransaction, item);
                    }


                    foreach (var account in wallet.GetAccountsByCoinType(this.coinType))
                    {
                        foreach (var address in account.ExternalAddresses)
                        {
                            SaveAddress(wallet.Id, dbTransaction, address, saveTransactionsHere);
                        }
                        foreach (var address in account.InternalAddresses)
                        {
                            SaveAddress(wallet.Id, dbTransaction, address, saveTransactionsHere);
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
            //});
            //return task;
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
            if (walletCache.ContainsKey(name) && !flushcache)
            {
                return walletCache[name];
            }
            //System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            //sw.Start();

            Wallet wallet = null;

            var selectWalletCmd = connection.CreateCommand();
            selectWalletCmd.CommandText = "SELECT ROWID,Name,EncryptedSeed,ChainCode,Network,CreationTime," +
                "LastBlockSyncedHash,LastBlockSyncedHeight, CoinType, Blocks FROM Wallet WHERE Name = $Name";
            selectWalletCmd.Parameters.AddWithValue("$Name", name);

            wallet = ReadWalletFromDb(wallet, selectWalletCmd);

            //sw.Stop();
            //Console.WriteLine($"Elapsed time {sw.ElapsedMilliseconds}");

            if (wallet != null)
            {
                walletCache.TryAdd(wallet.Name, wallet);
                walletCache.TryAdd("wallet_" + wallet.Id, wallet);
            }

            return wallet;
        }

        private Wallet ReadWalletFromDb(Wallet wallet, SQLiteCommand selectWalletCmd)
        {
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
                    if (!String.IsNullOrEmpty(blockHash))
                    {
                        lastBlockHash = new uint256(blockHash);
                    }

                    BlockLocator locator = ExtractBlockLocator(reader, 9);
                    wallet.BlockLocator = locator?.Blocks;

                    BuildAccountRoot(wallet, LastBlockSyncedHeight, coinType, lastBlockHash);
                }
            }

            return wallet;
        }

        private BlockLocator ExtractBlockLocator(SQLiteDataReader reader, int index)
        {
            if (reader[index].GetType() != typeof(DBNull))
            {
                byte[] bytes = new byte[reader.GetBytes(index, 0, null, 0, int.MaxValue)];
                var blob = reader.GetBlob(index, true);
                blob.Read(bytes, bytes.Length, 0);
                List<uint256> blocks = new List<uint256>();
                if (bytes.Length > 0)
                {
                    using (MemoryStream stream = new MemoryStream(bytes))
                    {
                        BinaryFormatter bin = new BinaryFormatter();
                        blocks = (List<uint256>)bin.Deserialize(stream);
                    }
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

        private void BuildAccountRoot(Wallet wallet, int? lastBlockSyncedHeight, int coinType, uint256 lastBlockHash)
        {
            HdAccount hdAccount = null;
            List<HdAccount> accounts = new List<HdAccount>();
            var selectAccountsCmd = connection.CreateCommand();
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
                LastBlockSyncedHeight = lastBlockSyncedHeight,
                CoinType = (CoinType)coinType,
                Accounts = accounts
            });

            //in single account scenario logic is simple
            if (accounts.Count == 1)
            {
                hdAccount = accounts[0];
            }

            var selectAddressesCmd = connection.CreateCommand();
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
            ReadTransactions(wallet);
        }

        private void ReadTransactions(Wallet wallet)
        {
            Dictionary<uint256, List<PaymentDetails>> paymentDetails = null;
            Dictionary<long, SpendingDetails> spendingDetails = null;
            List<TransactionData> transactionData = null;
            Task[] taskArray = new Task[3];
            taskArray[0] = Task.Factory.StartNew(() =>
            {
                paymentDetails = GetPaymentDetailsForThisWallet(wallet);
            });
            taskArray[1] = Task.Factory.StartNew(() =>
            {
                spendingDetails = GetSpendingDetailsForThisWallet(wallet);
            });
            taskArray[2] = Task.Factory.StartNew(() =>
            {
                transactionData = GetTransactionsForThisWallet(wallet);
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

        private List<TransactionData> GetTransactionsForThisWallet(Wallet wallet)
        {
            List<TransactionData> data = new List<TransactionData>();
            var selectTrnCommand = connection.CreateCommand();
            selectTrnCommand.CommandText = "SELECT Id, TxIndex, Hash, Amount, BlockHeight, BlockHash, CreationTime, MerkleProof, ScriptPubKey, Hex, IsPropagated,  AddressId  " +
                " FROM [Transaction] WHERE WalletId = $WalletId ORDER BY BlockHeight, AddressId ASC ";
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

        private Dictionary<uint256, List<PaymentDetails>> GetPaymentDetailsForThisWallet(Wallet wallet)
        {
            Dictionary<uint256, List<PaymentDetails>> paymentDetailsList = new Dictionary<uint256, List<PaymentDetails>>();
            var selectSpendTrnCmd = connection.CreateCommand();
            selectSpendTrnCmd.CommandText = "SELECT DISTINCT pd.Id,  tsl.SpendingTransactionId, pd.Amount, " +
                " pd.DestinationAddress, pd.DestinationScriptPubKey , sd.TransactionHash FROM PaymentDetails pd " +
                " INNER JOIN SpendingDetails sd ON sd.Id = pd.SpendingTransactionId " +
                " INNER JOIN TransactionSpendingLinks tsl ON sd.id = tsl.SpendingTransactionId and tsl.WalletId = $WalletId " +
                " WHERE pd.WalletId = $WalletId";
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
            return paymentDetailsList;
        }

        private Dictionary<long, SpendingDetails> GetSpendingDetailsForThisWallet(Wallet wallet)
        {
            Dictionary<long, SpendingDetails> spendingDetailsList = new Dictionary<long, SpendingDetails>();
            var selectSpendTrnCmd = connection.CreateCommand();
            selectSpendTrnCmd.CommandText = "SELECT sp.Id, sp.TransactionHash, sp.BlockHeight, sp.CreationTime, sp.Hex, tr.Hash as ParentTranscationHash, tsl.TransactionId,  tr.AddressId " +
                " FROM SpendingDetails sp " +
                " INNER JOIN TransactionSpendingLinks tsl ON sp.id = tsl.SpendingTransactionId and tsl.WalletId = $WalletId " +
                " INNER JOIN [Transaction] tr ON tr.Id = tsl.TransactionId  WHERE sp.WalletId = $WalletId ORDER BY sp.BlockHeight ASC";
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

        private void FlushWalletCache(long id)
        {
            string cacheKey = "wallet_" + id;
            if (walletCache.ContainsKey(cacheKey))
            {
               Wallet x = walletCache[cacheKey];
               string name = x.Name;
               walletCache.TryRemove(name, out x);
               walletCache.TryRemove(cacheKey, out x);
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
            if (walletCache.ContainsKey(cacheKey))
            {
                return walletCache[cacheKey];
            }
            //TODO implement wallet specific locking to ensure single thread per wallet id re-entry
            Wallet wallet = null;

            var selectWalletCmd = connection.CreateCommand();
            selectWalletCmd.CommandText = "SELECT ROWID,Name,EncryptedSeed,ChainCode,Network,CreationTime," +
                "LastBlockSyncedHash,LastBlockSyncedHeight, CoinType, Blocks FROM Wallet WHERE id = $id";
            selectWalletCmd.Parameters.AddWithValue("$id", id);

            wallet = ReadWalletFromDb(wallet, selectWalletCmd);

            if (wallet != null)
            {
                walletCache[wallet.Name] = wallet;
                walletCache["wallet_" + wallet.Id] = wallet;
            }

            return wallet;
        }




        public long SaveAddress(long walletId, HdAddress address, bool saveTransactionsHere = false)
        {
            using (var dbTransaction = this.connection.BeginTransaction())
            {
                var retval = SaveAddress(walletId, dbTransaction, address, saveTransactionsHere);
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

        /// <summary>
        /// Saves address to db making it queryable by address, ScriptPubKey. It relates to account through HD path. Does not commit transaction itself. Caller controlls transaction and must commit.
        /// </summary>
        /// <param name="wallet"></param>
        /// <param name="dbTransaction"></param>
        /// <param name="address"></param>
        private long SaveAddress(long walletId, SQLiteTransaction dbTransaction, HdAddress address, bool saveTransactionsHere = false)
        {
            if (address.Id < 1)
            {
                var insertCommand = this.connection.CreateCommand();
                insertCommand.Transaction = dbTransaction;
                insertCommand.CommandText = "INSERT INTO Address (WalletId, HdIndex, ScriptPubKey, Pubkey,ScriptPubKeyHash, Address, HdPath ) " +
                " VALUES ( $WalletId, $Index, $ScriptPubKey, $Pubkey,$ScriptPubKeyHash, $Address, $HdPath )";
                insertCommand.Parameters.AddWithValue("$WalletId", walletId);
                insertCommand.Parameters.AddWithValue("$Index", address.Index);
                insertCommand.Parameters.AddWithValue("$ScriptPubKey", PackageSriptToString(address.ScriptPubKey));
                insertCommand.Parameters.AddWithValue("$Pubkey", PackageSriptToString(address.Pubkey));
                insertCommand.Parameters.AddWithValue("$ScriptPubKeyHash", address.ScriptPubKey.Hash.ToString());
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
            if (saveTransactionsHere)
            {
                DeleteAddressTransactions(walletId, dbTransaction, address);
                //to save transactions we need to drop all transactions for this address.
                //and then re-add the one's that are left.
                foreach (var chainTran in address.Transactions)
                {
                    chainTran.DbId = SaveTransaction(walletId, dbTransaction, address, chainTran);
                }
            }

            address.WalletId = walletId;
            return address.Id;
        }

        private void DeleteAddressTransactions(long walletId, SQLiteTransaction dbTransaction, HdAddress address)
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

            var deletPaymentDetailCmd = this.connection.CreateCommand();
            deletPaymentDetailCmd.Transaction = dbTransaction;
            deletPaymentDetailCmd.CommandText = "DELETE FROM TransactionSpendingLinks WHERE TransactionId in ( " +
                "SELECT DISTINCT Id FROM [Transaction] " +
                "WHERE AddressId = $AddressId AND WalletId = $WalletId )";
            deletPaymentDetailCmd.Parameters.AddWithValue("$WalletId", walletId);
            deletPaymentDetailCmd.Parameters.AddWithValue("$AddressId", address.Id);
            deletPaymentDetailCmd.ExecuteNonQuery();

            var deleteTransactionCmd= this.connection.CreateCommand();
            deleteTransactionCmd.Transaction = dbTransaction;
            deleteTransactionCmd.CommandText = "DELETE FROM [Transaction] " +
                "WHERE AddressId = $AddressId AND WalletId = $WalletId";
            deleteTransactionCmd.Parameters.AddWithValue("$WalletId", walletId);
            deleteTransactionCmd.Parameters.AddWithValue("$AddressId", address.Id);
            deleteTransactionCmd.ExecuteNonQuery();
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
            using (var dbTransaction = this.connection.BeginTransaction())
            {
                long retval = SaveTransaction(walletId, dbTransaction, address, trx);
                dbTransaction.Commit();
                return retval;
            }
        }

        private long SaveTransaction(long walletId, SQLiteTransaction dbTransaction, HdAddress address, TransactionData trx)
        {
            //one transaction can be saved in multiple addresses. 
            if (trx == null)
            {
                return 0;
            }

            trx.DbId = GetTransactionDbId(walletId, dbTransaction, trx, address.Id);

            if (trx.DbId < 1)
            {
                var insertCommand = this.connection.CreateCommand();
                insertCommand.Transaction = dbTransaction;
                insertCommand.CommandText = "INSERT INTO [Transaction]  (WalletId, AddressId, TxIndex, Hash, Amount, BlockHeight, BlockHash, CreationTime, MerkleProof, ScriptPubKey , Hex, IsPropagated, IsSpent) " +
                " VALUES ( $WalletId, $AddressId, $TxIndex, $Hash, $Amount, $BlockHeight, $BlockHash, $CreationTime, $MerkleProof, $ScriptPubKey , $Hex, $IsPropagated, $IsSpent )";
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

                trx.DbId = GetTransactionDbId(walletId, dbTransaction, trx, address.Id);

            }
            else
            {
                var updateTrxCommand = this.connection.CreateCommand();
                updateTrxCommand.Transaction = dbTransaction;
                updateTrxCommand.CommandText = "UPDATE [Transaction]  set BlockHeight = $BlockHeight , BlockHash = $BlockHash, IsPropagated = $IsPropagated, IsSpent= $IsSpent  WHERE WalletId = $WalletId AND Hash = $Hash ";
                updateTrxCommand.Parameters.AddWithValue("$WalletId", walletId);
                updateTrxCommand.Parameters.AddWithValue("$Hash", trx.Id);
                updateTrxCommand.Parameters.AddWithValue("$BlockHeight", trx.BlockHeight);
                updateTrxCommand.Parameters.AddWithValue("$BlockHash", trx.BlockHash);
                updateTrxCommand.Parameters.AddWithValue("$IsPropagated", trx.IsPropagated);
                updateTrxCommand.Parameters.AddWithValue("$IsSpent", (trx.SpendingDetails != null) ? true : false);

                updateTrxCommand.ExecuteNonQuery();
            }



            if (trx.SpendingDetails != null)
            {
                trx.SpendingDetails.DbId = GetTransactionSpendingDbId(walletId, dbTransaction, trx);
                var spendTrx = trx.SpendingDetails;
                if (spendTrx.DbId < 1)
                {

                    var insertSpendCommand = this.connection.CreateCommand();
                    insertSpendCommand.Transaction = dbTransaction;
                    insertSpendCommand.CommandText = "INSERT INTO SpendingDetails  (WalletId, TransactionHash, BlockHeight, CreationTime, Hex ) " +
                    " VALUES ( $WalletId,  $TransactionHash, $BlockHeight, $CreationTime, $Hex ) ";
                    insertSpendCommand.Parameters.AddWithValue("$WalletId", walletId);
                    insertSpendCommand.Parameters.AddWithValue("$TransactionHash", spendTrx.TransactionId);
                    insertSpendCommand.Parameters.AddWithValue("$BlockHeight", spendTrx.BlockHeight);
                    insertSpendCommand.Parameters.AddWithValue("$CreationTime", spendTrx.CreationTime.ToUnixTimeSeconds());
                    insertSpendCommand.Parameters.AddWithValue("$Hex", spendTrx.Hex);


                    insertSpendCommand.ExecuteNonQuery();

                    spendTrx.DbId = GetTransactionSpendingDbId(walletId, dbTransaction, trx);

                    //var deletPaymentDetailCmd = this.connection.CreateCommand();
                    //deletPaymentDetailCmd.Transaction = dbTransaction;
                    //deletPaymentDetailCmd.CommandText = "DELETE FROM PaymentDetails  WHERE SpendingTransactionId = $SpendingTransactionId AND WalletId = $WalletId";
                    //deletPaymentDetailCmd.Parameters.AddWithValue("$WalletId", walletId);
                    //deletPaymentDetailCmd.Parameters.AddWithValue("$SpendingTransactionId", spendTrx.DbId);
                    //deletPaymentDetailCmd.ExecuteNonQuery();

                    foreach (var item in trx.SpendingDetails.Payments)
                    {
                        var insertPaymentDetailCommand = this.connection.CreateCommand();
                        insertPaymentDetailCommand.Transaction = dbTransaction;
                        insertPaymentDetailCommand.CommandText = "INSERT INTO PaymentDetails  (WalletId, SpendingTransactionId, Amount, DestinationAddress, DestinationScriptPubKey) " +
                        " VALUES ( $WalletId,  $SpendingTransactionId, $Amount, $DestinationAddress, $DestinationScriptPubKey) ";
                        insertPaymentDetailCommand.Parameters.AddWithValue("$WalletId", walletId);
                        insertPaymentDetailCommand.Parameters.AddWithValue("$SpendingTransactionId", spendTrx.DbId);
                        insertPaymentDetailCommand.Parameters.AddWithValue("$Amount", item.Amount.Satoshi);
                        insertPaymentDetailCommand.Parameters.AddWithValue("$DestinationAddress", item.DestinationAddress);
                        insertPaymentDetailCommand.Parameters.AddWithValue("$DestinationScriptPubKey", PackageSriptToString(item.DestinationScriptPubKey));

                        insertPaymentDetailCommand.ExecuteNonQuery();
                    }
                }
                else
                {
                    var updateTrxCommand = this.connection.CreateCommand();
                    updateTrxCommand.Transaction = dbTransaction;
                    updateTrxCommand.CommandText = "UPDATE SpendingDetails  set BlockHeight = $BlockHeight WHERE WalletId = $WalletId AND TransactionHash = $TransactionHash ";
                    updateTrxCommand.Parameters.AddWithValue("$WalletId", walletId);
                    updateTrxCommand.Parameters.AddWithValue("$TransactionHash", spendTrx.TransactionId);
                    updateTrxCommand.Parameters.AddWithValue("$BlockHeight", spendTrx.BlockHeight);

                    updateTrxCommand.ExecuteNonQuery();
                }

                var insertCommand = this.connection.CreateCommand();
                insertCommand.Transaction = dbTransaction;
                insertCommand.CommandText = "INSERT OR REPLACE INTO TransactionSpendingLinks (WalletId, TransactionId,SpendingTransactionId) " +
                " VALUES ( $WalletId, $TransactionId, $SpendingTransactionId )";
                insertCommand.Parameters.AddWithValue("$WalletId", walletId);
                insertCommand.Parameters.AddWithValue("$TransactionId", trx.DbId);
                insertCommand.Parameters.AddWithValue("$SpendingTransactionId", spendTrx.DbId);

                insertCommand.ExecuteNonQuery();
            }

            FlushWalletCache(walletId);
            //rebuild cache async
            Task task = Task.Run(() =>
                {
                    GetWalletById(walletId);
                }
            );
            
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

        private long GetTransactionSpendingDbId(long walletId, SQLiteTransaction dbTransaction, TransactionData trx)
        {
            var selectCmd = this.connection.CreateCommand();
            selectCmd.Transaction = dbTransaction;
            selectCmd.CommandText = "SELECT Id FROM SpendingDetails WHERE WalletId = $WalletId  AND TransactionHash = $TransactionHash"; //
            selectCmd.Parameters.AddWithValue("$TransactionHash", trx.SpendingDetails.TransactionId);
            selectCmd.Parameters.AddWithValue("$WalletId", walletId);
            using (var reader = selectCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    trx.SpendingDetails.DbId = reader.GetInt64(0);
                }
            }
            return trx.SpendingDetails.DbId;
        }


        private long GetTransactionDbId(long walletId, SQLiteTransaction dbTransaction, TransactionData trx, long AddressId)
        {
            var selectCmd = this.connection.CreateCommand();
            selectCmd.Transaction = dbTransaction;
            selectCmd.CommandText = "SELECT id  FROM [Transaction] WHERE Hash = $Hash AND WalletId = $WalletId AND AddressId = $AddressId";
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

            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = "UPDATE Wallet set LastBlockSyncedHash = $LastBlockSyncedHash, LastBlockSyncedHeight = $LastBlockSyncedHeight,  LastUpdated = $LastUpdated, Blocks = $Blocks WHERE Name = $Name";

            insertCommand.Parameters.AddWithValue("$Name", walletName);
            insertCommand.Parameters.AddWithValue("$LastBlockSyncedHash", chainedHeader.HashBlock);
            insertCommand.Parameters.AddWithValue("$LastBlockSyncedHeight", chainedHeader.Height);
            insertCommand.Parameters.AddWithValue("$LastUpdated", DateTimeOffset.Now.ToUnixTimeSeconds());

            byte[] bytes = GetBlockLocatorBytes(chainedHeader.GetLocator().Blocks);
            SQLiteParameter prm = new SQLiteParameter("$Blocks", DbType.Binary, bytes.Length, ParameterDirection.Input, false, 0, 0, null, DataRowVersion.Current, bytes);
            insertCommand.Parameters.Add(prm);


            insertCommand.ExecuteNonQuery();
            UpdateLastSychedInMemory(walletName, chainedHeader);
        }

        public void SaveLastSyncedBlock(ChainedHeader chainedHeader)
        {
            var insertCommand = connection.CreateCommand();
            //global batch update
            insertCommand.CommandText = "UPDATE Wallet set LastBlockSyncedHash = $LastBlockSyncedHash, LastBlockSyncedHeight = $LastBlockSyncedHeight,  LastUpdated = $LastUpdated , Blocks = $Blocks";

            insertCommand.Parameters.AddWithValue("$LastBlockSyncedHash", chainedHeader.HashBlock);
            insertCommand.Parameters.AddWithValue("$LastBlockSyncedHeight", chainedHeader.Height);
            insertCommand.Parameters.AddWithValue("$LastUpdated", DateTimeOffset.Now.ToUnixTimeSeconds());

            byte[] bytes = GetBlockLocatorBytes(chainedHeader.GetLocator().Blocks);
            SQLiteParameter prm = new SQLiteParameter("$Blocks", DbType.Binary, bytes.Length, ParameterDirection.Input, false, 0, 0, null, DataRowVersion.Current, bytes);
            insertCommand.Parameters.Add(prm);

            insertCommand.ExecuteNonQuery();

            foreach (var walletName in walletCache.Keys)
            {
                UpdateLastSychedInMemory(walletName, chainedHeader);
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
        }

        public WalletSyncPosition GetLastSyncedBlock()
        {
            WalletSyncPosition syncPosition = null;
            var selectWalletCmd = connection.CreateCommand();
            selectWalletCmd.CommandText = "SELECT LastBlockSyncedHeight , LastBlockSyncedHash FROM Wallet ORDER BY LastUpdated desc LIMIT 1";

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

            return syncPosition;
        }

        private uint256 ExtractUint256FromNullableDbField(SQLiteDataReader reader, int index)
        {
            uint256 BlockHashLocal = null;
            string LastBlockSyncedHash = reader[index] as string;
            if (!String.IsNullOrEmpty(LastBlockSyncedHash))
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
            var selectWalletCmd = connection.CreateCommand();
            selectWalletCmd.CommandText = "SELECT LastBlockSyncedHeight , LastBlockSyncedHash FROM Wallet WHERE walletName = $Name ";
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

            var selectWalletCmd = connection.CreateCommand();
            selectWalletCmd.CommandText = "SELECT Name FROM Wallet";
            using (var reader = selectWalletCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    result.Add(reader.GetString(0));
                }
            }

            return result;
        }

        public IEnumerable<WalletPointer> GetAllWalletPointers()
        {
            List<WalletPointer> result = new List<WalletPointer>();

            var selectWalletCmd = connection.CreateCommand();
            selectWalletCmd.CommandText = "SELECT id,Name FROM Wallet";

            using (var reader = selectWalletCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    result.Add(new WalletPointer(reader.GetInt32(0), reader.GetString(1)));
                }
            }
            return result;
        }


        public ICollection<uint256> GetFirstWalletBlockLocator()
        {
            BlockLocator locator = new BlockLocator();

            var selectWalletCmd = connection.CreateCommand();
            selectWalletCmd.CommandText = "SELECT RowId, Blocks FROM Wallet ORDER BY CreationTime DESC LIMIT 1";

            using (var reader = selectWalletCmd.ExecuteReader(CommandBehavior.KeyInfo))
            {
                while (reader.Read())
                {
                    locator = ExtractBlockLocator(reader, 1);
                }
            }

            return locator.Blocks;
        }

        public BlockLocator GetWalletBlockLocator(long walletId)
        {
            BlockLocator locator = new BlockLocator();
            var selectLocator = connection.CreateCommand();
            selectLocator.CommandText = "SELECT  RowId,Blocks  FROM Wallet WHERE WalletId = $WalletId";
            selectLocator.Parameters.AddWithValue("$WalletId", walletId);
            using (var reader = selectLocator.ExecuteReader(CommandBehavior.KeyInfo))
            {
                while (reader.Read())
                {
                    locator = ExtractBlockLocator(reader, 1);
                }
            }
            return locator;
        }

        internal int? GetEarliestWalletHeight()
        {
            int? result = null;

            var selectWalletCmd = connection.CreateCommand();
            selectWalletCmd.CommandText = "SELECT LastBlockSyncedHeight FROM Wallet order by CreationTime DESC LIMIT 1";

            using (var reader = selectWalletCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    result = ExtractNullableInt(reader, 0);
                }
            }
            return result;
        }
        internal DateTimeOffset GetOldestWalletCreationTime()
        {
            DateTimeOffset result = DateTimeOffset.MinValue;

            var selectWalletCmd = connection.CreateCommand();
            selectWalletCmd.CommandText = "SELECT CreationTime FROM Wallet order by CreationTime ASC LIMIT 1";

            using (var reader = selectWalletCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    result = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(0));
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
            var selectAccountsCmd = connection.CreateCommand();
            selectAccountsCmd.CommandText = "SELECT WalletId FROM Address WHERE Address = $Address";
            selectAccountsCmd.Parameters.AddWithValue("$Address", address);
            using (var reader = selectAccountsCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    walletId = reader.GetInt64(0);
                }
            }

            wallet = GetWalletById(walletId);

            return wallet;
        }

        internal long GetWalletIdByAddress(string address)
        {
            long walletId = 0;
            var selectAccountsCmd = connection.CreateCommand();
            selectAccountsCmd.CommandText = "SELECT WalletId FROM Address WHERE Address = $Address";
            selectAccountsCmd.Parameters.AddWithValue("$Address", address);
            using (var reader = selectAccountsCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    walletId = reader.GetInt64(0);
                }
            }
            return walletId;
        }

        internal Wallet GetWalletByScriptHash(string ScriptPubKeyHash)
        {
            Wallet wallet = null;
            long walletId = 0;
            var selectAccountsCmd = connection.CreateCommand();
            selectAccountsCmd.CommandText = "SELECT WalletId FROM Address WHERE ScriptPubKeyHash = $ScriptPubKeyHash";
            selectAccountsCmd.Parameters.AddWithValue("$ScriptPubKeyHash", ScriptPubKeyHash);

            using (var reader = selectAccountsCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    walletId = reader.GetInt64(0);
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

            var selectWalletCmd = connection.CreateCommand();
            selectWalletCmd.CommandText = "SELECT LastBlockSyncedHash FROM Wallet order by LastUpdated DESC, LastBlockSyncedHeight DESC LIMIT 1";

            using (var reader = selectWalletCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    uint256 BlockHashLocal = ExtractUint256FromNullableDbField(reader, 0);
                    result = BlockHashLocal;
                }
            }
            return result;
        }

        internal string GetLastUpdatedWalletName()
        {
            string result = null;

            var selectWalletCmd = connection.CreateCommand();
            selectWalletCmd.CommandText = "SELECT Name FROM Wallet order by LastUpdated DESC LIMIT 1";

            using (var reader = selectWalletCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    result = reader.GetString(0);
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