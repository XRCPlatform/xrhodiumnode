using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using BRhodium.Node.Configuration;
using BRhodium.Bitcoin.Features.Wallet.Broadcasting;
using BRhodium.Bitcoin.Features.Wallet.Interfaces;
using BRhodium.Node.Utilities;
using System.Text;
using BRhodium.Bitcoin.Features.Consensus.Models;
using Microsoft.AspNetCore.DataProtection;

[assembly: InternalsVisibleTo("BRhodium.Bitcoin.Features.Wallet.Tests")]

namespace BRhodium.Bitcoin.Features.Wallet
{
    /// <summary>
    /// A manager providing operations on wallets.
    /// </summary>
    public class WalletManager : IWalletManager
    {
        // <summary>As per RPC method definition this should be the max allowable expiry duration.</summary>
        private const int MaxWalletUnlockDurationInSeconds = 60 * 60 * 5;

        /// <summary>Size of the buffer of unused addresses maintained in an account. </summary>
        private const int UnusedAddressesBuffer = 20;

        /// <summary>Quantity of accounts created in a wallet file when a wallet is restored.</summary>
        private const int WalletRecoveryAccountsCount = 1;

        /// <summary>Quantity of accounts created in a wallet file when a wallet is created.</summary>
        private const int WalletCreationAccountsCount = 1;

        /// <summary>File extension for wallet files.</summary>
        private const string WalletFileExtension = "wallet.json";

        /// <summary>Timer for saving wallet files to the file system.</summary>
        private const int WalletSavetimeIntervalInMinutes = 5;

        /// <summary>Default account name </summary>
        private const string DefaultAccount = "account 0";

        /// <summary>
        /// A lock object that protects access to the <see cref="Wallet"/>.
        /// Any of the collections inside Wallet must be synchronized using this lock.
        /// </summary>
        private readonly object lockObject;

        /// <summary>The async loop we need to wait upon before we can shut down this manager.</summary>
        private IAsyncLoop asyncLoop;

        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        /// <summary>Gets the list of wallets.</summary>
        public ConcurrentDictionary<string,Wallet> Wallets { get; }

        /// <summary>The type of coin used in this manager.</summary>
        private readonly CoinType coinType;

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        private readonly Network network;

        /// <summary>The chain of headers.</summary>
        private readonly ConcurrentChain chain;

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>An object capable of storing <see cref="Wallet"/>s to the file system.</summary>
        public readonly FileStorage<Wallet> FileStorage;
        public readonly DBreezeStorage<Wallet> DBreezeStorage;

        /// <summary>The broadcast manager.</summary>
        private readonly IBroadcasterManager broadcasterManager;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>The settings for the wallet feature.</summary>
        private readonly WalletSettings walletSettings;

        private WalletSecrets walletSecrets;
        IDataProtector _protector;

        /// <summary>
        ///  Makes the wallet settings public for other functions.
        /// </summary>
        public WalletSettings WalletSettings {
            get
            {
                return this.walletSettings;
            }
        }

        /// <summary>
        /// Store and manage wallet secrets.
        /// </summary>
        /// <value></value>
        public WalletSecrets WalletSecrets
        {
            get
            {
                return this.walletSecrets;
            }
        }

        public uint256 WalletTipHash { get; set; }

        /// <summary>Memory locked unspendable transaction parts (tx hash, index vount)</summary>
        public ConcurrentDictionary<string, int> LockedTxOut { get; set; }

        // In order to allow faster look-ups of transactions affecting the wallets' addresses,
        // we keep a couple of objects in memory:
        // 1. the list of unspent outputs for checking whether inputs from a transaction are being spent by our wallet and
        // 2. the list of addresses contained in our wallet for checking whether a transaction is being paid to the wallet.
        private Dictionary<OutPoint, TransactionData> outpointLookup;
        internal Dictionary<Script, HdAddress> keysLookup;
        internal Dictionary<Script, string> keysLookupToWalletName;

        public WalletManager(
            ILoggerFactory loggerFactory,
            Network network,
            ConcurrentChain chain,
            NodeSettings settings,
            WalletSettings walletSettings,
            DataFolder dataFolder,
            IWalletFeePolicy walletFeePolicy,
            IAsyncLoopFactory asyncLoopFactory,
            INodeLifetime nodeLifetime,
            IDateTimeProvider dateTimeProvider,
            IBroadcasterManager broadcasterManager = null,  // no need to know about transactions the node will broadcast to.
            IDataProtectionProvider provider = null
            )
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(settings, nameof(settings));
            Guard.NotNull(walletSettings, nameof(walletSettings));
            Guard.NotNull(dataFolder, nameof(dataFolder));
            Guard.NotNull(walletFeePolicy, nameof(walletFeePolicy));
            Guard.NotNull(asyncLoopFactory, nameof(asyncLoopFactory));
            Guard.NotNull(nodeLifetime, nameof(nodeLifetime));

            this.walletSettings = walletSettings;
            this.lockObject = new object();

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.Wallets = new ConcurrentDictionary<string,Wallet>();

            this.network = network;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.chain = chain;
            this.asyncLoopFactory = asyncLoopFactory;
            this.nodeLifetime = nodeLifetime;
            this.FileStorage = new FileStorage<Wallet>(dataFolder.WalletPath);
            this.DBreezeStorage = new DBreezeStorage<Wallet>(dataFolder.WalletPath, "Wallets");
            this.broadcasterManager = broadcasterManager;
            this.dateTimeProvider = dateTimeProvider;

            // register events
            if (this.broadcasterManager != null)
            {
                this.broadcasterManager.TransactionStateChanged += this.BroadcasterManager_TransactionStateChanged;
            }

            this.keysLookup = new Dictionary<Script, HdAddress>();
            this.keysLookupToWalletName = new Dictionary<Script, string>();
            this.outpointLookup = new Dictionary<OutPoint, TransactionData>();

            this.walletSecrets = new WalletSecrets(provider);
        }

        private void BroadcasterManager_TransactionStateChanged(object sender, TransactionBroadcastEntry transactionEntry)
        {
            this.logger.LogTrace("()");

            if (string.IsNullOrEmpty(transactionEntry.ErrorMessage))
            {
                this.ProcessTransaction(transactionEntry.Transaction, null, null, transactionEntry.State == State.Propagated);
            }
            else
            {
                this.logger.LogTrace("Exception occurred: {0}", transactionEntry.ErrorMessage);
                this.logger.LogTrace("(-)[EXCEPTION]");
            }

            this.logger.LogTrace("(-)");
        }

