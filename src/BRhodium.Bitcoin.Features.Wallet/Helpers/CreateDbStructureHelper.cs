using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Text;
using System.Transactions;

namespace BRhodium.Bitcoin.Features.Wallet.Helpers
{
    internal class CreateDbStructureHelper
    {
        internal void CreateIt(string connection)
        {
            using (var dbConnection = new SQLiteConnection(connection))
            {
                dbConnection.Open();

                using (var transaction = dbConnection.BeginTransaction())
                {
                    try
                    {
                        var sql = "CREATE TABLE \"Wallet\" (" +
                                "\"Id\"    INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, " +
                                "\"Name\"  TEXT NOT NULL UNIQUE, " +
                                "\"EncryptedSeed\" TEXT NOT NULL, " +
                                "\"ChainCode\" TEXT NOT NULL, " +
                                "\"Network\"   TEXT NOT NULL, " +
                                "\"CreationTime\"  INTEGER NOT NULL, " +
                                "\"LastBlockSyncedHash\" TEXT NULL, " +
                                "\"LastBlockSyncedHeight\" INTEGER NULL, " +
                                "\"CoinType\" INTEGER NOT NULL, " +
                                "\"LastUpdated\" INTEGER NOT NULL, " +
                                "\"Blocks\"    BLOB " +
                                "); ";
                        using (var command = new SQLiteCommand(sql, dbConnection, transaction))
                        {
                            command.ExecuteNonQuery();
                        }

                        sql = "CREATE TABLE \"Account\"(" +
                              "\"Id\"    INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT," +
                              "\"WalletId\"  INTEGER  NOT NULL," +
                              "\"HdIndex\"   INTEGER  NOT NULL," +
                              "\"Name\"  TEXT NOT NULL," +
                              "\"HdPath\"    TEXT NOT NULL," +
                              "\"ExtendedPubKey\"    TEXT NOT NULL," +
                              "\"CreationTime\"  INTEGER  NOT NULL" +
                        ");";
                        using (var command = new SQLiteCommand(sql, dbConnection, transaction))
                        {
                            command.ExecuteNonQuery();
                        }

                        sql = "CREATE TABLE \"Address\"(" +
                              "\"Id\"    INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT," +
                              "\"WalletId\"  INTEGER NOT NULL," +
                              "\"HdIndex\"   INTEGER NOT NULL," +
                              "\"ScriptPubKey\"  TEXT NOT NULL," +
                              "\"Pubkey\"    TEXT NOT NULL," +
                              "\"ScriptPubKeyHash\" TEXT NOT NULL UNIQUE," +
                              "\"Address\"   TEXT NOT NULL UNIQUE," +
                              "\"HdPath\"    TEXT NOT NULL" +
                        ");";
                        using (var command = new SQLiteCommand(sql, dbConnection, transaction))
                        {
                            command.ExecuteNonQuery();
                        }

                        sql = "CREATE TABLE \"Transaction\"(" +
                              "\"Id\"    INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT," +
                              "\"WalletId\"  INTEGER NOT NULL," +
                              "\"AddressId\" INTEGER NOT NULL," +
                              "\"TxIndex\"   INTEGER NOT NULL," +
                              "\"Hash\"  TEXT NOT NULL," +
                              "\"Amount\"    INTEGER NOT NULL," +
                              "\"BlockHeight\"   INTEGER NULL," +
                              "\"BlockHash\" TEXT NULL," +
                              "\"CreationTime\"  INTEGER NOT NULL," +
                              "\"MerkleProof\"   TEXT NULL," +
                              "\"ScriptPubKey\"  TEXT NOT NULL," +
                              "\"Hex\"   TEXT NULL," +
                              "\"IsPropagated\"  NUMERIC DEFAULT 0 ," +
                              "\"IsSpent\"   NUMERIC DEFAULT 0 " +
                        ");";
                        using (var command = new SQLiteCommand(sql, dbConnection, transaction))
                        {
                            command.ExecuteNonQuery();
                        }

                        sql = " CREATE TABLE \"TransactionSpendingLinks\"(" +
                              "\"WalletId\"  INTEGER NOT NULL," +
                              "\"TransactionId\" INTEGER NOT NULL," +
                              "\"SpendingTransactionId\" INTEGER NOT NULL," +
                              "PRIMARY KEY(WalletId, TransactionId, SpendingTransactionId)" +
                        ");";
                        using (var command = new SQLiteCommand(sql, dbConnection, transaction))
                        {
                            command.ExecuteNonQuery();
                        }

                        sql = " CREATE TABLE \"SpendingDetails\"(" +
                            "\"Id\"    INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT," +
                            "\"WalletId\"  INTEGER NOT NULL," +
                            "\"TransactionHash\"   TEXT NOT NULL," +
                            "\"BlockHeight\"   INTEGER NULL," +
                            "\"CreationTime\"  INTEGER NOT NULL," +
                            "\"Hex\"   TEXT NULL" +
                        ");";
                        using (var command = new SQLiteCommand(sql, dbConnection, transaction))
                        {
                            command.ExecuteNonQuery();
                        }

                        sql = "CREATE TABLE \"PaymentDetails\"( " +
                            "\"Id\"    INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, " +
                            "\"WalletId\"  INTEGER  NOT NULL,  " +
                            "\"SpendingTransactionId\" INTEGER  NOT NULL, " +
                            "\"Amount\"    INTEGER  NOT NULL," +
                            "\"DestinationAddress\"    TEXT NULL," +//OP_RETURN does not need address
                            "\"DestinationScriptPubKey\"   TEXT NOT NULL" +
                        ");";
                        using (var command = new SQLiteCommand(sql, dbConnection, transaction))
                        {
                            command.ExecuteNonQuery();
                        }

                        sql = "CREATE UNIQUE INDEX \"ix_Address\" ON \"Address\" (\"Address\");";

                        using (var command = new SQLiteCommand(sql, dbConnection, transaction))
                        {
                            command.ExecuteNonQuery();
                        }

                        sql = "CREATE UNIQUE INDEX \"ix_Address_ScriptPubKey\" ON \"Address\" (\"ScriptPubKey\");";
                        using (var command = new SQLiteCommand(sql, dbConnection, transaction))
                        {
                            command.ExecuteNonQuery();
                        }

                        sql = "CREATE UNIQUE INDEX \"ix_WalletName\" ON \"Wallet\" (\"Name\");";
                        using (var command = new SQLiteCommand(sql, dbConnection, transaction))
                        {
                            command.ExecuteNonQuery();
                        }

                        sql = "CREATE INDEX \"ix_Transaction_WalletId\" ON \"Transaction\" (\"WalletId\");";
                        using (var command = new SQLiteCommand(sql, dbConnection, transaction))
                        {
                            command.ExecuteNonQuery();
                        }

                        sql = "CREATE INDEX \"ix_Transaction_Combined\" ON \"Transaction\" (\"WalletId\",\"AddressId\",\"TxIndex\");";
                        using (var command = new SQLiteCommand(sql, dbConnection, transaction))
                        {
                            command.ExecuteNonQuery();
                        }

                        sql = "CREATE INDEX \"ix_SpendingDetails_WalletId\" ON \"SpendingDetails\" (\"WalletId\");";
                        using (var command = new SQLiteCommand(sql, dbConnection, transaction))
                        {
                            command.ExecuteNonQuery();
                        }

                        sql = "CREATE INDEX \"ix_SpendingDetails_WalletIdAndTxHash\" ON \"SpendingDetails\" (\"WalletId\",\"TransactionHash\");";
                        using (var command = new SQLiteCommand(sql, dbConnection, transaction))
                        {
                            command.ExecuteNonQuery();
                        }

                        sql = "CREATE INDEX \"ix_PaymentDetails_WalletId\" ON \"PaymentDetails\" (\"WalletId\");";
                        using (var command = new SQLiteCommand(sql, dbConnection, transaction))
                        {
                            command.ExecuteNonQuery();
                        }

                        sql = "CREATE INDEX \"ix_TransactionSpendingLinks_WalletId\" ON \"TransactionSpendingLinks\" (\"WalletId\");";
                        using (var command = new SQLiteCommand(sql, dbConnection, transaction))
                        {
                            command.ExecuteNonQuery();
                        }
                        

                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }
    }
}
