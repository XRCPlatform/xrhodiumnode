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

                if (wallet.BlockLocator != null && wallet.BlockLocator.Count > 0)
                {
                    SaveBlockLocator(wallet.Name, dbTransaction, new BlockLocator()
                    {
                        Blocks = wallet.BlockLocator
                    });
                }

                dbTransaction.Commit();

                if (wallet != null)
                {
                    walletCache[wallet.Name] = wallet;
                    walletCache["wallet_" + wallet.Id] = wallet;
                }

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

            var selectWalletCmd = connection.CreateCommand();
            selectWalletCmd.CommandText = "SELECT id,Name,EncryptedSeed,ChainCode,Network,CreationTime," +
                "LastBlockSyncedHash,LastBlockSyncedHeight, CoinType FROM Wallet WHERE Name = $Name";
            selectWalletCmd.Parameters.AddWithValue("$Name", name);

            wallet = ReadWalletFromDb(wallet, selectWalletCmd);

            if (wallet != null)
            {
                walletCache[name] = wallet;
                walletCache["wallet_" + wallet.Id] = wallet;
            }

            return wallet;
        }

        private Wallet ReadWalletFromDb(Wallet wallet, SQLiteCommand selectWalletCmd)
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
                    int LastBlockSyncedHeight = ExtractNullableInt(reader, 7);

                    int coinType = reader.GetInt16(8);
                    uint256 lastBlockHash = null;
                    if (!String.IsNullOrEmpty(blockHash))
                    {
                        lastBlockHash = new uint256(blockHash);
                    }
                    BuildAccountRoot(wallet, LastBlockSyncedHeight, coinType, lastBlockHash);
                }
            }

            return wallet;
        }

        private int ExtractNullableInt(SQLiteDataReader reader, int index)
        {
            int LastBlockSyncedHeight = 0;

            if (reader[index].GetType() != typeof(DBNull))
            {
                LastBlockSyncedHeight = reader.GetInt32(index);
            }

            return LastBlockSyncedHeight;
        }

        private void BuildAccountRoot(Wallet wallet, int LastBlockSyncedHeight, int coinType, uint256 lastBlockHash)
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

            List<uint256> locator = new List<uint256>();

            var selectWalletCmd = connection.CreateCommand();
            selectWalletCmd.CommandText = "SELECT  BlockHash FROM BlockLocator WHERE WalletId = $WalletId";
            selectWalletCmd.Parameters.AddWithValue("$WalletId", wallet.Id);
            using (var reader = selectWalletCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    locator.Add(new uint256(reader.GetString(0)));
                }
            }
            wallet.BlockLocator = locator;
        }

        private void ReadTransactions(Wallet wallet)
        {

            Dictionary<uint256, List<PaymentDetails>> paymentDetails = GetPaymentDetailsForThisWallet(wallet);
            Dictionary<uint256, SpendingDetails> spendingDetails = GetSpendingDetailsForThisWallet(wallet, paymentDetails);

            long last_AddressId = 0;
            HdAddress last_hdAddress = null;
            var selectTrnCommand = connection.CreateCommand();
            selectTrnCommand.CommandText = "SELECT Id, TxIndex, Hash, Amount, BlockHeight, BlockHash, CreationTime, MerkleProof, ScriptPubKey, Hex, IsPropagated,  AddressId, IsFinal " +
                " FROM [Transaction] WHERE WalletId = $WalletId order by AddressId asc";
            selectTrnCommand.Parameters.AddWithValue("$WalletId", wallet.Id);
            using (var reader = selectTrnCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    var merkle = new PartialMerkleTree();
                    string merkleString = ExtractStringFromNullableDbField(reader, 7);
                    if (!String.IsNullOrEmpty(merkleString))
                    {
                        merkle.FromBytes(merkleString.ToByteArrayFromHex());
                    }

                    var transaction = new TransactionData()
                    {
                        DbId = reader.GetInt64(0),
                        Index = reader.GetInt32(1),
                        Id = ExtractUint256FromNullableDbField(reader, 2),
                        Amount = new Money(reader.GetInt64(3)),
                        BlockHeight = ExtractNullableInt(reader, 4),
                        BlockHash = ExtractUint256FromNullableDbField(reader, 5),
                        CreationTime = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(6)),
                        MerkleProof = merkle,
                        ScriptPubKey = ExtractScriptFromDb(reader.GetString(8)),
                        Hex = ExtractStringFromNullableDbField(reader, 9),
                        IsPropagated = (reader.GetInt32(10) == 1),
                        AddressId = reader.GetInt64(11),
                        IsFinal = (reader.GetInt32(12) == 1)
                    };
                    if (spendingDetails.ContainsKey(transaction.Id))
                    {
                        transaction.SpendingDetails = spendingDetails[transaction.Id];
                    }

                    if (reader.GetInt64(11) != last_AddressId)
                    {
                        last_hdAddress = AddTransactionToAddressInWallet(wallet, transaction);
                        last_AddressId = reader.GetInt64(11);
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
        }

        private Dictionary<uint256, List<PaymentDetails>> GetPaymentDetailsForThisWallet(Wallet wallet)
        {
            Dictionary<uint256, List<PaymentDetails>> paymentDetailsList = new Dictionary<uint256, List<PaymentDetails>>();
            var selectSpendTrnCmd = connection.CreateCommand();
            selectSpendTrnCmd.CommandText = "SELECT pd.Id, pd.AddressId, pd.TransactionId, pd.SpendingTransactionId, pd.Amount, " +
                "pd.DestinationAddress, pd.DestinationScriptPubKey , sd.TransactionHash FROM PaymentDetails pd " +
                "INNER JOIN SpendingDetails sd ON sd.Id = pd.SpendingTransactionId WHERE pd.WalletId = $WalletId";
            selectSpendTrnCmd.Parameters.AddWithValue("$WalletId", wallet.Id);

            using (var reader = selectSpendTrnCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    uint256 trnId = ExtractUint256FromNullableDbField(reader, 7);
                    if (!paymentDetailsList.ContainsKey(trnId))
                    {
                        paymentDetailsList.Add(trnId,new List<PaymentDetails>());
                    }

                    var paymentDetail = new PaymentDetails()
                    {
                        DbId = reader.GetInt64(0),
                        DbAddressId = reader.GetInt64(1),
                        DbTransactionId = reader.GetInt64(2),
                        DbSpendingTransactionId = reader.GetInt64(3),
                        Amount = Money.Satoshis(reader.GetInt64(4)),
                        DestinationAddress = reader.GetString(5),
                        DestinationScriptPubKey = ExtractScriptFromDb(reader.GetString(6)),
                        TransactionId = trnId
                    };

                    var list = paymentDetailsList[trnId];
                    list.Add(paymentDetail);
                    paymentDetailsList.AddOrReplace(trnId, list);
                }
            }
            return paymentDetailsList;
        }

        private Dictionary<uint256, SpendingDetails> GetSpendingDetailsForThisWallet(Wallet wallet, Dictionary<uint256, List<PaymentDetails>> paymentDetails)
        {
            Dictionary<uint256, SpendingDetails> spendingDetailsList = new Dictionary<uint256, SpendingDetails>();
            var selectSpendTrnCmd = connection.CreateCommand();
            selectSpendTrnCmd.CommandText = "SELECT sp.Id, sp.TransactionHash, sp.BlockHeight, sp.CreationTime, sp.Hex, tr.Hash as ParentTranscationHash, sp.IsFinal FROM SpendingDetails sp " +
                " INNER JOIN [Transaction] tr ON tr.Id = sp.TransactionId  WHERE sp.WalletId = $WalletId";
            selectSpendTrnCmd.Parameters.AddWithValue("$WalletId", wallet.Id);

            using (var reader = selectSpendTrnCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    uint256 TransactionId = ExtractUint256FromNullableDbField(reader, 1);
                    uint256 parentTrnHash = ExtractUint256FromNullableDbField(reader, 5);
                    var spendingDetails = new SpendingDetails()
                    {
                        DbId = reader.GetInt64(0),
                        TransactionId = TransactionId,
                        BlockHeight = ExtractNullableInt(reader, 2),
                        CreationTime = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(3)),
                        Hex = ExtractStringFromNullableDbField(reader, 4),
                        ParentTransactionHash = parentTrnHash,
                        IsFinal = reader.GetInt32(6) ==1

                    };
                    if (paymentDetails.ContainsKey(TransactionId))
                    {
                        var payment = paymentDetails[TransactionId];
                        spendingDetails.Payments = payment;
                    }                    
                    spendingDetailsList.AddOrReplace(parentTrnHash, spendingDetails);
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

            var selectWalletCmd = connection.CreateCommand();
            selectWalletCmd.CommandText = "SELECT id,Name,EncryptedSeed,ChainCode,Network,CreationTime," +
                "LastBlockSyncedHash,LastBlockSyncedHeight, CoinType FROM Wallet WHERE id = $id";
            selectWalletCmd.Parameters.AddWithValue("$id", id);

            wallet = ReadWalletFromDb(wallet, selectWalletCmd);

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
                dbTransaction.Commit(); // single batch transactions commiting automatically, attempt to commit thows exceptions
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
            foreach (var chainTran in address.Transactions)
            {
                if (!chainTran.IsFinal ||(chainTran.SpendingDetails!=null && !chainTran.SpendingDetails.IsFinal) )
                {
                    chainTran.DbId = SaveTranscation(walletId, dbTransaction, address, chainTran);
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

        private long SaveTranscation(long walletId, SQLiteTransaction dbTransaction, HdAddress address, TransactionData trx)
        {
            //one transaction can be saved in multiple addresses. 
            if (trx == null)
            {
                return 0;
            }
            if (trx.IsFinal)
            {
                return trx.DbId;
            }
            trx.DbId = GetTransactionDbId(walletId, dbTransaction, trx, address.Id);
            bool IsTrxFinal = (trx.BlockHeight > 0);

            if (trx.DbId < 1 )
            {
                var insertCommand = this.connection.CreateCommand();
                insertCommand.Transaction = dbTransaction;
                insertCommand.CommandText = "INSERT INTO [Transaction]  (WalletId, AddressId, TxIndex, Hash, Amount, BlockHeight, BlockHash, CreationTime, MerkleProof, ScriptPubKey , Hex, IsPropagated, IsSpent, IsFinal) " +
                " VALUES ( $WalletId, $AddressId, $TxIndex, $Hash, $Amount, $BlockHeight, $BlockHash, $CreationTime, $MerkleProof, $ScriptPubKey , $Hex, $IsPropagated, $IsSpent, $IsFinal )";
                insertCommand.Parameters.AddWithValue("$WalletId", walletId);
                insertCommand.Parameters.AddWithValue("$AddressId", address.Id);
                insertCommand.Parameters.AddWithValue("$TxIndex", trx.Index);
                insertCommand.Parameters.AddWithValue("$Hash", trx.Id);
                insertCommand.Parameters.AddWithValue("$Amount", trx.Amount.Satoshi);
                insertCommand.Parameters.AddWithValue("$BlockHeight", trx.BlockHeight);
                insertCommand.Parameters.AddWithValue("$BlockHash", trx.BlockHash);
                insertCommand.Parameters.AddWithValue("$CreationTime", trx.CreationTime.ToUnixTimeSeconds());
                insertCommand.Parameters.AddWithValue("$MerkleProof", PackPartialMerkleTree(trx.MerkleProof));
                insertCommand.Parameters.AddWithValue("$ScriptPubKey", PackageSriptToString(trx.ScriptPubKey));
                insertCommand.Parameters.AddWithValue("$Hex", trx.Hex);
                insertCommand.Parameters.AddWithValue("$IsPropagated", trx.IsPropagated);
                insertCommand.Parameters.AddWithValue("$IsSpent", (trx.SpendingDetails != null) ? true : false);
                insertCommand.Parameters.AddWithValue("$IsFinal", IsTrxFinal);

                insertCommand.ExecuteNonQuery();

                trx.DbId = GetTransactionDbId(walletId, dbTransaction, trx, address.Id);

            }
            else
            {
                var updateTrxCommand = this.connection.CreateCommand();
                updateTrxCommand.Transaction = dbTransaction;
                updateTrxCommand.CommandText = "UPDATE [Transaction]  set BlockHeight = $BlockHeight , BlockHash = $BlockHash, IsPropagated = $IsPropagated, IsSpent= $IsSpent, IsFinal = $IsFinal WHERE WalletId = $WalletId AND Hash = $Hash ";
                updateTrxCommand.Parameters.AddWithValue("$WalletId", walletId);
                updateTrxCommand.Parameters.AddWithValue("$Hash", trx.Id);
                updateTrxCommand.Parameters.AddWithValue("$BlockHeight", trx.BlockHeight);
                updateTrxCommand.Parameters.AddWithValue("$BlockHash", trx.BlockHash);
                updateTrxCommand.Parameters.AddWithValue("$IsPropagated", trx.IsPropagated);
                updateTrxCommand.Parameters.AddWithValue("$IsSpent", (trx.SpendingDetails != null) ? true : false);
                updateTrxCommand.Parameters.AddWithValue("$IsFinal", IsTrxFinal);
                updateTrxCommand.ExecuteNonQuery();
            }



            if (trx.SpendingDetails != null && !trx.SpendingDetails.IsFinal)
            {
                bool IsSpendTrxFinal = (trx.SpendingDetails.BlockHeight > 0);

                trx.SpendingDetails.DbId = GetTransactionSpendingDbId(walletId, dbTransaction, trx.SpendingDetails, address.Id);
                var spendTrx = trx.SpendingDetails;
                if (spendTrx.DbId < 1)
                {
                   
                    var insertSpendCommand = this.connection.CreateCommand();
                    insertSpendCommand.Transaction = dbTransaction;
                    insertSpendCommand.CommandText = "INSERT INTO SpendingDetails  (WalletId, AddressId, TransactionId, TransactionHash, BlockHeight, CreationTime, Hex, IsFinal) " +
                    " VALUES ( $WalletId, $AddressId, $TransactionId, $TransactionHash, $BlockHeight, $CreationTime, $Hex , $IsFinal) ";
                    insertSpendCommand.Parameters.AddWithValue("$WalletId", walletId);
                    insertSpendCommand.Parameters.AddWithValue("$AddressId", address.Id);
                    insertSpendCommand.Parameters.AddWithValue("$TransactionId", trx.DbId);
                    insertSpendCommand.Parameters.AddWithValue("$TransactionHash", spendTrx.TransactionId);
                    insertSpendCommand.Parameters.AddWithValue("$BlockHeight", spendTrx.BlockHeight);
                    insertSpendCommand.Parameters.AddWithValue("$CreationTime", spendTrx.CreationTime.ToUnixTimeSeconds());
                    insertSpendCommand.Parameters.AddWithValue("$Hex", spendTrx.Hex);
                    insertSpendCommand.Parameters.AddWithValue("$IsFinal", IsSpendTrxFinal);

                    insertSpendCommand.ExecuteNonQuery();

                    spendTrx.DbId = GetTransactionSpendingDbId(walletId, dbTransaction, trx.SpendingDetails, address.Id);

                    foreach (var item in trx.SpendingDetails.Payments)
                    {
                        var insertPaymentDetailCommand = this.connection.CreateCommand();
                        insertPaymentDetailCommand.Transaction = dbTransaction;
                        insertPaymentDetailCommand.CommandText = "INSERT INTO PaymentDetails  (WalletId, AddressId, TransactionId, SpendingTransactionId, Amount, DestinationAddress, DestinationScriptPubKey) " +
                        " VALUES ( $WalletId, $AddressId, $TransactionId,  $SpendingTransactionId, $Amount, $DestinationAddress, $DestinationScriptPubKey) ";
                        insertPaymentDetailCommand.Parameters.AddWithValue("$WalletId", walletId);
                        insertPaymentDetailCommand.Parameters.AddWithValue("$AddressId", address.Id);
                        insertPaymentDetailCommand.Parameters.AddWithValue("$TransactionId", trx.DbId);
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
                    updateTrxCommand.CommandText = "UPDATE SpendingDetails  set BlockHeight = $BlockHeight , IsFinal= $IsFinal WHERE WalletId = $WalletId AND TransactionHash = $TransactionHash ";
                    updateTrxCommand.Parameters.AddWithValue("$WalletId", walletId);
                    updateTrxCommand.Parameters.AddWithValue("$TransactionHash", spendTrx.TransactionId);
                    updateTrxCommand.Parameters.AddWithValue("$BlockHeight", spendTrx.BlockHeight);
                    updateTrxCommand.Parameters.AddWithValue("$IsFinal", IsSpendTrxFinal);
                    updateTrxCommand.ExecuteNonQuery();
                }
            }

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

        private long GetTransactionSpendingDbId(long walletId, SQLiteTransaction dbTransaction, SpendingDetails spendingDetails, long AddressId)
        {
            var selectCmd = this.connection.CreateCommand();
            selectCmd.Transaction = dbTransaction;
            selectCmd.CommandText = "SELECT Id FROM SpendingDetails WHERE TransactionHash = $TransactionHash AND WalletId = $WalletId AND AddressId = $AddressId";
            selectCmd.Parameters.AddWithValue("$TransactionHash", spendingDetails.TransactionId);
            selectCmd.Parameters.AddWithValue("$WalletId", walletId);
            selectCmd.Parameters.AddWithValue("$AddressId", AddressId);
            using (var reader = selectCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    spendingDetails.DbId = reader.GetInt64(0);
                }
            }
            return spendingDetails.DbId;
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
            return Encoders.Hex.EncodeData(((Script)value).ToBytes(false));
        }

        public void SaveLastSyncedBlock(string walletName, ChainedHeader chainedHeader)
        {
            Guard.NotNull(walletName, nameof(walletName));

            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = "UPDATE Wallet set LastBlockSyncedHash = $LastBlockSyncedHash, LastBlockSyncedHeight = $LastBlockSyncedHeight,  LastUpdated = $LastUpdated WHERE Name = $Name";

            insertCommand.Parameters.AddWithValue("$Name", walletName);
            insertCommand.Parameters.AddWithValue("$LastBlockSyncedHash", chainedHeader.HashBlock);
            insertCommand.Parameters.AddWithValue("$LastBlockSyncedHeight", chainedHeader.Height);
            insertCommand.Parameters.AddWithValue("$LastUpdated", DateTimeOffset.Now.ToUnixTimeSeconds());

            insertCommand.ExecuteNonQuery();
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
                    int LastBlockSyncedHeight = ExtractNullableInt(reader, 0);

                    uint256 BlockHashLocal = ExtractUint256FromNullableDbField(reader, 1);
                    syncPosition = new WalletSyncPosition()
                    {
                        Height = LastBlockSyncedHeight,
                        BlockHash = BlockHashLocal
                    };
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
                    int LastBlockSyncedHeight = ExtractNullableInt(reader, 0);

                    uint256 BlockHashLocal = ExtractUint256FromNullableDbField(reader, 1);
                    syncPosition = new WalletSyncPosition()
                    {
                        Height = LastBlockSyncedHeight,
                        BlockHash = BlockHashLocal
                    };
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
        public void SaveBlockLocator(string walletName, BlockLocator blocks)
        {
            using (var dbTransaction = this.connection.BeginTransaction())
            {
                SaveBlockLocator(walletName, dbTransaction, blocks);
                //dbTransaction.Commit();//looks like auto commit is doing a job..  passing the tranaction object only because interface
            }
        }
        /// <summary>
        /// Stores blocks for future use.
        /// </summary>
        /// <param name="walletName"></param>
        /// <param name="blocks"></param>
        public void SaveBlockLocator(string walletName, SQLiteTransaction dbTransaction, BlockLocator blocks)
        {
            Guard.NotNull(walletName, nameof(walletName));
            HashSet<uint256> blocksFromDb = new HashSet<uint256>();
            long walletId = 0;

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
                    blocksFromDb.Add(ExtractUint256FromNullableDbField(reader, 2));
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
        }

        public ICollection<uint256> GetFirstWalletBlockLocator()
        {
            List<uint256> result = new List<uint256>();

            var selectWalletCmd = connection.CreateCommand();
            selectWalletCmd.CommandText = "SELECT  BlockHash FROM BlockLocator INNER JOIN Wallet ON BlockLocator.WalletId = Wallet.Id order by CreationTime asc";

            using (var reader = selectWalletCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    result.Add(new uint256(reader.GetString(0)));
                }
            }

            return result;
        }

        internal int? GetEarliestWalletHeight()
        {
            int? result = null;

            var selectWalletCmd = connection.CreateCommand();
            selectWalletCmd.CommandText = "SELECT LastBlockSyncedHeight FROM Wallet order by CreationTime asc LIMIT 1";

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
            selectWalletCmd.CommandText = "SELECT CreationTime FROM Wallet order by CreationTime asc LIMIT 1";

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
                    walletId = reader.GetInt64(1);
                }
            }

            wallet = GetWalletById(walletId);

            return wallet;
        }

        internal Wallet GetWalletByScriptHash(ScriptId hash)
        {
            Wallet wallet = null;
            long walletId = 0;
            var selectAccountsCmd = connection.CreateCommand();
            selectAccountsCmd.CommandText = "SELECT WalletId FROM Address WHERE ScriptPubKey = $ScriptPubKey";
            selectAccountsCmd.Parameters.AddWithValue("$ScriptPubKey", PackageSriptToString(hash.ScriptPubKey));

            using (var reader = selectAccountsCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    walletId = reader.GetInt64(1);
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

            var selectWalletCmd = connection.CreateCommand();
            selectWalletCmd.CommandText = "SELECT LastBlockSyncedHash FROM Wallet order by LastUpdated asc LIMIT 1";

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
            selectWalletCmd.CommandText = "SELECT Name FROM Wallet order by LastUpdated asc LIMIT 1";

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