        public void Start()
        {
            this.logger.LogTrace("()");

            // Find wallets and load them in memory.
            IEnumerable<Wallet> wallets = this.DBreezeStorage.LoadAll(this.network);

            if ((wallets == null) || (wallets.Count() == 0))
            {
                wallets = this.FileStorage.LoadByFileExtension("wallet.json");
            }

            foreach (Wallet wallet in wallets)
                this.Wallets.AddOrReplace(wallet.Name, wallet);

            // Load data in memory for faster lookups.
            this.LoadKeysLookupLock();

            // Find the last chain block received by the wallet manager.
            this.WalletTipHash = this.LastReceivedBlockHash();

            // Save the wallets file every 5 minutes to help against crashes.
            this.asyncLoop = this.asyncLoopFactory.Run("wallet persist job", token =>
            {
                this.logger.LogTrace("()");

                this.SaveWallets();
                this.logger.LogInformation("Wallets saved to file at {0}.", this.dateTimeProvider.GetUtcNow());

                this.logger.LogTrace("(-)");
                return Task.CompletedTask;
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpan.FromMinutes(WalletSavetimeIntervalInMinutes),
            startAfter: TimeSpan.FromMinutes(WalletSavetimeIntervalInMinutes));

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void Stop()
        {
            this.logger.LogTrace("()");

            if (this.broadcasterManager != null)
                this.broadcasterManager.TransactionStateChanged -= this.BroadcasterManager_TransactionStateChanged;

            this.asyncLoop?.Dispose();
            this.SaveWallets();

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public Mnemonic CreateWallet(string password, string name, string passphrase = null, string mnemonicList = null)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotEmpty(name, nameof(name));
            this.logger.LogTrace("({0}:'{1}')", nameof(name), name);

            // For now the passphrase is set to be the password by default.
            if (passphrase == null)
                passphrase = password;

            passphrase = Convert.ToBase64String(Encoding.UTF8.GetBytes(passphrase));

            // Generate the root seed used to generate keys from a mnemonic picked at random
            // and a passphrase optionally provided by the user.
            Mnemonic mnemonic = string.IsNullOrEmpty(mnemonicList)
                ? new Mnemonic(Wordlist.English, WordCount.Twelve)
                : new Mnemonic(mnemonicList);
            ExtKey extendedKey = HdOperations.GetHdPrivateKey(mnemonic, passphrase);

            // Create a wallet file.
            string encryptedSeed = extendedKey.PrivateKey.GetEncryptedBitcoinSecret(password, this.network).ToWif();
            Wallet wallet = this.GenerateWalletFile(name, encryptedSeed, extendedKey.ChainCode);

            // Generate multiple accounts and addresses from the get-go.
            for (int i = 0; i < WalletCreationAccountsCount; i++)
            {
                HdAccount account = wallet.AddNewAccount(password, this.coinType, this.dateTimeProvider.GetTimeOffset());
                IEnumerable<HdAddress> newReceivingAddresses = account.CreateAddresses(this.network, UnusedAddressesBuffer);
                IEnumerable<HdAddress> newChangeAddresses = account.CreateAddresses(this.network, UnusedAddressesBuffer, true);
                this.UpdateKeysLookupLock(newReceivingAddresses.Concat(newChangeAddresses), wallet.Name);
            }

            // If the chain is downloaded, we set the height of the newly created wallet to it.
            // However, if the chain is still downloading when the user creates a wallet,
            // we wait until it is downloaded in order to set it. Otherwise, the height of the wallet will be the height of the chain at that moment.
            if (this.chain.IsDownloaded())
            {
                this.UpdateLastBlockSyncedHeight(wallet, this.chain.Tip);
            }
            else
            {
                this.UpdateWhenChainDownloaded(new[] { wallet }, DateTime.Now);
            }

            // Save the changes to the file and add addresses to be tracked.
            this.SaveWallet(wallet);
            this.Load(wallet);

            this.logger.LogTrace("(-)");
            return mnemonic;
        }

        /// <inheritdoc />
        public Wallet LoadWallet(string password, string name)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotEmpty(name, nameof(name));
            this.logger.LogTrace("({0}:'{1}')", nameof(name), name);

            // Load the file from the local system.
            Wallet wallet = this.DBreezeStorage.LoadByKey(name, this.network);
            if (wallet == null)
            {
                this.logger.LogTrace("Wallet does not exist in breeze db: {0}",name);
                this.logger.LogTrace("(-)[EXCEPTION]");
                throw new WalletDoesNotExistException($"Wallet {name} does not exist.");
            }
            // Check the password.
            try
            {
                Key.Parse(wallet.EncryptedSeed, password, wallet.Network);
            }
            catch (Exception ex)
            {
                this.logger.LogTrace("Exception occurred: {0}", ex.ToString());
                this.logger.LogTrace("(-)[EXCEPTION]");
                throw new SecurityException(ex.Message);
            }

            this.Load(wallet);

            this.logger.LogTrace("(-)");
            return wallet;
        }

        /// <inheritdoc />
        public Wallet RecoverWallet(string password, string name, string mnemonic, DateTime creationTime, string passphrase = null)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotEmpty(name, nameof(name));
            Guard.NotEmpty(mnemonic, nameof(mnemonic));
            this.logger.LogTrace("({0}:'{1}')", nameof(name), name);

            // For now the passphrase is set to be the password by default.
            if (passphrase == null)
                passphrase = password;

            var date = DateTimeOffset.FromUnixTimeSeconds(1539810380).DateTime;
            if (creationTime > date) passphrase = Convert.ToBase64String(Encoding.UTF8.GetBytes(passphrase));

            // Generate the root seed used to generate keys.
            ExtKey extendedKey;
            try
            {
                extendedKey = HdOperations.GetHdPrivateKey(mnemonic, passphrase);
            }
            catch (NotSupportedException ex)
            {
                this.logger.LogTrace("Exception occurred: {0}", ex.ToString());
                this.logger.LogTrace("(-)[EXCEPTION]");

                if (ex.Message == "Unknown")
                    throw new WalletException("Please make sure you enter valid mnemonic words.");

                throw;
            }

            // Create a wallet file.
            string encryptedSeed = extendedKey.PrivateKey.GetEncryptedBitcoinSecret(password, this.network).ToWif();
            Wallet wallet = this.GenerateWalletFile(name, encryptedSeed, extendedKey.ChainCode, creationTime);

            // Generate multiple accounts and addresses from the get-go.
            for (int i = 0; i < WalletRecoveryAccountsCount; i++)
            {
                HdAccount account = wallet.AddNewAccount(password, this.coinType, this.dateTimeProvider.GetTimeOffset());
                IEnumerable<HdAddress> newReceivingAddresses = account.CreateAddresses(this.network, UnusedAddressesBuffer);
                IEnumerable<HdAddress> newChangeAddresses = account.CreateAddresses(this.network, UnusedAddressesBuffer, true);
                this.UpdateKeysLookupLock(newReceivingAddresses.Concat(newChangeAddresses), wallet.Name);
            }

            // If the chain is downloaded, we set the height of the recovered wallet to that of the recovery date.
            // However, if the chain is still downloading when the user restores a wallet,
            // we wait until it is downloaded in order to set it. Otherwise, the height of the wallet may not be known.
            if (this.chain.IsDownloaded())
            {
                int blockSyncStart = this.chain.GetHeightAtTime(creationTime);
                this.UpdateLastBlockSyncedHeight(wallet, this.chain.GetBlock(blockSyncStart));
            }
            else
            {
                this.UpdateWhenChainDownloaded(new[] { wallet }, creationTime);
            }

            // Save the changes to the file and add addresses to be tracked.
            this.SaveWallet(wallet);
            this.Load(wallet);

            this.logger.LogTrace("(-)");
            return wallet;
        }

        /// <inheritdoc />
        public HdAccount GetUnusedAccount(string walletName, string password)
        {
            Guard.NotEmpty(walletName, nameof(walletName));
            Guard.NotEmpty(password, nameof(password));
            this.logger.LogTrace("({0}:'{1}')", nameof(walletName), walletName);

            Wallet wallet = this.GetWalletByName(walletName);

            HdAccount res = this.GetUnusedAccount(wallet, password);
            this.logger.LogTrace("(-)");
            return res;
        }

        /// <inheritdoc />
        public HdAccount GetUnusedAccount(Wallet wallet, string password)
        {
            Guard.NotNull(wallet, nameof(wallet));
            Guard.NotEmpty(password, nameof(password));
            this.logger.LogTrace("({0}:'{1}')", nameof(wallet), wallet.Name);

            HdAccount account;

            lock (this.lockObject)
            {
                account = wallet.GetFirstUnusedAccount(this.coinType);

                if (account != null)
                {
                    return account;
                }

                // No unused account was found, create a new one.
                account = wallet.AddNewAccount(password, this.coinType, this.dateTimeProvider.GetTimeOffset());
            }

            // Save the changes to the file.
            this.SaveWallet(wallet);

            this.logger.LogTrace("(-)");
            return account;
        }

        public string GetExtPubKey(WalletAccountReference accountReference)
        {
            Guard.NotNull(accountReference, nameof(accountReference));
            this.logger.LogTrace("({0}:'{1}')", nameof(accountReference), accountReference);

            Wallet wallet = this.GetWalletByName(accountReference.WalletName);

            string res = null;
            lock (this.lockObject)
            {
                // Get the account.
                HdAccount account = wallet.GetAccountByCoinType(accountReference.AccountName, this.coinType);
                res = account.ExtendedPubKey;
            }

            this.logger.LogTrace("(-):'{0}'", res);
            return res;
        }

