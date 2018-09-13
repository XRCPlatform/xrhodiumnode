using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using BRhodium.Node.Controllers;
using BRhodium.Bitcoin.Features.RPC;
using BRhodium.Bitcoin.Features.Wallet.Interfaces;
using BRhodium.Bitcoin.Features.Wallet.Models;
using BRhodium.Node.Utilities.JsonContract;
using BRhodium.Node.Utilities.JsonErrors;
using BRhodium.Bitcoin.Features.Wallet.Controllers;
using BRhodium.Bitcoin.Features.Consensus.Models;
using BRhodium.Bitcoin.Features.BlockStore;
using BRhodium.Node.Configuration;
using BRhodium.Node.Utilities;
using BRhodium.Bitcoin.Features.Consensus.Interfaces;
using NBitcoin.RPC;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using BRhodium.Bitcoin.Features.Wallet.Helpers;
using BRhodium.Bitcoin.Features.Wallet.Broadcasting;
using BRhodium.Node.Connection;
using BRhodium.Node;
using System.Threading.Tasks;
using BRhodium.Node.Interfaces;
using System.IO;
using System.Text;
using System.Reflection;
using BRhodium.Bitcoin.Features.RPC.Models;

namespace BRhodium.Bitcoin.Features.Wallet.Controllers
{
    /// <summary>
    /// Wallet Controller RPCs method
    /// </summary>
    /// <seealso cref="BRhodium.Node.Controllers.FeatureController" />
    [Controller]
    public class WalletRPCController : FeatureController
    {
        private const string DEFAULT_ACCOUNT_NAME = "account 0";
        internal IServiceProvider serviceProvider;
        public IWalletManager walletManager { get; set; }

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;
        private readonly ILoggerFactory loggerFactory;
        private BlockStoreCache blockStoreCache;
        private readonly IBlockRepository blockRepository;
        private readonly NodeSettings nodeSettings;
        private readonly Network network;
        public IConsensusLoop ConsensusLoop { get; private set; }
        public IBroadcasterManager broadcasterManager;
        private readonly IConnectionManager connectionManager;

        //wallet address mapping on the node
        public static ConcurrentDictionary<string, string> walletsByAddressMap = new ConcurrentDictionary<string, string>();
        public static ConcurrentDictionary<string, HdAddress> hdAddressByAddressMap = new ConcurrentDictionary<string, HdAddress>();
        private static string walletPassphrase = null;
        private static bool inRescan = false;
        private static DateTime walletPassphraseExpiration = DateTime.MinValue;

        public WalletRPCController(
            IServiceProvider serviceProvider,
            IWalletManager walletManager,
            ILoggerFactory loggerFactory,
            IFullNode fullNode,
            IBlockRepository blockRepository,
            NodeSettings nodeSettings,
            Network network,
            IBroadcasterManager broadcasterManager,
            IConnectionManager connectionManager,
            IConsensusLoop consensusLoop = null)
        {
            this.walletManager = walletManager;
            this.serviceProvider = serviceProvider;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.Network = fullNode.Network;
            this.FullNode = fullNode;
            this.broadcasterManager = broadcasterManager;
            this.connectionManager = connectionManager;
            this.ConsensusLoop = consensusLoop;

            this.loggerFactory = loggerFactory;
            this.nodeSettings = nodeSettings;
            this.blockRepository = blockRepository;
            this.network = network;
            this.blockStoreCache = new BlockStoreCache(this.blockRepository, DateTimeProvider.Default, this.loggerFactory, this.nodeSettings);
        }