        public object GetLock()
        {
            return this.lockObject;
        }

        /// <inheritdoc />
        public HdAddress GetUnusedAddress(WalletAccountReference accountReference)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(accountReference), accountReference);

            HdAddress res = this.GetUnusedAddresses(accountReference, 1).Single();

            this.logger.LogTrace("(-)");
            return res;
        }

        /// <inheritdoc />
        public HdAddress GetNewAddress(WalletAccountReference accountReference)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(accountReference), accountReference);

            HdAddress res = this.GetNewAddresses(accountReference, 1).Single();

            this.logger.LogTrace("(-)");
            return res;
        }

        /// <inheritdoc />
        public HdAddress GetUnusedChangeAddress(WalletAccountReference accountReference)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(accountReference), accountReference);

            HdAddress res = this.GetUnusedAddresses(accountReference, 1, true).Single();

            this.logger.LogTrace("(-)");
            return res;
        }

        /// <inheritdoc />
        public IEnumerable<HdAddress> GetUnusedAddresses(WalletAccountReference accountReference, int count, bool isChange = false)
        {
            Guard.NotNull(accountReference, nameof(accountReference));
            Guard.Assert(count > 0);
            this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(accountReference), accountReference, nameof(count), count);

            Wallet wallet = this.GetWalletByName(accountReference.WalletName);

            return GetUnusedAddresses(wallet, count, isChange, accountReference.AccountName);
        }

        /// <inheritdoc />
        public IEnumerable<HdAddress> GetNewAddresses(WalletAccountReference accountReference, int count, bool isChange = false)
        {
            Guard.NotNull(accountReference, nameof(accountReference));
            Guard.Assert(count > 0);
            this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(accountReference), accountReference, nameof(count), count);

            Wallet wallet = this.GetWalletByName(accountReference.WalletName);

            return GetNewAddresses(wallet, count, isChange, accountReference.AccountName);
        }

        /// <inheritdoc />
        public IEnumerable<HdAddress> GetUnusedAddresses(Wallet wallet, int count, bool isChange = false, string accountName = null)
        {
            Guard.Assert(count > 0);

            bool generated = false;
            IEnumerable<HdAddress> addresses;

            if (accountName == null)
            {
                var accountReference = wallet.AccountsRoot.Single(a => a.CoinType == (CoinType)this.network.Consensus.CoinType);
                accountName = accountReference.Accounts.First().Name;
            }

            lock (this.lockObject)
            {
                // Get the account.
                HdAccount account = wallet.GetAccountByCoinType(accountName, this.coinType);

                List<HdAddress> unusedAddresses = isChange ?
                    account.InternalAddresses.Where(acc => !acc.Transactions.Any()).ToList() :
                    account.ExternalAddresses.Where(acc => !acc.Transactions.Any()).ToList();

                int diff = unusedAddresses.Count - count;
                List<HdAddress> newAddresses = new List<HdAddress>();
                if (diff < 0)
                {
                    newAddresses = account.CreateAddresses(this.network, Math.Abs(diff), isChange: isChange).ToList();
                    this.UpdateKeysLookupLock(newAddresses, wallet.Name);
                    generated = true;
                }

                addresses = unusedAddresses.Concat(newAddresses).OrderBy(x => x.Index).Take(count);
            }

            if (generated)
            {
                // Save the changes to the file.
                this.SaveWallet(wallet);
            }

            this.logger.LogTrace("(-)");
            return addresses;
        }

        /// <inheritdoc />
        public IEnumerable<HdAddress> GetNewAddresses(Wallet wallet, int count, bool isChange = false, string accountName = null)
        {
            Guard.Assert(count > 0);

            IEnumerable<HdAddress> addresses;

            if (accountName == null)
            {
                var accountReference = wallet.AccountsRoot.Single(a => a.CoinType == (CoinType)this.network.Consensus.CoinType);
                accountName = accountReference.Accounts.First().Name;
            }

            lock (this.lockObject)
            {
                HdAccount account = wallet.GetAccountByCoinType(accountName, this.coinType);

                List<HdAddress> unusedAddresses = isChange ?
                    account.InternalAddresses.Where(acc => !acc.Transactions.Any()).ToList() :
                    account.ExternalAddresses.Where(acc => !acc.Transactions.Any()).ToList();

                List<HdAddress> newAddresses = new List<HdAddress>();
                newAddresses = account.CreateAddresses(this.network, count, isChange: isChange).ToList();
                this.UpdateKeysLookupLock(newAddresses, wallet.Name);

                addresses = unusedAddresses.Concat(newAddresses).OrderBy(x => x.Index).ToList();
            }

            // Save the changes to the file.
            this.SaveWallet(wallet);

            this.logger.LogTrace("(-)");
            return addresses;
        }

        /// <inheritdoc />
        public (string folderPath, IEnumerable<string>) GetWalletsFiles()
        {
            return (this.DBreezeStorage.FolderPath, this.DBreezeStorage.GetDatabaseKeys());
        }

        /// <inheritdoc />
        public IEnumerable<AccountHistory> GetHistory(string walletName, string accountName = null)
        {
            Guard.NotEmpty(walletName, nameof(walletName));
            this.logger.LogTrace("({0}:'{1}', {2}:'{3}')", nameof(walletName), walletName, nameof(accountName), accountName);

            // In order to calculate the fee properly we need to retrieve all the transactions with spending details.
            Wallet wallet = this.GetWalletByName(walletName);

            List<AccountHistory> accountsHistory = new List<AccountHistory>();

            lock (this.lockObject)
            {
                List<HdAccount> accounts = new List<HdAccount>();
                if (!string.IsNullOrEmpty(accountName))
                {
                    accounts.Add(wallet.GetAccountByCoinType(accountName, this.coinType));
                }
                else
                {
                    accounts.AddRange(wallet.GetAccountsByCoinType(this.coinType));
                }

                foreach (var account in accounts)
                {
                    accountsHistory.Add(this.GetHistory(account));
                }
            }

            this.logger.LogTrace("(-):*.Count={0}", accountsHistory.Count());
            return accountsHistory;
        }

        /// <inheritdoc />
        public AccountHistory GetHistory(HdAccount account)
        {
            Guard.NotNull(account, nameof(account));
            FlatHistory[] items;
            lock (this.lockObject)
            {
                // Get transactions contained in the account.
                items = account.GetCombinedAddresses()
                    .Where(a => a.Transactions.Any())
                    .SelectMany(s => s.Transactions.Select(t => new FlatHistory { Address = s, Transaction = t })).ToArray();
            }

            this.logger.LogTrace("(-):*.Count={0}", items.Count());
            return new AccountHistory { Account = account, History = items };
        }

        /// <inheritdoc />
        public IEnumerable<AccountBalance> GetBalances(string walletName, string accountName = null)
        {
            List<AccountBalance> balances = new List<AccountBalance>();

            lock (this.lockObject)
            {
                Wallet wallet = this.GetWalletByName(walletName);

                List<HdAccount> accounts = new List<HdAccount>();
                if (!string.IsNullOrEmpty(accountName))
                {
                    accounts.Add(wallet.GetAccountByCoinType(accountName, this.coinType));
                }
                else
                {
                    accounts.AddRange(wallet.GetAccountsByCoinType(this.coinType));
                }

                foreach (var account in accounts)
                {
                    (Money amountConfirmed, Money amountUnconfirmed, Money amountImmature) result = account.GetSpendableAmount(this.chain);

                    balances.Add(new AccountBalance
                    {
                        Account = account,
                        AmountConfirmed = result.amountConfirmed,
                        AmountUnconfirmed = result.amountUnconfirmed,
                        AmountImmature = result.amountImmature

                    });
                }
            }

            return balances;
        }

        /// <inheritdoc />
        public AddressBalance GetAddressBalance(string address)
        {
            return GetAddressBalance(address, null);
        }

        /// <inheritdoc />
        public AddressBalance GetAddressBalance(string address, string walletName = null)
        {
            Guard.NotEmpty(address, nameof(address));
            this.logger.LogTrace("({0}:'{1}')", nameof(address), address);

            AddressBalance balance = new AddressBalance
            {
                Address = address,
                CoinType = this.coinType
            };

            lock (this.lockObject)
            {
                foreach (Wallet wallet in this.Wallets.Values)
                {
                    if ((walletName != null) && (wallet.Name != walletName)) continue;

                    HdAddress hdAddress = wallet.GetAllAddressesByCoinType(this.coinType).FirstOrDefault(a => a.Address == address);
                    if (hdAddress == null) continue;

                    (Money amountConfirmed, Money amountUnconfirmed, Money amountImmature) result = hdAddress.GetSpendableAmount(this.chain);

                    balance.AmountConfirmed = result.amountConfirmed;
                    balance.AmountUnconfirmed = result.amountUnconfirmed;
                    balance.AmountImmature = result.amountImmature;
                    balance.Transactions = hdAddress.Transactions;

                    break;
                }
            }

            return balance;
        }

        /// <inheritdoc />
        public Wallet GetWallet(string walletName)
        {
            Guard.NotEmpty(walletName, nameof(walletName));
            this.logger.LogTrace("({0}:'{1}')", nameof(walletName), walletName);

            Wallet wallet = this.GetWalletByName(walletName);

            this.logger.LogTrace("(-)");
            return wallet;
        }

        /// <inheritdoc />
        public IEnumerable<HdAccount> GetAccounts(string walletName)
        {
            Guard.NotEmpty(walletName, nameof(walletName));
            this.logger.LogTrace("({0}:'{1}')", nameof(walletName), walletName);

            Wallet wallet = this.GetWalletByName(walletName);

            HdAccount[] res = null;
            lock (this.lockObject)
            {
                res = wallet.GetAccountsByCoinType(this.coinType).ToArray();
            }

            this.logger.LogTrace("(-):*.Count={0}", res.Count());
            return res;
        }

        /// <inheritdoc />
        public int LastBlockHeight()
        {
            this.logger.LogTrace("()");

            if (!this.Wallets.Any())
            {
                int height = this.chain.Tip.Height;
                this.logger.LogTrace("(-)[NO_WALLET]:{0}", height);
                return height;
            }

            int res;
            lock (this.lockObject)
            {
                res = this.Wallets.Values.Min(w => w.AccountsRoot.SingleOrDefault(a => a.CoinType == this.coinType)?.LastBlockSyncedHeight) ?? 0;
            }
            this.logger.LogTrace("(-):{0}", res);
            return res;
        }

        /// <inheritdoc />
        public bool ContainsWallets => this.Wallets.Any();

        /// <summary>
        /// Gets the hash of the last block received by the wallets.
        /// </summary>
        /// <returns>Hash of the last block received by the wallets.</returns>
        public uint256 LastReceivedBlockHash()
        {
            this.logger.LogTrace("()");

            if (!this.Wallets.Any())
            {
                uint256 hash = this.chain.Tip.HashBlock;
                this.logger.LogTrace("(-)[NO_WALLET]:'{0}'", hash);
                return hash;
            }

            uint256 lastBlockSyncedHash;
            lock (this.lockObject)
            {
                lastBlockSyncedHash = this.Wallets.Values
                    .Select(w => w.AccountsRoot.SingleOrDefault(a => a.CoinType == this.coinType))
                    .Where(w => w != null)
                    .OrderBy(o => o.LastBlockSyncedHeight)
                    .FirstOrDefault()?.LastBlockSyncedHash;

                // If details about the last block synced are not present in the wallet,
                // find out which is the oldest wallet and set the last block synced to be the one at this date.
                if (lastBlockSyncedHash == null)
                {
                    this.logger.LogWarning("There were no details about the last block synced in the wallets.");
                    DateTimeOffset earliestWalletDate = this.Wallets.Values.Min(c => c.CreationTime);
                    this.UpdateWhenChainDownloaded(this.Wallets.Values, earliestWalletDate.DateTime);
                    lastBlockSyncedHash = this.chain.Tip.HashBlock;
                }
            }

            this.logger.LogTrace("(-):'{0}'", lastBlockSyncedHash);
            return lastBlockSyncedHash;
        }

        /// <inheritdoc />
        public IEnumerable<UnspentOutputReference> GetSpendableTransactionsInWallet(string walletName, int confirmations = 0)
        {
            Guard.NotEmpty(walletName, nameof(walletName));
            this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(walletName), walletName, nameof(confirmations), confirmations);

            Wallet wallet = this.GetWalletByName(walletName);
            UnspentOutputReference[] res = null;
            lock (this.lockObject)
            {
                res = wallet.GetAllSpendableTransactions(this.coinType, this.network, this.chain.Tip.Height, confirmations).ToArray();
            }

            this.logger.LogTrace("(-):*.Count={0}", res.Count());
            return res;
        }

        /// <inheritdoc />
        public IEnumerable<UnspentOutputReference> GetSpendableTransactionsInAccount(WalletAccountReference walletAccountReference, int confirmations = 0)
        {
            Guard.NotNull(walletAccountReference, nameof(walletAccountReference));
            this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(walletAccountReference), walletAccountReference, nameof(confirmations), confirmations);

            Wallet wallet = this.GetWalletByName(walletAccountReference.WalletName);
            UnspentOutputReference[] res = null;
            lock (this.lockObject)
            {
                HdAccount account = wallet.GetAccountByCoinType(walletAccountReference.AccountName, this.coinType);

                if (account == null)
                {
                    this.logger.LogTrace("(-)[ACT_NOT_FOUND]");
                    throw new WalletException(
                        $"Account '{walletAccountReference.AccountName}' in wallet '{walletAccountReference.WalletName}' not found.");
                }

                res = account.GetSpendableTransactions(this.network, this.chain.Tip.Height, confirmations).ToArray();
            }

            this.logger.LogTrace("(-):*.Count={0}", res.Count());
            return res;
        }

        /// <inheritdoc />
        public void RemoveBlocks(ChainedHeader fork)
        {
            Guard.NotNull(fork, nameof(fork));
            this.logger.LogTrace("({0}:'{1}'", nameof(fork), fork);

            lock (this.lockObject)
            {
                IEnumerable<HdAddress> allAddresses = this.keysLookup.Values;
                foreach (HdAddress address in allAddresses)
                {
                    // Remove all the UTXO that have been reorged.
                    IEnumerable<TransactionData> makeUnspendable = address.Transactions.Where(w => w.BlockHeight > fork.Height).ToList();
                    foreach (TransactionData transactionData in makeUnspendable)
                        address.Transactions.Remove(transactionData);

                    // Bring back all the UTXO that are now spendable after the reorg.
                    IEnumerable<TransactionData> makeSpendable = address.Transactions.Where(w => (w.SpendingDetails != null) && (w.SpendingDetails.BlockHeight > fork.Height));
                    foreach (TransactionData transactionData in makeSpendable)
                        transactionData.SpendingDetails = null;
                }

                this.UpdateLastBlockSyncedHeight(fork);
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void ProcessBlock(Block block, ChainedHeader chainedHeader)
        {
            Guard.NotNull(block, nameof(block));
            Guard.NotNull(chainedHeader, nameof(chainedHeader));
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(block), block.GetHash(), nameof(chainedHeader), chainedHeader);

            // If there is no wallet yet, update the wallet tip hash and do nothing else.
            if (!this.Wallets.Any())
            {
                this.WalletTipHash = chainedHeader.HashBlock;
                this.logger.LogTrace("(-)[NO_WALLET]");
                return;
            }

            // Is this the next block.
            if (chainedHeader.Header.HashPrevBlock != this.WalletTipHash)
            {
                this.logger.LogTrace("New block's previous hash '{0}' does not match current wallet's tip hash '{1}'.", chainedHeader.Header.HashPrevBlock, this.WalletTipHash);

                // Are we still on the main chain.
                ChainedHeader current = this.chain.GetBlock(this.WalletTipHash);
                if (current == null)
                {
                    this.logger.LogTrace("(-)[REORG]");
                    throw new WalletException("Reorg");
                }

                // The block coming in to the wallet should never be ahead of the wallet.
                // If the block is behind, let it pass.
                if (chainedHeader.Height > current.Height)
                {
                    this.logger.LogTrace("(-)[BLOCK_TOO_FAR]");
                    throw new WalletException("block too far in the future has arrived to the wallet");
                }
            }

            lock (this.lockObject)
            {
                bool walletUpdated = false;
                foreach (Transaction transaction in block.Transactions)
                {
                    bool trxFound = this.ProcessTransaction(transaction, chainedHeader.Height, block, true);
                    if (trxFound)
                    {
                        walletUpdated = true;
                    }
                }

                // Update the wallets with the last processed block height.
                // It's important that updating the height happens after the block processing is complete,
                // as if the node is stopped, on re-opening it will start updating from the previous height.
                this.UpdateLastBlockSyncedHeight(chainedHeader);

                if (walletUpdated)
                {
                    this.SaveWallets();
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public bool ProcessTransaction(Transaction transaction, int? blockHeight = null, Block block = null, bool isPropagated = true)
        {
            Guard.NotNull(transaction, nameof(transaction));
            uint256 hash = transaction.GetHash();
            this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(transaction), hash, nameof(blockHeight), blockHeight);

            bool foundReceivingTrx = false, foundSendingTrx = false;
            lock (this.lockObject)
            {
                // Check the outputs.
                foreach (TxOut utxo in transaction.Outputs)
                {
                    // Check if the outputs contain one of our addresses.
                    if (this.keysLookup.TryGetValue(utxo.ScriptPubKey, out HdAddress _))
                    {
                        this.AddTransactionToWallet(transaction, utxo, blockHeight, block, isPropagated);
                        foundReceivingTrx = true;
                    }
                }

                // Check the inputs - include those that have a reference to a transaction containing one of our scripts and the same index.
                foreach (TxIn input in transaction.Inputs)
                {
                    if (!this.outpointLookup.TryGetValue(input.PrevOut, out TransactionData tTx))
                    {
                        continue;
                    }

                    // Get the details of the outputs paid out.
                    IEnumerable<TxOut> paidOutTo = transaction.Outputs.Where(o =>
                    {
                        // If script is empty ignore it.
                        if (o.IsEmpty)
                            return false;

                        // Check if the destination script is one of the wallet's.
                        bool found = this.keysLookup.TryGetValue(o.ScriptPubKey, out HdAddress addr);

                        // Include the keys not included in our wallets (external payees).
                        if (!found)
                            return true;

                        // Include the keys that are in the wallet but that are for receiving
                        // addresses (which would mean the user paid itself).
                        // We also exclude the keys involved in a staking transaction.
                        return !addr.IsChangeAddress();
                    });

                    this.AddSpendingTransactionToWallet(transaction, paidOutTo, tTx.Id, tTx.Index, blockHeight, block);
                    foundSendingTrx = true;
                }
            }

            // Figure out what to do when this transaction is found to affect the wallet.
            if (foundSendingTrx || foundReceivingTrx)
            {
                if (foundSendingTrx && blockHeight > 0)
                {
                    NotifyTransaction(TransactionNotificationType.Sent, hash);
                }
                if (foundReceivingTrx && blockHeight > 0)
                {
                    NotifyTransaction(TransactionNotificationType.Received, hash);
                }
                // Save the wallet when the transaction was not included in a block.
                if (blockHeight == null)
                {
                    this.SaveWallets();
                }
            }

            this.logger.LogTrace("(-)");
            return foundSendingTrx || foundReceivingTrx;
        }

        /// <inheritdoc />
        public TransactionModel GetTransactionDetails(
            string walletName,
            Transaction transaction,
            List<IndexedTxOut> prevTransactions,
            TransactionModel transactionModel)
        {
            Guard.NotNull(transaction, nameof(transaction));

            transactionModel.Details = new List<TransactionDetail>();
            var details = transactionModel.Details;

            var totalInputs = prevTransactions.Sum(i => i.TxOut.Value.ToUnit(MoneyUnit.Satoshi));
            var fee = totalInputs - transaction.TotalOut.ToUnit(MoneyUnit.Satoshi);
            decimal unitFee = 0;
            if (prevTransactions != null && prevTransactions.Count() > 0) unitFee = fee / prevTransactions.Count();

            foreach (IndexedTxOut utxo in prevTransactions)
            {
                if (this.keysLookup.TryGetValue(utxo.TxOut.ScriptPubKey, out HdAddress address))
                {
                    if (this.keysLookupToWalletName.TryGetValue(utxo.TxOut.ScriptPubKey, out string keyWalletName))
                    {
                        if ((walletName == null) || (keyWalletName == walletName))
                        {
                            details.Add(new TransactionDetail()
                            {
                                Account = DefaultAccount,
                                Address = address.Address,
                                Category = "send",
                                Amount = utxo.TxOut.Value.ToUnit(MoneyUnit.XRC) * -1,
                                Fee = new Money(unitFee * -1, MoneyUnit.Satoshi).ToUnit(MoneyUnit.XRC)
                            });
                        }
                    }
                }
            }

            //checkfee
            if (details.Count > 0)
            {
                //total fee is here if we just send tx
                transactionModel.Fee = new Money(fee * -1, MoneyUnit.Satoshi).ToUnit(MoneyUnit.XRC);

                var sumFee = details.Sum(f => f.Fee);
                if (sumFee != transactionModel.Fee)
                {
                    var restFee = transactionModel.Fee - sumFee;
                    details[0].Fee = details[0].Fee + restFee;
                }
            }

            var isSendTx = false;

            foreach (TxOut utxo in transaction.Outputs)
            {
                if (this.keysLookup.TryGetValue(utxo.ScriptPubKey, out HdAddress address))
                {
                    if (this.keysLookupToWalletName.TryGetValue(utxo.ScriptPubKey, out string keyWalletName))
                    {
                        if ((walletName == null) || (keyWalletName == walletName))
                        {
                            if (address.IsChangeAddress())
                            {
                                isSendTx = true;
                            }

                            details.Add(new TransactionDetail()
                            {
                                Account = DefaultAccount,
                                Address = address.Address,
                                Category = "receive",
                                Amount = utxo.Value.ToUnit(MoneyUnit.XRC)
                            });
                        }
                    }
                }
            }

            if (isSendTx)
            {
                var clearOutAmount = details.Where(o => o.Category == "receive").Sum(a => a.Amount);
                transactionModel.Amount = transaction.TotalOut.ToUnit(MoneyUnit.XRC) - clearOutAmount;
            }
            else
            {
                var clearOutAmount = details.Where(o => o.Category == "receive").Sum(a => a.Amount);
                transactionModel.Amount = clearOutAmount;
            }

            return transactionModel;
        }

        private decimal AddSpendingTransactionDetails(Transaction transaction, IEnumerable<TxOut> paidToOutputs, uint256 spendingTransactionId, int spendingTransactionIndex, List<TransactionDetail> details)
        {

            Guard.NotNull(transaction, nameof(transaction));
            Guard.NotNull(paidToOutputs, nameof(paidToOutputs));
            // Get the transaction being spent.
            TransactionData spentTransaction = this.keysLookup.Values.Distinct().SelectMany(v => v.Transactions)
                .SingleOrDefault(t => (t.Id == spendingTransactionId) && (t.Index == spendingTransactionIndex));
            if (spentTransaction == null)
            {
               return 0;
            }

            List<PaymentDetails> payments = new List<PaymentDetails>();
            foreach (TxOut paidToOutput in paidToOutputs)
            {
                string destinationAddress = GetOutputDestinationAddress(paidToOutput);
                details.Add(new TransactionDetail()
                {
                    Account = DefaultAccount,
                    Address = destinationAddress,
                    Amount = paidToOutput.Value.ToUnit(MoneyUnit.XRC),
                    Category = "send"
                });
            }

            return spentTransaction.Amount.ToDecimal(MoneyUnit.XRC);
        }

        private string GetOutputDestinationAddress(TxOut txOut)
        {
            string destinationAddress = string.Empty;
            if (txOut.ScriptPubKey != null)
            {
                ScriptTemplate scriptTemplate = txOut.ScriptPubKey.FindTemplate(this.network);
                if (scriptTemplate != null)
                {
                    switch (scriptTemplate.Type)
                    {
                        // Pay to PubKey can be found in outputs of staking transactions.
                        case TxOutType.TX_PUBKEY:
                            PubKey pubKey = PayToPubkeyTemplate.Instance.ExtractScriptPubKeyParameters(txOut.ScriptPubKey);
                            destinationAddress = pubKey.GetAddress(this.network).ToString();
                            break;
                        // Pay to PubKey hash is the regular, most common type of output.
                        case TxOutType.TX_PUBKEYHASH:
                            destinationAddress = txOut.ScriptPubKey.GetDestinationAddress(this.network).ToString();
                            break;
                        case TxOutType.TX_NONSTANDARD:
                        case TxOutType.TX_SCRIPTHASH:
                        case TxOutType.TX_MULTISIG:
                        case TxOutType.TX_NULL_DATA:
                            destinationAddress = txOut.ScriptPubKey.GetDestinationAddress(this.network).ToString();
                            break;
                    }
                }
            }
            return destinationAddress;
        }

        private void NotifyTransaction(TransactionNotificationType subsription, uint256 transactionHash )
        {
            foreach (var notificationSub in this.walletSettings.WalletNotify)
            {
                if (notificationSub.Trigger == subsription || notificationSub.Trigger == TransactionNotificationType.All)
                {
                    try
                    {
                       string command = notificationSub.Command.Replace("%s", transactionHash.ToString());
                       this.logger.LogInformation($"About to call walletnotify command [{command}]");
                       var result = ShellHelper.Run(command);
                       this.logger.LogInformation($"[{result.stdout}]");
                       this.logger.LogInformation($"[{result.stderr}]");
                    }
                    catch (Exception e)
                    {
                        this.logger.LogError(e.ToString());
                    }

                }
            }
        }

        /// <summary>
        /// Adds a transaction that credits the wallet with new coins.
        /// This method is can be called many times for the same transaction (idempotent).
        /// </summary>
        /// <param name="transaction">The transaction from which details are added.</param>
        /// <param name="utxo">The unspent output to add to the wallet.</param>
        /// <param name="blockHeight">Height of the block.</param>
        /// <param name="block">The block containing the transaction to add.</param>
        /// <param name="isPropagated">Propagation state of the transaction.</param>
        private void AddTransactionToWallet(Transaction transaction, TxOut utxo, int? blockHeight = null, Block block = null, bool isPropagated = true)
        {
            Guard.NotNull(transaction, nameof(transaction));
            Guard.NotNull(utxo, nameof(utxo));

            uint256 transactionHash = transaction.GetHash();

            this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(transaction), transactionHash, nameof(blockHeight), blockHeight);

            // Get the collection of transactions to add to.
            Script script = utxo.ScriptPubKey;
            this.keysLookup.TryGetValue(script, out HdAddress address);
            ICollection<TransactionData> addressTransactions = address.Transactions;

            // Check if a similar UTXO exists or not (same transaction ID and same index).
            // New UTXOs are added, existing ones are updated.
            int index = transaction.Outputs.IndexOf(utxo);
            Money amount = utxo.Value;
            TransactionData foundTransaction = addressTransactions.FirstOrDefault(t => (t.Id == transactionHash) && (t.Index == index));
            if (foundTransaction == null)
            {
                this.logger.LogTrace("UTXO '{0}-{1}' not found, creating.", transactionHash, index);
                var newTransaction = new TransactionData
                {
                    Amount = amount,
                    BlockHeight = blockHeight,
                    BlockHash = block?.GetHash(),
                    Id = transactionHash,
                    CreationTime = DateTimeOffset.FromUnixTimeSeconds(block?.Header.Time ?? transaction.Time),
                    Index = index,
                    ScriptPubKey = script,
                    Hex = this.walletSettings.SaveTransactionHex ? transaction.ToHex() : null,
                    IsPropagated = isPropagated,
                    IsCoinbase = transaction.IsCoinBase
                };

                // Add the Merkle proof to the (non-spending) transaction.
                if (block != null)
                {
                    newTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
                }

                addressTransactions.Add(newTransaction);
                this.AddInputKeysLookupLock(newTransaction);
            }
            else
            {
                this.logger.LogTrace("Transaction ID '{0}' found, updating.", transactionHash);

                // Update the block height and block hash.
                if ((foundTransaction.BlockHeight == null) && (blockHeight != null))
                {
                    foundTransaction.BlockHeight = blockHeight;
                    foundTransaction.BlockHash = block?.GetHash();
                }

                // Update the block time.
                if (block != null)
                {
                    foundTransaction.CreationTime = DateTimeOffset.FromUnixTimeSeconds(block.Header.Time);
                }

                // Add the Merkle proof now that the transaction is confirmed in a block.
                if ((block != null) && (foundTransaction.MerkleProof == null))
                {
                    foundTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
                }

                if (isPropagated)
                {
                    foundTransaction.IsPropagated = true;
                }
                
                foundTransaction.IsCoinbase = transaction.IsCoinBase;
            }

            this.TransactionFoundInternal(script);
            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Mark an output as spent, the credit of the output will not be used to calculate the balance.
        /// The output will remain in the wallet for history (and reorg).
        /// </summary>
        /// <param name="transaction">The transaction from which details are added.</param>
        /// <param name="paidToOutputs">A list of payments made out</param>
        /// <param name="spendingTransactionId">The id of the transaction containing the output being spent, if this is a spending transaction.</param>
        /// <param name="spendingTransactionIndex">The index of the output in the transaction being referenced, if this is a spending transaction.</param>
        /// <param name="blockHeight">Height of the block.</param>
        /// <param name="block">The block containing the transaction to add.</param>
        private void AddSpendingTransactionToWallet(Transaction transaction, IEnumerable<TxOut> paidToOutputs,
            uint256 spendingTransactionId, int? spendingTransactionIndex, int? blockHeight = null, Block block = null)
        {
            Guard.NotNull(transaction, nameof(transaction));
            Guard.NotNull(paidToOutputs, nameof(paidToOutputs));

            this.logger.LogTrace("({0}:'{1}',{2}:'{3}',{4}:{5},{6}:'{7}')", nameof(transaction), transaction.GetHash(),
                nameof(spendingTransactionId), spendingTransactionId, nameof(spendingTransactionIndex), spendingTransactionIndex, nameof(blockHeight), blockHeight);

            // Get the transaction being spent.
            TransactionData spentTransaction = this.keysLookup.Values.Distinct().SelectMany(v => v.Transactions)
                .SingleOrDefault(t => (t.Id == spendingTransactionId) && (t.Index == spendingTransactionIndex));
            if (spentTransaction == null)
            {
                // Strange, why would it be null?
                this.logger.LogTrace("(-)[TX_NULL]");
                return;
            }

            // If the details of this spending transaction are seen for the first time.
            if (spentTransaction.SpendingDetails == null)
            {
                this.logger.LogTrace("Spending UTXO '{0}-{1}' is new.", spendingTransactionId, spendingTransactionIndex);

                List<PaymentDetails> payments = new List<PaymentDetails>();
                foreach (TxOut paidToOutput in paidToOutputs)
                {
                    // Figure out how to retrieve the destination address.
                    string destinationAddress = string.Empty;
                    ScriptTemplate scriptTemplate = paidToOutput.ScriptPubKey.FindTemplate(this.network);
                    switch (scriptTemplate.Type)
                    {
                        // Pay to PubKey can be found in outputs of staking transactions.
                        case TxOutType.TX_PUBKEY:
                            PubKey pubKey = PayToPubkeyTemplate.Instance.ExtractScriptPubKeyParameters(paidToOutput.ScriptPubKey);
                            destinationAddress = pubKey.GetAddress(this.network).ToString();
                            break;
                        // Pay to PubKey hash is the regular, most common type of output.
                        case TxOutType.TX_PUBKEYHASH:
                            destinationAddress = paidToOutput.ScriptPubKey.GetDestinationAddress(this.network).ToString();
                            break;
                        case TxOutType.TX_NONSTANDARD:
                        case TxOutType.TX_SCRIPTHASH:
                        case TxOutType.TX_MULTISIG:
                        case TxOutType.TX_NULL_DATA:
                            destinationAddress = paidToOutput.ScriptPubKey.GetDestinationAddress(this.network)?.ToString();
                            break;
                    }

                    payments.Add(new PaymentDetails
                    {
                        DestinationScriptPubKey = paidToOutput.ScriptPubKey,
                        DestinationAddress = destinationAddress,
                        Amount = paidToOutput.Value
                    });
                }

                SpendingDetails spendingDetails = new SpendingDetails
                {
                    TransactionId = transaction.GetHash(),
                    Payments = payments,
                    CreationTime = DateTimeOffset.FromUnixTimeSeconds(block?.Header.Time ?? transaction.Time),
                    BlockHeight = blockHeight,
                    Hex = this.walletSettings.SaveTransactionHex ? transaction.ToHex() : null
                };

                spentTransaction.SpendingDetails = spendingDetails;
                spentTransaction.MerkleProof = null;
            }
            else // If this spending transaction is being confirmed in a block.
            {
                this.logger.LogTrace("Spending transaction ID '{0}' is being confirmed, updating.", spendingTransactionId);

                // Update the block height.
                if (spentTransaction.SpendingDetails.BlockHeight == null && blockHeight != null)
                {
                    spentTransaction.SpendingDetails.BlockHeight = blockHeight;
                }

                // Update the block time to be that of the block in which the transaction is confirmed.
                if (block != null)
                {
                    spentTransaction.SpendingDetails.CreationTime = DateTimeOffset.FromUnixTimeSeconds(block.Header.Time);
                }
            }

            this.logger.LogTrace("(-)");
        }

        private void TransactionFoundInternal(Script script)
        {
            this.logger.LogTrace("()");

            foreach (Wallet wallet in this.Wallets.Values)
            {
                foreach (HdAccount account in wallet.GetAccountsByCoinType(this.coinType))
                {
                    bool isChange;
                    if (account.ExternalAddresses.Any(address => address.ScriptPubKey == script))
                    {
                        isChange = false;
                    }
                    else if (account.InternalAddresses.Any(address => address.ScriptPubKey == script))
                    {
                        isChange = true;
                    }
                    else
                    {
                        continue;
                    }

                    // Calculate how many accounts to add to keep a buffer of 20 unused addresses.
                    int lastUsedAddressIndex = account.GetLastUsedAddress(isChange).Index;
                    int addressesCount = isChange ? account.InternalAddresses.Count() : account.ExternalAddresses.Count();
                    int emptyAddressesCount = addressesCount - lastUsedAddressIndex - 1;
                    int accountsToAdd = UnusedAddressesBuffer - emptyAddressesCount;
                    var newAddresses = account.CreateAddresses(this.network, accountsToAdd, isChange);

                    this.UpdateKeysLookupLock(newAddresses, wallet.Name);
                }
            }

            this.logger.LogTrace("()");
        }

        /// <inheritdoc />
        public void DeleteWallet(string name)
        {
            this.DBreezeStorage.DeleteWallet(name);
            Wallet walletCache;
            this.Wallets.TryRemove(name, out walletCache);
        }

        /// <inheritdoc />
        public void SaveWallets()
        {
            foreach (Wallet wallet in this.Wallets.Values)
            {
                this.SaveWallet(wallet);
            }
        }

        /// <inheritdoc />
        public void SaveWallet(Wallet wallet)
        {
            Guard.NotNull(wallet, nameof(wallet));
            this.logger.LogTrace("({0}:'{1}')", nameof(wallet), wallet.Name);

            this.DBreezeStorage.SaveToStorage(wallet, wallet.Name, this.network);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public string GetWalletFileExtension()
        {
            return WalletFileExtension;
        }

        /// <inheritdoc />
        public void UpdateLastBlockSyncedHeight(ChainedHeader chainedHeader)
        {
            Guard.NotNull(chainedHeader, nameof(chainedHeader));
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedHeader), chainedHeader);

            // Update the wallets with the last processed block height.
            foreach (Wallet wallet in this.Wallets.Values)
            {
                this.UpdateLastBlockSyncedHeight(wallet, chainedHeader);
            }

            this.WalletTipHash = chainedHeader.HashBlock;
            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void UpdateLastBlockSyncedHeight(Wallet wallet, ChainedHeader chainedHeader)
        {
            Guard.NotNull(wallet, nameof(wallet));
            Guard.NotNull(chainedHeader, nameof(chainedHeader));
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(wallet), wallet.Name, nameof(chainedHeader), chainedHeader);

            // The block locator will help when the wallet
            // needs to rewind this will be used to find the fork.
            wallet.BlockLocator = chainedHeader.GetLocator().Blocks;

            lock (this.lockObject)
            {
                // Update the wallets with the last processed block height.
                foreach (AccountRoot accountRoot in wallet.AccountsRoot.Where(a => a.CoinType == this.coinType))
                {
                    accountRoot.LastBlockSyncedHeight = chainedHeader.Height;
                    accountRoot.LastBlockSyncedHash = chainedHeader.HashBlock;
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Generates the wallet file.
        /// </summary>
        /// <param name="name">The name of the wallet.</param>
        /// <param name="encryptedSeed">The seed for this wallet, password encrypted.</param>
        /// <param name="chainCode">The chain code.</param>
        /// <param name="creationTime">The time this wallet was created.</param>
        /// <returns>The wallet object that was saved into the file system.</returns>
        /// <exception cref="WalletException">Thrown if wallet cannot be created.</exception>
        private Wallet GenerateWalletFile(string name, string encryptedSeed, byte[] chainCode, DateTimeOffset? creationTime = null)
        {
            Guard.NotEmpty(name, nameof(name));
            Guard.NotEmpty(encryptedSeed, nameof(encryptedSeed));
            Guard.NotNull(chainCode, nameof(chainCode));
            this.logger.LogTrace("({0}:'{1}')", nameof(name), name);

            // Check if any wallet file already exists, with case insensitive comparison.
            //if (this.Wallets.Any(w => string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase)))
            if (this.Wallets.ContainsKey(name))
            {
                this.logger.LogTrace("(-)[WALLET_ALREADY_EXISTS]");
                throw new WalletException($"Wallet with name '{name}' already exists.");
            }

            List<Wallet> similarWallets = this.Wallets.Values.Where(w => w.EncryptedSeed == encryptedSeed).ToList();
            if (similarWallets.Any())
            {
                this.logger.LogTrace("(-)[SAME_PK_ALREADY_EXISTS]");
                throw new WalletException("Cannot create this wallet as a wallet with the same private key already exists. If you want to restore your wallet from scratch, " +
                                                    $"please remove the file {string.Join(", ", similarWallets.Select(w => w.Name))}.{WalletFileExtension} from '{this.DBreezeStorage.FolderPath}' and try restoring the wallet again. " +
                                                    "Make sure you have your mnemonic and your password handy!");
            }

            Wallet walletFile = new Wallet
            {
                Name = name,
                EncryptedSeed = encryptedSeed,
                ChainCode = chainCode,
                CreationTime = creationTime ?? this.dateTimeProvider.GetTimeOffset(),
                Network = this.network,
                AccountsRoot = new List<AccountRoot> { new AccountRoot() { Accounts = new List<HdAccount>(), CoinType = this.coinType } },
            };

            // Create a folder if none exists and persist the file.
            this.SaveWallet(walletFile);

            this.logger.LogTrace("(-)");
            return walletFile;
        }

        /// <summary>
        /// Loads the wallet to be used by the manager.
        /// </summary>
        /// <param name="wallet">The wallet to load.</param>
        private void Load(Wallet wallet)
        {
            Guard.NotNull(wallet, nameof(wallet));
            this.logger.LogTrace("({0}:'{1}')", nameof(wallet), wallet.Name);

            this.Wallets.AddOrReplace(wallet.Name,wallet);
            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Loads the keys and transactions we're tracking in memory for faster lookups.
        /// </summary>
        public void LoadKeysLookupLock()
        {
            lock (this.lockObject)
            {
                foreach (Wallet wallet in this.Wallets.Values)
                {
                    IEnumerable<HdAddress> addresses = wallet.GetAllAddressesByCoinType(this.coinType);
                    foreach (HdAddress address in addresses)
                    {
                        this.keysLookup[address.ScriptPubKey] = address;
                        this.keysLookupToWalletName[address.ScriptPubKey] = wallet.Name;
                        if (address.Pubkey != null)
                        {
                            this.keysLookup[address.Pubkey] = address;
                            this.keysLookupToWalletName[address.Pubkey] = wallet.Name;
                        }

                        foreach (var transaction in address.Transactions)
                        {
                            this.outpointLookup[new OutPoint(transaction.Id, transaction.Index)] = transaction;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Update the keys and transactions we're tracking in memory for faster lookups.
        /// </summary>
        public void UpdateKeysLookupLock(IEnumerable<HdAddress> addresses, string walletName)
        {
            if (addresses == null || !addresses.Any())
            {
                return;
            }

            lock (this.lockObject)
            {
                foreach (HdAddress address in addresses)
                {
                    this.keysLookup[address.ScriptPubKey] = address;
                    this.keysLookupToWalletName[address.ScriptPubKey] = walletName;
                    if (address.Pubkey != null)
                    {
                        this.keysLookup[address.Pubkey] = address;
                        this.keysLookupToWalletName[address.Pubkey] = walletName;
                    }
                }
            }
        }

        /// <summary>
        /// Add to the list of unspent outputs kept in memory for faster lookups.
        /// </summary>
        private void AddInputKeysLookupLock(TransactionData transactionData)
        {
            Guard.NotNull(transactionData, nameof(transactionData));

            lock (this.lockObject)
            {
                this.outpointLookup[new OutPoint(transactionData.Id, transactionData.Index)] = transactionData;
            }
        }

        /// <inheritdoc />
        public IEnumerable<string> GetWalletsNames()
        {
            return this.Wallets.Keys;
        }

        /// <inheritdoc />
        public Wallet GetWalletByName(string walletName)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(walletName), walletName);
            Wallet wallet = null;
            if (this.Wallets.ContainsKey(walletName))
            {
                wallet = this.Wallets[walletName];
            }
            if (wallet == null)
            {
                this.logger.LogTrace("(-)[NOT_FOUND]");
                throw new WalletException($"No wallet with name {walletName} could be found.");
            }

            this.logger.LogTrace("(-)");
            return wallet;
        }

        /// <inheritdoc />
        public ICollection<uint256> GetFirstWalletBlockLocator()
        {
            return this.Wallets.Values.First().BlockLocator;
        }

        /// <inheritdoc />
        public int? GetEarliestWalletHeight()
        {
            return this.Wallets.Values.Min(w => w.AccountsRoot.Single(a => a.CoinType == this.coinType).LastBlockSyncedHeight);
        }

        /// <inheritdoc />
        public DateTimeOffset GetOldestWalletCreationTime()
        {
            return this.Wallets.Values.Min(w => w.CreationTime);
        }

        /// <inheritdoc />
        public HashSet<(uint256, DateTimeOffset)> RemoveTransactionsByIds(string walletName, IEnumerable<uint256> transactionsIds)
        {
            Guard.NotNull(transactionsIds, nameof(transactionsIds));
            Guard.NotEmpty(walletName, nameof(walletName));

            List<uint256> idsToRemove = transactionsIds.ToList();
            Wallet wallet = this.GetWallet(walletName);

            HashSet<(uint256, DateTimeOffset)> result = new HashSet<(uint256, DateTimeOffset)>();

            lock (this.lockObject)
            {
                IEnumerable<HdAccount> accounts = wallet.GetAccountsByCoinType(this.coinType);
                foreach (HdAccount account in accounts)
                {
                    foreach (HdAddress address in account.GetCombinedAddresses())
                    {
                        for (int i = 0; i < address.Transactions.Count; i++)
                        {
                            TransactionData transaction = address.Transactions.ElementAt(i);

                            // Remove the transaction from the list of transactions affecting an address.
                            // Only transactions that haven't been confirmed in a block can be removed.
                            if (!transaction.IsConfirmed() && idsToRemove.Contains(transaction.Id))
                            {
                                result.Add((transaction.Id, transaction.CreationTime));
                                address.Transactions = address.Transactions.Except(new[] { transaction }).ToList();
                                i--;
                            }

                            // Remove the spending transaction object containing this transaction id.
                            if (transaction.SpendingDetails != null && !transaction.SpendingDetails.IsSpentConfirmed() && idsToRemove.Contains(transaction.SpendingDetails.TransactionId))
                            {
                                result.Add((transaction.SpendingDetails.TransactionId, transaction.SpendingDetails.CreationTime));
                                address.Transactions.ElementAt(i).SpendingDetails = null;
                            }
                        }
                    }
                }
            }

            if (result.Any())
            {
                this.SaveWallet(wallet);
            }

            return result;
        }

        /// <inheritdoc />
        public HashSet<(uint256, DateTimeOffset)> RemoveAllTransactions(string walletName)
        {
            Guard.NotEmpty(walletName, nameof(walletName));
            Wallet wallet = this.GetWallet(walletName);

            HashSet<(uint256, DateTimeOffset)> removedTransactions = new HashSet<(uint256, DateTimeOffset)>();

            lock (this.lockObject)
            {
                IEnumerable<HdAccount> accounts = wallet.GetAccountsByCoinType(this.coinType);
                foreach (HdAccount account in accounts)
                {
                    foreach (HdAddress address in account.GetCombinedAddresses())
                    {
                        removedTransactions.UnionWith(address.Transactions.Select(t => (t.Id, t.CreationTime)));
                        address.Transactions.Clear();
                    }
                }
            }

            if (removedTransactions.Any())
            {
                this.SaveWallet(wallet);
            }

            return removedTransactions;
        }

        /// <summary>
        /// Updates details of the last block synced in a wallet when the chain of headers finishes downloading.
        /// </summary>
        /// <param name="wallets">The wallets to update when the chain has downloaded.</param>
        /// <param name="date">The creation date of the block with which to update the wallet.</param>
        private void UpdateWhenChainDownloaded(IEnumerable<Wallet> wallets, DateTime date)
        {
            this.asyncLoopFactory.RunUntil("WalletManager.DownloadChain", this.nodeLifetime.ApplicationStopping,
                () => this.chain.IsDownloaded(),
                () =>
                {
                    int heightAtDate = this.chain.GetHeightAtTime(date);

                    foreach (var wallet in wallets)
                    {
                        this.logger.LogTrace("The chain of headers has finished downloading, updating wallet '{0}' with height {1}", wallet.Name, heightAtDate);
                        this.UpdateLastBlockSyncedHeight(wallet, this.chain.GetBlock(heightAtDate));
                        this.SaveWallet(wallet);
                    }
                },
                (ex) =>
                {
                    // In case of an exception while waiting for the chain to be at a certain height, we just cut our losses and
                    // sync from the current height.
                    this.logger.LogError($"Exception occurred while waiting for chain to download: {ex.Message}");

                    foreach (var wallet in wallets)
                    {
                        this.UpdateLastBlockSyncedHeight(wallet, this.chain.Tip);
                    }
                },
                TimeSpans.FiveSeconds);
        }

        public HdAddress GetAddressByPubKeyHash(Script scriptSig)
        {
            if (this.keysLookup.ContainsKey(scriptSig))
            {
                return this.keysLookup[scriptSig];
            }
            else
            {
                return null;
            }
        }

    }
}