        /// <summary>
        /// Sends some amount to specified address.
        /// 
        /// <p>Example: <br/>
        /// Set the passphrase for 2 minutes to perform a transaction<br/>
        /// walletpassphrase "my pass phrase" 120</p>
        /// <p>Perform a send(requires passphrase set)<br/>
        /// sendtoaddress "1M72Sfpbz1BPpXFHz9m3CdqATR44Jvaydd" 1.0</p>
        /// <p>Clear the passphrase since we are done before 2 minutes is up<br/>
        /// walletlock </p> 
        /// </summary>
        /// <param name="addressFrom">Source address.</param>
        /// <param name="address">Target address.</param>
        /// <param name="amount">The amount in BTR</param>
        /// <returns>The transaction id</returns>
        [ActionName("sendtoaddress")]
        [ActionDescription("Sends some amount to specified address.")]
        public IActionResult SendToAddress(string addressFrom, string address, string amount)
        {
            try
            {
                if (string.IsNullOrEmpty(addressFrom))
                {
                    throw new ArgumentNullException("addressFrom");
                }
                if (string.IsNullOrEmpty(address))
                {
                    throw new ArgumentNullException("address");
                }
                if (string.IsNullOrEmpty(amount))
                {
                    throw new ArgumentNullException("amount");
                }
                if (walletPassphraseExpiration < DateTime.Now)
                {
                    walletPassphrase = null;
                }
                if (string.IsNullOrEmpty(walletPassphrase))
                {
                    throw new ArgumentNullException("passphrase");
                }

                //we need to find wallet
                string walletCombix = walletsByAddressMap.TryGet<string, string>(addressFrom);
                if (walletCombix == null)
                {
                    bool isFound = false;

                    foreach (var currWalletName in this.walletManager.GetWalletsNames())
                    {
                        foreach (var currAccount in this.walletManager.GetAccounts(currWalletName))
                        {
                            foreach (var walletAddress in currAccount.ExternalAddresses)
                            {
                                if (walletAddress.Address.ToString().Equals(addressFrom))
                                {
                                    isFound = true;
                                    walletCombix = $"{currAccount.Name}/{currWalletName}";
                                    walletsByAddressMap.TryAdd<string, string>(addressFrom, walletCombix);
                                    hdAddressByAddressMap.TryAdd<string, HdAddress>(address, walletAddress);
                                    break;
                                }
                            }

                            if (isFound) break;
                        }

                        if (isFound) break;
                    }
                }

                if (walletCombix == null)
                {
                    throw new WalletException("Address doesnt exist.");
                }

                string walletAccount = walletCombix.Split('/')[0].Replace("$", string.Empty);
                string walletName = walletCombix.Split('/')[1];
                var mywallet = this.walletManager.GetWallet(walletName);

                //send money
                var money = new Money(decimal.Parse(amount), MoneyUnit.BTR);
                var transaction = SendMoney(walletAccount, walletName, address, walletPassphrase, money.Satoshi);

                return this.Json(ResultHelper.BuildResultResponse(transaction.GetHash().ToString()));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        private WalletAccountReference GetAccount()
        {
            //TODO: Support multi wallet like core by mapping passed RPC credentials to a wallet/account
            var w = this.walletManager.GetWalletsNames().FirstOrDefault();
            if (w == null)
                throw new RPCServerException(NBitcoin.RPC.RPCErrorCode.RPC_INVALID_REQUEST, "No wallet found");
            var account = this.walletManager.GetAccounts(w).FirstOrDefault();
            return new WalletAccountReference(w, account.Name);
        }

        /// <summary>
        /// Wallets the passphrase.
        /// 
        /// <p>Example: <br/>
        /// Set the passphrase for 2 minutes to perform a transaction<br/>
        /// walletpassphrase "my pass phrase" 120</p>
        /// <p>Perform a send(requires passphrase set)<br/>
        /// sendtoaddress "1M72Sfpbz1BPpXFHz9m3CdqATR44Jvaydd" 1.0</p>
        /// <p>Clear the passphrase since we are done before 2 minutes is up<br/>
        /// walletlock </p>
        /// </summary>
        /// <param name="passphrase">The passphrase.</param>
        /// <param name="timeout">The timeout in seconds. Limited to 1073741824 sec.</param>
        /// <returns>Null or success</returns>
        [ActionName("walletpassphrase")]
        [ActionDescription("Stores the wallet decryption key in memory for 'timeout' seconds.")]
        public IActionResult WalletPassphrase(string passphrase, int timeout)
        {
            try
            {
                if (string.IsNullOrEmpty(passphrase))
                {
                    throw new ArgumentNullException("passphrase");
                }
                if ((timeout <= 0) || (timeout > 1073741824))
                {
                    throw new ArgumentNullException("timeout");
                }

                var dateExpiration = DateTime.Now.AddSeconds(timeout);
                walletPassphrase = passphrase;
                walletPassphraseExpiration = dateExpiration;

                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Dumps the priv key.
        /// 
        /// <p>Example: <br/>
        /// Set the passphrase for 2 minutes to perform a transaction<br/>
        /// walletpassphrase "my pass phrase" 120</p>
        /// <p>Perform a send(requires passphrase set)<br/>
        /// sendtoaddress "1M72Sfpbz1BPpXFHz9m3CdqATR44Jvaydd" 1.0</p>
        /// <p>Clear the passphrase since we are done before 2 minutes is up<br/>
        /// walletlock </p>
        /// </summary>
        /// <param name="address">The address.</param>
        /// <returns>Private key</returns>
        [ActionName("dumpprivkey")]
        [ActionDescription("Gets private key of given wallet address")]
        public IActionResult DumpPrivKey(string address)
        {
            try
            {
                if (string.IsNullOrEmpty(address))
                {
                    throw new ArgumentNullException("address");
                }
                if (walletPassphraseExpiration < DateTime.Now)
                {
                    walletPassphrase = null;
                }
                if (string.IsNullOrEmpty(walletPassphrase))
                {
                    throw new ArgumentNullException("passphrase");
                }

                //we need to find wallet
                string walletCombix = walletsByAddressMap.TryGet<string, string>(address);
                if (walletCombix == null)
                {
                    bool isFound = false;

                    foreach (var currWalletName in this.walletManager.GetWalletsNames())
                    {
                        foreach (var currAccount in this.walletManager.GetAccounts(currWalletName))
                        {
                            foreach (var walletAddress in currAccount.ExternalAddresses)
                            {
                                if (walletAddress.Address.ToString().Equals(address))
                                {
                                    isFound = true;
                                    walletCombix = $"{currAccount.Name}/{currWalletName}";
                                    walletsByAddressMap.TryAdd<string, string>(address, walletCombix);
                                    hdAddressByAddressMap.TryAdd<string, HdAddress>(address, walletAddress);
                                    break;
                                }
                            }

                            if (isFound) break;
                        }

                        if (isFound) break;
                    }
                }

                string walletName = walletCombix.Split('/')[1];
                var mywallet = this.walletManager.GetWallet(walletName);

                //try to decript wallet
                foreach (var item in walletPassphrase)
                {
                    try
                    {
                        var privateKey = HdOperations.DecryptSeed(mywallet.EncryptedSeed, walletPassphrase, this.Network);

                        var secret = new BitcoinSecret(privateKey, this.Network);
                        var stringPrivateKey = secret.ToString();
                        return this.Json(ResultHelper.BuildResultResponse(stringPrivateKey));
                    }
                    catch (Exception)
                    {

                    }
                }

                throw new ArgumentNullException("passphrase");
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Lock the wallets.
        /// 
        /// <p>Example: <br/>
        /// Set the passphrase for 2 minutes to perform a transaction<br/>
        /// walletpassphrase "my pass phrase" 120</p>
        /// <p>Perform a send(requires passphrase set)<br/>
        /// sendtoaddress "1M72Sfpbz1BPpXFHz9m3CdqATR44Jvaydd" 1.0</p>
        /// <p>Clear the passphrase since we are done before 2 minutes is up<br/>
        /// walletlock </p>
        /// </summary>
        /// <returns>Null or success</returns>
        [ActionName("walletlock")]
        [ActionDescription("Removes the wallet encryption key from memory, locking the wallet.")]
        public IActionResult WalletLock()
        {
            try
            {
                walletPassphrase = null;
                walletPassphraseExpiration = DateTime.MinValue;

                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// If [walletName] is not specified, returns the server's total available balance.
        /// If [walletName] is specified, returns the balance in the account.
        /// If [walletName] is "*", get the balance of all accounts.
        /// </summary>
        /// <param name="walletName">The account to get the balance for.</param>
        /// <returns>The balance of the account or the total wallet.</returns>
        [ActionName("getbalance")]
        [ActionDescription("Gets account balance")]
        public IActionResult GetBalance(string walletName)
        {
            try
            {
                WalletBalanceModel model = new WalletBalanceModel();

                var accounts = new List<HdAccount>();
                if (walletName == "*")
                {
                    foreach (var wallet in this.walletManager.Wallets)
                    {
                        accounts.Concat(this.walletManager.GetAccounts(wallet.Name).ToList());
                    }
                }
                else
                {
                    accounts = this.walletManager.GetAccounts(walletName).ToList();
                }

                var totalBalance = new Money(0);

                foreach (var account in accounts)
                {
                    var result = account.GetSpendableAmount();

                    List<Money> balances = new List<Money>();
                    balances.Add(totalBalance);
                    balances.Add(result.ConfirmedAmount);
                    balances.Add(result.UnConfirmedAmount);
                    totalBalance = MoneyExtensions.Sum(balances);
                }

                var balanceToString = totalBalance.ToString();
                return this.Json(ResultHelper.BuildResultResponse(balanceToString));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Returns information about a bitcoin address
        /// </summary>
        /// <param name="address">bech32 or base58 BitcoinAddress to validate.</param>
        /// <returns>ValidatedAddress containing a boolean indicating address validity</returns>
        [ActionName("validateaddress")]
        [ActionDescription("Returns information about a bech32 or base58 bitcoin address")]
        public IActionResult ValidateAddress(string address)
        {
            try
            {
                if (string.IsNullOrEmpty(address))
                    throw new ArgumentNullException("address");

                var res = new ValidatedAddress();
                res.IsValid = false;
                res.Address = address;
                res.IsMine = true;
                res.IsWatchOnly = false;
                res.IsScript = false;

                // P2PKH
                if (BitcoinPubKeyAddress.IsValid(address, ref this.Network))
                {
                    res.IsValid = true;
                }
                // P2SH
                else if (BitcoinScriptAddress.IsValid(address, ref this.Network))
                {
                    res.IsValid = true;
                }

                return this.Json(ResultHelper.BuildResultResponse(res));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets the account.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <returns>Return wallet combix as string</returns>
        /// <exception cref="ArgumentNullException">address</exception>
        /// <exception cref="RPCException">Wallet not initialized - null - false</exception>
        [ActionName("getaccount")]
        [ActionDescription("")]
        public IActionResult GetAccount(string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                throw new ArgumentNullException("address");
            }

            string walletCombix = walletsByAddressMap.TryGet<string, string>(address);
            if (walletCombix != null)
            {
                return this.Json(ResultHelper.BuildResultResponse(walletCombix));
            }

            foreach (var currWalletName in this.walletManager.GetWalletsNames())
            {
                foreach (var currAccount in this.walletManager.GetAccounts(currWalletName))
                {
                    foreach (var walletAddress in currAccount.ExternalAddresses)
                    {
                        if (walletAddress.Address.ToString().Equals(address))
                        {
                            walletCombix = $"{currAccount.Name}/{currWalletName}";
                            walletsByAddressMap.TryAdd<string, string>(address, walletCombix);
                            hdAddressByAddressMap.TryAdd<string, HdAddress>(address, walletAddress);
                            return this.Json(ResultHelper.BuildResultResponse(walletCombix));
                        }
                    }
                }
            }

            //if this point is reached the address is not in any wallets
            throw new RPCException(RPCErrorCode.RPC_INVALID_ADDRESS_OR_KEY, "Wallet not initialized", null, false);
        }

        /// <summary>
        /// Generates the new wallet.
        /// </summary>
        /// <param name="walletName">Name of the wallet.</param>
        /// <param name="password">The password.</param>
        /// <returns>Return Mnemonic BIP39 format</returns>
        [ActionName("generatenewwallet")]
        public Mnemonic GenerateNewWallet(string walletName, string password)
        {
            var w = this.walletManager as WalletManager;
            return w.CreateWallet(password, walletName);
        }

        /// <summary>
        /// Gets the wallet.
        /// </summary>
        /// <param name="walletName">Name of the wallet.</param>
        /// <returns>HdAccount RPC format</returns>
        [ActionName("getwallet")]
        public HdAccount GetWallet(string walletName)
        {
            var w = this.walletManager;
            var wallet = w.GetWalletByName(walletName);
            return wallet.GetAccountsByCoinType((CoinType)this.network.Consensus.CoinType).ToArray().First();
        }

        /// <summary>
        /// Sends the money.
        /// </summary>
        /// <param name="hdAcccountName">Name of the hd acccount.</param>
        /// <param name="walletName">Name of the wallet.</param>
        /// <param name="targetAddress">The target address.</param>
        /// <param name="password">The password.</param>
        /// <param name="satoshi">The satoshi.</param>
        /// <returns>Transaction rpc format</returns>
        [ActionName("sendmoney")]
        public Transaction SendMoney(string hdAcccountName, string walletName, string targetAddress, string password, decimal satoshi)
        {
            var transaction = this.FullNode.NodeService<IWalletTransactionHandler>() as WalletTransactionHandler;
            var w = this.walletManager as WalletManager;

            var isValid = false;
            if (BitcoinPubKeyAddress.IsValid(targetAddress, ref this.Network))
            {
                isValid = true;
            }
            else if (BitcoinScriptAddress.IsValid(targetAddress, ref this.Network))
            {
                isValid = true;
            }

            if (isValid)
            {
                var walletReference = new WalletAccountReference()
                {
                    AccountName = hdAcccountName,
                    WalletName = walletName
                };

                var context = new TransactionBuildContext(
                    walletReference,
                    new[]
                    {
                         new Recipient {
                             Amount = new Money(satoshi, MoneyUnit.Satoshi),
                             ScriptPubKey = BitcoinAddress.Create(targetAddress, this.Network).ScriptPubKey
                         }
                    }.ToList(), password)
                {
                    MinConfirmations = 0,
                    FeeType = FeeType.Medium,
                    Sign = true
                };

                var controller = this.FullNode.NodeService<WalletController>();

                var fundTransaction = transaction.BuildTransaction(context);
                controller.SendTransaction(new SendTransactionRequest(fundTransaction.ToHex()));

                return fundTransaction;
            }

            return null;
        }

        /// <summary>
        /// Sendmanies the specified hd acccount name.
        /// </summary>
        /// <param name="hdAcccountName">Name of the hd acccount.</param>
        /// <param name="toBitcoinAddresses">To bitcoin addresses.</param>
        /// <param name="minconf">The minconf.</param>
        /// <param name="password">The password.</param>
        /// <returns>uint256 rpc format</returns>
        /// <exception cref="ArgumentNullException">
        /// hdAcccountName
        /// or
        /// toBitcoinAddresses
        /// </exception>
        [ActionName("sendmany")]
        public uint256 Sendmany(string hdAcccountName, string toBitcoinAddresses, int minconf, string password)
        {
            string acccountName = "";
            string walletName = "";
            if (string.IsNullOrEmpty(hdAcccountName))
            {
                throw new ArgumentNullException("hdAcccountName");
            }
            if (string.IsNullOrEmpty(toBitcoinAddresses))
            {
                throw new ArgumentNullException("toBitcoinAddresses");
            }
            if (hdAcccountName.Contains("/"))
            {
                acccountName = hdAcccountName.Substring(0, hdAcccountName.IndexOf("/"));
                walletName = hdAcccountName.Substring(hdAcccountName.IndexOf("/")+1);//, hdAcccountName.Length - hdAcccountName.IndexOf("/")
            }
           

            var transaction = this.FullNode.NodeService<IWalletTransactionHandler>() as WalletTransactionHandler;
            var w = this.walletManager as WalletManager;
            var walletReference = new WalletAccountReference(walletName, acccountName);
            Dictionary<string, decimal> toBitcoinAddress = JsonConvert.DeserializeObject<Dictionary<string, decimal>>(toBitcoinAddresses);

            List<Recipient> recipients = new List<Recipient>();
            foreach (var item in toBitcoinAddress)
            {
                recipients.Add(new Recipient
                {
                    Amount = new Money(item.Value, MoneyUnit.BTR),
                    ScriptPubKey = BitcoinAddress.Create(item.Key, this.Network).ScriptPubKey
                });
            };

            var context = new TransactionBuildContext(walletReference, recipients, password)
            {
                MinConfirmations = minconf,
                FeeType = FeeType.Medium,
                Sign = true
            };

            var controller = this.FullNode.NodeService<WalletController>();

            var fundTransaction = transaction.BuildTransaction(context);
            controller.SendTransaction(new SendTransactionRequest(fundTransaction.ToHex()));

            return fundTransaction.GetHash();
        }

        /// <summary>
        /// Retrieves the history of a wallet.
        /// </summary>
        /// <param name="walletName">walletName</param>
        /// <param name="hdAcccountName">The hdAcccountName. (Optional) default = "account 0"</param>
        /// <returns>WalletHistoryModel rpc format</returns>
        [ActionName("gethistory")]
        [ActionDescription("Returns a wallet (only local transactions) history.")]
        public IActionResult GetHistory(string walletName, string hdAcccountName)
        {
            Guard.NotNull(walletName, nameof(walletName));
            if (String.IsNullOrEmpty(hdAcccountName)) {
                hdAcccountName = DEFAULT_ACCOUNT_NAME;
            }

            try
            {
                WalletHistoryModel model = new WalletHistoryModel();

                // Get a list of all the transactions found in an account (or in a wallet if no account is specified), with the addresses associated with them.
                IEnumerable<AccountHistory> accountsHistory = this.walletManager.GetHistory(walletName, hdAcccountName);

                var feeRate = new FeeRate(this.nodeSettings.MinTxFeeRate.FeePerK);
                var blockManager = this.FullNode.NodeService<BlockStoreManager>();

                foreach (var accountHistory in accountsHistory)
                {
                    List<TransactionItemModel> transactionItems = new List<TransactionItemModel>();

                    List<FlatHistory> items = accountHistory.History.OrderByDescending(o => o.Transaction.CreationTime).Take(200).ToList();

                    // Represents a sublist containing only the transactions that have already been spent.
                    List<FlatHistory> spendingDetails = items.Where(t => t.Transaction.SpendingDetails != null).ToList();

                    // Represents a sublist of transactions associated with receive addresses + a sublist of already spent transactions associated with change addresses.
                    // In effect, we filter out 'change' transactions that are not spent, as we don't want to show these in the history.
                    List<FlatHistory> history = items.Where(t => !t.Address.IsChangeAddress() || (t.Address.IsChangeAddress() && !t.Transaction.IsSpendable())).ToList();

                    // Represents a sublist of 'change' transactions.
                    List<FlatHistory> allchange = items.Where(t => t.Address.IsChangeAddress()).ToList();

                    foreach (var item in history)
                    {
                        var transaction = item.Transaction;
                        var address = item.Address;

                        // Create a record for a 'receive' transaction.
                        if (!address.IsChangeAddress())
                        {
                            // Add incoming fund transaction details.
                            TransactionItemModel receivedItem = new TransactionItemModel
                            {
                                Type = TransactionItemType.Received,
                                ToAddress = address.Address,
                                Amount = transaction.Amount,
                                Id = transaction.Id,
                                Timestamp = transaction.CreationTime,
                                ConfirmedInBlock = transaction.BlockHeight
                            };

                            transactionItems.Add(receivedItem);
                        }

                        // If this is a normal transaction (not staking) that has been spent, add outgoing fund transaction details.
                        if (transaction.SpendingDetails != null)
                        {
                            // Create a record for a 'send' transaction.
                            var spendingTransactionId = transaction.SpendingDetails.TransactionId;
                            TransactionItemModel sentItem = new TransactionItemModel
                            {
                                Type = TransactionItemType.Sent,
                                Id = spendingTransactionId,
                                Timestamp = transaction.SpendingDetails.CreationTime,
                                ConfirmedInBlock = transaction.SpendingDetails.BlockHeight,
                                Amount = Money.Zero
                            };

                            // If this 'send' transaction has made some external payments, i.e the funds were not sent to another address in the wallet.
                            if (transaction.SpendingDetails.Payments != null)
                            {
                                sentItem.Payments = new List<PaymentDetailModel>();
                                foreach (var payment in transaction.SpendingDetails.Payments)
                                {
                                    sentItem.Payments.Add(new PaymentDetailModel
                                    {
                                        DestinationAddress = payment.DestinationAddress,
                                        Amount = payment.Amount
                                    });

                                    sentItem.Amount += payment.Amount;
                                }
                            }

                            // Get the change address for this spending transaction.
                            var changeAddress = allchange.FirstOrDefault(a => a.Transaction.Id == spendingTransactionId);

                            // Find all the spending details containing the spending transaction id and aggregate the sums.
                            // This is our best shot at finding the total value of inputs for this transaction.
                            var inputsAmount = new Money(spendingDetails.Where(t => t.Transaction.SpendingDetails.TransactionId == spendingTransactionId).Sum(t => t.Transaction.Amount));

                            //calculation of fee based on transaction
                            var currentTransaction = blockManager.BlockRepository.GetTrxAsync(spendingTransactionId).GetAwaiter().GetResult();
                            if (currentTransaction != null) sentItem.Fee = feeRate.GetFee(currentTransaction);

                            // The fee is calculated as follows: funds in utxo - amount spent - amount sent as change.
                            //OLD: sentItem.Fee = inputsAmount - sentItem.Amount - (changeAddress == null ? 0 : changeAddress.Transaction.Amount);

                            // Mined coins add more coins to the total out.
                            // That makes the fee negative. If that's the case ignore the fee.
                            if (sentItem.Fee < 0)
                                sentItem.Fee = 0;

                            if (!transactionItems.Contains(sentItem, new SentTransactionItemModelComparer()))
                            {
                                transactionItems.Add(sentItem);
                            }
                        }
                    }

                    model.AccountsHistoryModel.Add(new AccountHistoryModel
                    {
                        TransactionsHistory = transactionItems.OrderByDescending(t => t.Timestamp).ToList(),
                        Name = accountHistory.Account.Name,
                        CoinType = (CoinType)this.network.Consensus.CoinType,
                        HdPath = accountHistory.Account.HdPath
                    });
                }

                return this.Json(ResultHelper.BuildResultResponse(model));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Stops current wallet rescan triggered by an RPC call, e.g. by an importprivkey call.
        /// </summary>
        /// <returns>True/False</returns>
        [ActionName("abortrescan")]
        [ActionDescription("Stops current wallet rescan triggered by an RPC call, e.g. by an importprivkey call.")]
        public IActionResult AbortReScan()
        {
            try
            {
                inRescan = false;

                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Rescan the local blockchain for wallet related transactions.
        /// </summary>
        /// <param name="startHeight">(int, optional) The start height.</param>
        /// <param name="stopHeight">(int, optional) The last block height that should be scanned</param>
        /// <returns>Start height and stopped height</returns>
        [ActionName("rescanblockchain")]
        [ActionDescription("Rescan the local blockchain for wallet related transactions.")]
        public IActionResult RescanBlockChain(int? startHeight, int? stopHeight)
        {
            try
            {
                var chainRepository = this.FullNode.NodeService<ConcurrentChain>();
                var blockStoreManager = this.FullNode.NodeService<BlockStoreManager>();

                // If there is no wallet yet, raise error
                if (!this.walletManager.Wallets.Any())
                {
                    throw new RPCServerException(RPCErrorCode.RPC_WALLET_ERROR, "No wallets");
                }

                if (!startHeight.HasValue) startHeight = 0;
                if (!stopHeight.HasValue) stopHeight = chainRepository.Height;

                if (startHeight > stopHeight)
                {
                    throw new ArgumentNullException("startHeight", "Start height cant be higher then stop height");
                }
                if (stopHeight <= 0)
                {
                    throw new ArgumentNullException("stopHeight");
                }
                if (startHeight > chainRepository.Height)
                {
                    throw new ArgumentNullException("startHeight", "Chain is shorter");
                }
                if (stopHeight > chainRepository.Height)
                {
                    throw new ArgumentNullException("stopHeight", "Chain is shorter");
                }

                var result = new RescanBlockChainModel();
                result.StartHeight = startHeight.Value;
                inRescan = true;

                Console.WriteLine(string.Format("Start rescan at {0}", result.StartHeight));

                lock (this.walletManager.GetLock())
                {
                    for (int i = startHeight.Value; i <= stopHeight; i++)
                    {
                        if (!inRescan) break;

                        Console.WriteLine(string.Format("Scanning {0}", i));

                        var chainedHeader = chainRepository.GetBlock(i);
                        var block = blockStoreManager.BlockRepository.GetAsync(chainedHeader.HashBlock).Result;

                        var walletUpdated = false;

                        foreach (Transaction transaction in block.Transactions)
                        {
                            bool trxFound = this.walletManager.ProcessTransaction(transaction, chainedHeader.Height, block, true);
                            if (trxFound)
                            {
                                walletUpdated = true;
                            }
                        }

                        // Update the wallets with the last processed block height.
                        // It's important that updating the height happens after the block processing is complete,
                        // as if the node is stopped, on re-opening it will start updating from the previous height.
                        foreach (var wallet in this.walletManager.Wallets)
                        {
                            foreach (AccountRoot accountRoot in wallet.AccountsRoot.Where(a => a.CoinType == (CoinType)this.network.Consensus.CoinType))
                            {
                                if (accountRoot.LastBlockSyncedHeight < i)
                                {
                                    accountRoot.LastBlockSyncedHeight = chainedHeader.Height;
                                    accountRoot.LastBlockSyncedHash = chainedHeader.HashBlock;
                                }
                            }
                        }

                        if (walletUpdated)
                        {
                            this.walletManager.SaveWallets();
                        }

                        result.StopHeight = i;
                    }
                }

                Console.WriteLine(string.Format("End rescan at {0}", result.StopHeight));

                return this.Json(ResultHelper.BuildResultResponse(result));
            }
            catch (Exception e)
            {
                inRescan = false;
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// The destination directory or file
        /// </summary>
        /// <param name="walletName">(string) Wallet Name to backup</param>
        /// <param name="destination">(string) The destination file</param>
        /// <returns>Return true if it is done</returns>
        [ActionName("backupwallet")]
        [ActionDescription("The destination directory or file")]
        public IActionResult BackupWallet(string walletName, string destination)
        {
            try
            {
                if (string.IsNullOrEmpty(walletName))
                {
                    throw new ArgumentNullException("walletNamexid");
                }

                if (string.IsNullOrEmpty(destination))
                {
                    throw new ArgumentNullException("destination");
                }

                var fileStorage = new FileStorage<Wallet>(destination);
                var wallet = this.walletManager.GetWalletByName(walletName);

                lock (this.walletManager.GetLock())
                {
                    fileStorage.SaveToFile(wallet, $"{wallet.Name}.{this.walletManager.GetWalletFileExtension()}.bak");
                }

                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Dumps all wallet keys in a human-readable format to a server-side file. This does not allow overwriting existing files. Imported scripts are included in the dumpfile, but corresponding BIP173 addresses, etc.may not be added automatically by importwallet.        Note that if your wallet contains keys which are not derived from your HD seed(e.g.imported keys), these are not covered by only backing up the seed itself, and must be backed up too(e.g.ensure you back up the whole dumpfile).
        /// </summary>
        /// <param name="walletName">(string) Wallet Name to backup</param>
        /// <param name="filename">(string) The filename with path relative to config folder</param>
        /// <returns>The filename with full absolute path</returns>
        [ActionName("dumpwallet")]
        [ActionDescription("Dumps all wallet keys in a human-readable format to a server-side file. This does not allow overwriting existing files. Imported scripts are included in the dumpfile, but corresponding BIP173 addresses, etc.may not be added automatically by importwallet.        Note that if your wallet contains keys which are not derived from your HD seed(e.g.imported keys), these are not covered by only backing up the seed itself, and must be backed up too(e.g.ensure you back up the whole dumpfile).")]
        public IActionResult DumpWallet(string walletName, string filename)
        {
            try
            {
                if (string.IsNullOrEmpty(walletName))
                {
                    throw new ArgumentNullException("walletNamexid");
                }

                if (string.IsNullOrEmpty(filename))
                {
                    throw new ArgumentNullException("filename");
                }

                var wallet = this.walletManager.GetWalletByName(walletName);
                var fullFileName = ((WalletManager)this.walletManager).FileStorage.FolderPath + filename;

                Directory.CreateDirectory(fullFileName);

                var chainRepository = this.FullNode.NodeService<ConcurrentChain>();
                var chainedHeader = chainRepository.GetBlock(chainRepository.Height);

                var fileContent = new StringBuilder();
                fileContent.AppendLine("# Wallet dump created by BitCoin Rhodium" + Assembly.GetEntryAssembly().GetName().Version.ToString());
                fileContent.AppendLine("# * Created on " + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssK"));
                fileContent.AppendLine("# * Best block at time of backup was " + chainRepository.Height + " ," + chainedHeader.HashBlock);
                fileContent.AppendLine("# * mined on" + Utils.UnixTimeToDateTime(chainedHeader.Header.Time).DateTime.ToString("yyyy-MM-ddTHH:mm:ssK"));
                fileContent.AppendLine(string.Empty);

                var addresses = wallet.GetAllAddressesByCoinType((CoinType)this.network.Consensus.CoinType);

                foreach (var item in addresses)
                {
                    fileContent.AppendLine("# addr=" + item.Address + " " + item.HdPath);
                }

                fileContent.AppendLine("# End of dump");

                System.IO.File.WriteAllText(fullFileName, JsonConvert.SerializeObject(wallet, Formatting.Indented));

                var fileInfo = new FileInfo(filename);
                return this.Json(ResultHelper.BuildResultResponse(fileInfo.FullName));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("getaccountaddress")]
        [ActionDescription("")]
        public IActionResult GetAccountAddress()
        {
            try
            {
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("getaddressesbyaccount")]
        [ActionDescription("")]
        public IActionResult GetAddressesByAccount()
        {
            try
            {
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("getnewaddress")]
        [ActionDescription("")]
        public IActionResult GetNewAddress()
        {
            try
            {
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("getrawchangeaddress")]
        [ActionDescription("")]
        public IActionResult GetRawChangeAddress()
        {
            try
            {
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("getreceivedbyaccount")]
        [ActionDescription("")]
        public IActionResult GetReceivedByAccount()
        {
            try
            {
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("getreceivedbyaddress")]
        [ActionDescription("")]
        public IActionResult GetReceivedByAddress()
        {
            try
            {
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("getunconfirmedbalance")]
        [ActionDescription("")]
        public IActionResult GetUnconfirmedBalance()
        {
            try
            {
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("getwalletinfo")]
        [ActionDescription("")]
        public IActionResult GetWalletInfo()
        {
            try
            {
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("importaddress")]
        [ActionDescription("")]
        public IActionResult ImportAddress()
        {
            try
            {
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("importmulti")]
        [ActionDescription("")]
        public IActionResult ImportMulti()
        {
            try
            {
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("importprivkey")]
        [ActionDescription("")]
        public IActionResult ImportPrivKey()
        {
            try
            {
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("importpubkey")]
        [ActionDescription("")]
        public IActionResult ImportPubKey()
        {
            try
            {
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("importwallet")]
        [ActionDescription("")]
        public IActionResult ImportWallet()
        {
            try
            {
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("keypoolrefill")]
        [ActionDescription("")]
        public IActionResult KeyPoolReFill()
        {
            try
            {
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("listaccounts")]
        [ActionDescription("")]
        public IActionResult ListAccounts()
        {
            try
            {
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("listaddressgroupings")]
        [ActionDescription("")]
        public IActionResult ListAddressGroupings()
        {
            try
            {
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("listlockunspent")]
        [ActionDescription("")]
        public IActionResult ListLockUnspent()
        {
            try
            {
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("listreceivedbyaccount")]
        [ActionDescription("")]
        public IActionResult ListReceivedByAccount()
        {
            try
            {
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("listreceivedbyaddress")]
        [ActionDescription("")]
        public IActionResult ListReceivedByAddress()
        {
            try
            {
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("listsinceblock")]
        [ActionDescription("")]
        public IActionResult ListSinceBlock()
        {
            try
            {
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("listtransactions")]
        [ActionDescription("")]
        public IActionResult ListTransactions()
        {
            try
            {
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("listunspent")]
        [ActionDescription("")]
        public IActionResult ListUnspent()
        {
            try
            {
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("sendfrom")]
        [ActionDescription("")]
        public IActionResult SendFrom()
        {
            try
            {
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }
    }
}
