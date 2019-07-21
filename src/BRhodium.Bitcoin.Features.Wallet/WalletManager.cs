using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.DataEncoders;
using BRhodium.Node.Configuration;
using BRhodium.Bitcoin.Features.Wallet.Broadcasting;
using BRhodium.Bitcoin.Features.Wallet.Interfaces;
using BRhodium.Node.Utilities;
using System.Text;
using BRhodium.Bitcoin.Features.Consensus.Models;
using System.Diagnostics;
using System.IO;

[assembly: InternalsVisibleTo("BRhodium.Bitcoin.Features.Wallet.Tests")]

namespace BRhodium.Bitcoin.Features.Wallet
{
    /// <summary>
    /// A manager providing operations on wallets.
    /// </summary>
    public class WalletManager : IWalletManager
    {
        /// <summary>Size of the buffer of unused addresses maintained in an account. </summary>
        private const int UnusedAddressesBuffer = 20;

        /// <summary>Quantity of accounts created in a wallet file when a wallet is restored.</summary>
        private const int WalletRecoveryAccountsCount = 1;

        /// <summary>Quantity of accounts created in a wallet file when a wallet is created.</summary>
        private const int WalletCreationAccountsCount = 1;

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

        /// <summary>An object capable of storing <see cref="Wallet"/>s to the repository.</summary>
        private readonly WalletRepository repository;
        /// <summary>An object capable of storing <see cref="Wallet"/>s to the file system.</summary>
        private readonly FileStorage<Wallet> fileStorage;

        /// <summary>The broadcast manager.</summary>
        private readonly IBroadcasterManager broadcasterManager;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>The settings for the wallet feature.</summary>
        private readonly WalletSettings walletSettings;

        /// <summary>
        ///  Makes the wallet settings public for other functions.
        /// </summary>
        public WalletSettings WalletSettings {
            get
            {
                return this.walletSettings;
            }
        }

        public uint256 WalletTipHash { get; set; }

        /// <summary>Memory locked unspendable transaction parts (tx hash, index vount)</summary>
        public ConcurrentDictionary<string, int> LockedTxOut { get; set; }

        // In order to allow faster look-ups of transactions affecting the wallets' addresses,
        // we keep a couple of objects in memory:
        // 1. the list of unspent outputs for checking whether inputs from a transaction are being spent by our wallet and
        // 2. the list of addresses contained in our wallet for checking whether a transaction is being paid to the wallet.
        private ConcurrentDictionary<OutPoint, TransactionData> outpointLookup;
        internal ConcurrentDictionary<ScriptId, WalletLinkedHdAddress> addressByScriptLookup;
        internal ConcurrentDictionary<string, WalletLinkedHdAddress> addressLookup;


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
            IBroadcasterManager broadcasterManager = null,
            WalletRepository walletRepository = null) // no need to know about transactions the node will broadcast to.
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

            this.network = network;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.chain = chain;
            this.asyncLoopFactory = asyncLoopFactory;
            this.nodeLifetime = nodeLifetime;

            this.repository = walletRepository;
            if (this.repository == null)
            {
                this.repository = new WalletRepository(dataFolder.WalletPath, this.coinType, this.network);
            }
            this.fileStorage = new FileStorage<Wallet>(dataFolder.RootPath);

            this.broadcasterManager = broadcasterManager;
            this.dateTimeProvider = dateTimeProvider;

            // register events
            if (this.broadcasterManager != null)
            {
                this.broadcasterManager.TransactionStateChanged += this.BroadcasterManager_TransactionStateChanged;
            }
            this.addressByScriptLookup = new ConcurrentDictionary<ScriptId, WalletLinkedHdAddress>();
            this.addressLookup = new ConcurrentDictionary<string, WalletLinkedHdAddress>();
            this.outpointLookup = new ConcurrentDictionary<OutPoint, TransactionData>();
        }

       
        public void LoadWalletsFromFiles() {
            // Find wallets and load them in memory.
            IEnumerable<Wallet> wallets = this.FileStorage.LoadByFileExtension(WalletFileExtension);
            int count = 0;
            Stopwatch sw = new Stopwatch();
            int length= wallets.Count();
            foreach (Wallet wallet in wallets)
            {
                sw.Start();
                count++;
                EnsureAddress(wallet, false);
                EnsureAddress(wallet, true);
                this.repository.SaveWallet(wallet.Name, wallet, true);
                var walletResult = this.repository.GetWalletByName(wallet.Name);
                sw.Stop();                
                this.logger.LogInformation($"Migrated wallet to db: {wallet.Name} #{count} / of {length} duration {sw.ElapsedMilliseconds}ms {Math.Round((double)((double)count / (double)length) * 100,2) }% complete");
                sw.Reset();
            }
        }

        private void EnsureAddress(Wallet wallet, bool isChange) {

            var accountReference = wallet.AccountsRoot.Single(a => a.CoinType == (CoinType)this.network.Consensus.CoinType);
            var hdAccount = accountReference.Accounts.First();

            var lastAddy = hdAccount.GetLastUsedAddress(isChange);
            int lastUsedAddressIndex = 0;
            if (lastAddy != null)
            {
                lastUsedAddressIndex = lastAddy.Index;
            }
            int addressesCount = isChange ? hdAccount.InternalAddresses.Count() : hdAccount.ExternalAddresses.Count();
            int emptyAddressesCount = addressesCount - lastUsedAddressIndex - 1;
            int accountsToAdd = UnusedAddressesBuffer - emptyAddressesCount;
            var newAddresses = hdAccount.CreateAddresses(this.network, accountsToAdd, isChange);

            List<WalletLinkedHdAddress> walletLinkerList = new List<WalletLinkedHdAddress>();
            foreach (var address in newAddresses)
            {
                walletLinkerList.Add(new WalletLinkedHdAddress(address, wallet.Id));
            }
            this.UpdateKeysLookupLock(walletLinkerList);
        }

        private void BroadcasterManager_TransactionStateChanged(object sender, TransactionBroadcastEntry transactionEntry)
        {
            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("()");
                try
                {
                    if (string.IsNullOrEmpty(transactionEntry.ErrorMessage))
                    {
                        this.ProcessTransaction(transactionEntry.Transaction, null, null, null, transactionEntry.State == State.Propagated);
                    }
                    else
                    {
                        this.logger.LogTrace("Exception occurred: {0}", transactionEntry.ErrorMessage);
                        this.logger.LogTrace("(-)[EXCEPTION]");
                    }
                }
                catch (Exception e)
                {
                    this.logger.LogTrace("Exception occurred: {0}: {1}", e.GetType().Name, e.Message);
                    this.logger.LogTrace("(-)[EXCEPTION]");
                }
                this.logger.LogTrace("(-)");
            });
        }
        
        public void Start()
        {
            this.logger.LogTrace("()");

            //if db has no wallet initialized, load migrate from files if they exist
            if (!this.repository.HasWallets())
            {
                LoadWalletsFromFiles();
            }

            // Load data in memory for faster lookups.
            this.LoadKeysLookupLock();

            // Find the last chain block received by the wallet manager.
            this.WalletTipHash = this.LastReceivedBlockHash();

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void Stop()
        {
            this.logger.LogTrace("()");

            if (this.broadcasterManager != null)
                this.broadcasterManager.TransactionStateChanged -= this.BroadcasterManager_TransactionStateChanged;

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
            Wallet wallet = this.GenerateWallet(name, encryptedSeed, extendedKey.ChainCode);

            //Need wallet id set correctly for address linkers.
            //Save at this stage will only save skeleton and not addresses or accounts
            this.SaveWallet(wallet);
            wallet = this.repository.GetWalletByName(wallet.Name);

            // Generate multiple accounts and addresses from the get-go.
            for (int i = 0; i < WalletCreationAccountsCount; i++)
            {
                HdAccount account = wallet.AddNewAccount(password, this.coinType, this.dateTimeProvider.GetTimeOffset());
                IEnumerable<HdAddress> newReceivingAddresses = account.CreateAddresses(this.network, UnusedAddressesBuffer);
                IEnumerable<HdAddress> newChangeAddresses = account.CreateAddresses(this.network, UnusedAddressesBuffer, true);
                List<WalletLinkedHdAddress> walletLinkerList = new List<WalletLinkedHdAddress>();
                foreach (var rAddress in newReceivingAddresses)
                {
                    walletLinkerList.Add(new WalletLinkedHdAddress(rAddress, wallet.Id));
                }
                foreach (var cAddress in newChangeAddresses)
                {
                    walletLinkerList.Add(new WalletLinkedHdAddress(cAddress, wallet.Id));
                }
                this.UpdateKeysLookupLock(walletLinkerList);
            }

            // If the chain is downloaded, we set the height of the newly created wallet to it.
            // However, if the chain is still downloading when the user creates a wallet,
            // we wait until it is downloaded in order to set it. Otherwise, the height of the wallet will be the height of the chain at that moment.
            if (this.chain.IsDownloaded())
            {
                this.UpdateLastBlockSyncedHeight(name, this.chain.Tip);
            }
            else
            {
                this.UpdateWhenChainDownloaded(new[] { wallet.Name }, DateTime.Now);
            }

            // Save the changes to the file and add addresses to be tracked.
            this.SaveWallet(wallet);
            wallet = this.repository.GetWalletByName(wallet.Name);
            this.logger.LogTrace("(-)");
            return mnemonic;
        }
      /// <summary>
      /// Generates and returns new wallet object.
      /// </summary>
      /// <param name="password"></param>
      /// <param name="name"></param>
      /// <param name="passphrase"></param>
      /// <param name="mnemonicList"></param>
      /// <returns></returns>
        public Wallet CreateAndReturnWallet(string password, string name, string passphrase = null, string mnemonicList = null)
        {
            CreateWallet(password, name, passphrase, mnemonicList);
            return GetWalletByName(name);
        }
            /// <inheritdoc />
        public Wallet LoadWallet(string password, string name)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotEmpty(name, nameof(name));
            this.logger.LogTrace("({0}:'{1}')", nameof(name), name);

            // Load the file from the local system.
            Wallet wallet = this.repository.GetWalletByName(name);

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
            Wallet wallet = this.GenerateWallet(name, encryptedSeed, extendedKey.ChainCode, creationTime);

            //Need wallet id set correctly for address linkers.
            //Save at this stage will only save skeleton and not addresses or accounts
            this.SaveWallet(wallet);
            wallet = this.repository.GetWalletByName(wallet.Name);

            // Generate multiple accounts and addresses from the get-go.
            for (int i = 0; i < WalletRecoveryAccountsCount; i++)
            {
                HdAccount account = wallet.AddNewAccount(password, this.coinType, this.dateTimeProvider.GetTimeOffset());
                IEnumerable<HdAddress> newReceivingAddresses = account.CreateAddresses(this.network, UnusedAddressesBuffer);
                IEnumerable<HdAddress> newChangeAddresses = account.CreateAddresses(this.network, UnusedAddressesBuffer, true);
                List<WalletLinkedHdAddress> walletLinkerList = new List<WalletLinkedHdAddress>();
                foreach (var rAddress in newReceivingAddresses)
                {
                    walletLinkerList.Add(new WalletLinkedHdAddress(rAddress, wallet.Id));
                }
                foreach (var cAddress in newChangeAddresses)
                {
                    walletLinkerList.Add(new WalletLinkedHdAddress(cAddress, wallet.Id));
                }
                this.UpdateKeysLookupLock(walletLinkerList);
            }

            // If the chain is downloaded, we set the height of the recovered wallet to that of the recovery date.
            // However, if the chain is still downloading when the user restores a wallet,
            // we wait until it is downloaded in order to set it. Otherwise, the height of the wallet may not be known.
            if (this.chain.IsDownloaded())
            {
                int blockSyncStart = this.chain.GetHeightAtTime(creationTime);
                this.UpdateLastBlockSyncedHeight(name, this.chain.GetBlock(blockSyncStart));
            }
            else
            {
                this.UpdateWhenChainDownloaded(new[] { wallet.Name }, creationTime);
            }

            // Save the changes to the file and add addresses to be tracked.
            this.SaveWallet(wallet);
            wallet = this.repository.GetWalletByName(wallet.Name);
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
                    List<WalletLinkedHdAddress> walletLinkerList = new List<WalletLinkedHdAddress>();
                    foreach (var address in newAddresses)
                    {
                        walletLinkerList.Add(new WalletLinkedHdAddress(address, wallet.Id));
                        this.repository.SaveAddress(wallet.Id, address);
                    }                   
                    this.UpdateKeysLookupLock(walletLinkerList);
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

                List<WalletLinkedHdAddress> walletLinkerList = new List<WalletLinkedHdAddress>();
                foreach (var address in newAddresses)
                {
                    walletLinkerList.Add(new WalletLinkedHdAddress(address, wallet.Id));
                    this.repository.SaveAddress(wallet.Id, address);
                }

                this.UpdateKeysLookupLock(walletLinkerList);

                addresses = unusedAddresses.Concat(newAddresses).OrderBy(x => x.Index).ToList();
            }

            // Save the changes to the file.
            this.SaveWallet(wallet);

            this.logger.LogTrace("(-)");
            return addresses;
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
                    (Money amountConfirmed, Money amountUnconfirmed) result = account.GetSpendableAmount();

                    balances.Add(new AccountBalance
                    {
                        Account = account,
                        AmountConfirmed = result.amountConfirmed,
                        AmountUnconfirmed = result.amountUnconfirmed
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
                Wallet wallet = this.repository.GetWalletByAddress(address);
                HdAddress hdAddress = wallet.GetAllAddressesByCoinType(this.coinType).FirstOrDefault(a => a.Address == address);
                if (hdAddress != null)
                {
                    (Money amountConfirmed, Money amountUnconfirmed) result = hdAddress.GetSpendableAmount();

                    balance.AmountConfirmed = result.amountConfirmed;
                    balance.AmountUnconfirmed = result.amountUnconfirmed;
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

            if (!this.repository.HasWallets())
            {
                int height = this.chain.Tip.Height;
                this.logger.LogTrace("(-)[NO_WALLET]:{0}", height);
                return height;
            }
            
            int res;
            lock (this.lockObject)
            {
                res = 0;
                var rlast = this.repository.GetLastSyncedBlock();
                if (rlast != null)
                {
                    res = rlast.Height;
                }
            }
            
            this.logger.LogTrace("(-):{0}", res);
            return res;
        }

        /// <inheritdoc />
        public bool ContainsWallets => this.repository.HasWallets();

        public FileStorage<Wallet> FileStorage
        {
            get
            {
                return this.fileStorage;
            }
        }

        /// <summary>
        /// Gets the hash of the last block received by the wallets.
        /// </summary>
        /// <returns>Hash of the last block received by the wallets.</returns>
        public uint256 LastReceivedBlockHash()
        {
            this.logger.LogTrace("()");           

            uint256 lastBlockSyncedHash = new uint256();
            lock (this.lockObject)
            {
                lastBlockSyncedHash = this.repository.GetLastUpdatedBlockHash();
                if (lastBlockSyncedHash == null)
                {
                    string walletName = this.repository.GetLastUpdatedWalletName();
                    if (String.IsNullOrEmpty(walletName))
                    {
                        uint256 hash = this.chain.Tip.HashBlock;
                        this.logger.LogTrace("(-)[NO_WALLET]:'{0}'", hash);
                        return hash;
                    }
                }

                // If details about the last block synced are not present in the wallet,
                // find out which is the oldest wallet and set the last block synced to be the one at this date.
                if (lastBlockSyncedHash == null)
                {
                    this.logger.LogWarning("There were no details about the last block synced in the wallets.");
                    DateTimeOffset earliestWalletDate = this.repository.GetOldestWalletCreationTime();
                    this.UpdateWhenChainDownloaded(this.GetWalletNames(), earliestWalletDate.DateTime);
                    lastBlockSyncedHash = this.chain.Tip.HashBlock;
                }
            }

            //this.logger.LogTrace("(-):'{0}'", lastBlockSyncedHash);
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
                res = wallet.GetAllSpendableTransactions(this.coinType, this.chain.Tip.Height, confirmations).ToArray();
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

                res = account.GetSpendableTransactions(this.chain.Tip.Height, confirmations).ToArray();
            }

            this.logger.LogTrace("(-):*.Count={0}", res.Count());
            return res;
        }

        /// <inheritdoc />
        public void RemoveBlocks(ChainedHeader fork)
        {
            this.logger.LogTrace("({0}:'{1}'", nameof(fork), fork);

            lock (this.lockObject)
            {
                foreach (var walletLinkedHdAddress in this.addressByScriptLookup.Values)
                {
                    // Remove all the UTXO that have been reorged.
                    IEnumerable<TransactionData> makeUnspendable = walletLinkedHdAddress.HdAddress.Transactions.Where(w => w.BlockHeight > fork.Height).ToList();
                    foreach (TransactionData transactionData in makeUnspendable)
                    {
                        walletLinkedHdAddress.HdAddress.Transactions.Remove(transactionData);
                        this.repository.SaveAddress(walletLinkedHdAddress.HdAddress.WalletId, walletLinkedHdAddress.HdAddress,true);
                        this.repository.FlushWalletCache(walletLinkedHdAddress.WalletId);
                    }
                    // Bring back all the UTXO that are now spendable after the reorg.
                    IEnumerable<TransactionData> makeSpendable = walletLinkedHdAddress.HdAddress.Transactions.Where(w => (w.SpendingDetails != null) && (w.SpendingDetails.BlockHeight > fork.Height));
                    foreach (TransactionData transactionData in makeSpendable)
                    {
                        transactionData.SpendingDetails = null;
                        this.repository.SaveAddress(walletLinkedHdAddress.HdAddress.WalletId, walletLinkedHdAddress.HdAddress,true);
                        this.repository.FlushWalletCache(walletLinkedHdAddress.WalletId);
                    }
                }

                this.UpdateLastBlockSyncedHeight(fork);
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void ProcessBlock(Block block, ChainedHeader chainedHeader)
        {
            //Task task = Task.Run(() =>
            //{
           
            Guard.NotNull(block, nameof(block));
            Guard.NotNull(chainedHeader, nameof(chainedHeader));
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(block), block.GetHash(), nameof(chainedHeader), chainedHeader);

            // If there is no wallet yet, update the wallet tip hash and do nothing else.
            if (!this.repository.HasWallets())
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
                MerkleProofTemplate merkleProofTemplate = new MerkleProofTemplate(block);
                bool walletUpdated = false;
                foreach (Transaction transaction in block.Transactions)
                {
                    bool trxFound = this.ProcessTransaction(transaction, chainedHeader.Height, block, merkleProofTemplate, true);
                    if (trxFound)
                    {
                        walletUpdated = true;
                    }
                }

                // Update the wallets with the last processed block height.
                // It's important that updating the height happens after the block processing is complete,
                // as if the node is stopped, on re-opening it will start updating from the previous height.
                this.UpdateLastBlockSyncedHeight(chainedHeader);                
            }

            this.logger.LogTrace("(-)");
            //});
        }

        /// <inheritdoc />
        public bool ProcessTransaction(Transaction transaction, int? blockHeight = null, Block block = null, MerkleProofTemplate merkleProofTemplate = null, bool isPropagated = true)
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
                    if (this.addressByScriptLookup.ContainsKey(utxo.ScriptPubKey.Hash))
                    {
                        var address = this.addressByScriptLookup[utxo.ScriptPubKey.Hash];
                        this.AddTransactionToWallet(merkleProofTemplate, transaction, utxo, address, blockHeight, block,  isPropagated);
                        foundReceivingTrx = true;
                    }
                    else {
                        string destination = GetOutputDestinationAddress(utxo);
                        if (!String.IsNullOrEmpty(destination) && this.addressLookup.ContainsKey(destination)) {
                            var address = this.addressLookup[destination];
                            this.AddTransactionToWallet(merkleProofTemplate, transaction, utxo, address, blockHeight, block, isPropagated);
                            foundReceivingTrx = true;
                        }
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
                        {
                            return false;
                        }

                        WalletLinkedHdAddress walletAddress;
                        // Check if the destination script is one of the wallet's.
                        bool found = this.addressByScriptLookup.TryGetValue(o.ScriptPubKey.Hash, out walletAddress);

                        if (!found)
                        {
                            string destination = GetOutputDestinationAddress(o);
                            if (!String.IsNullOrEmpty(destination))
                            {
                                found = this.addressLookup.TryGetValue(destination, out walletAddress);
                            }
                        }

                        // Include the keys not included in our wallets (external payees).
                        if (!found)
                        {
                            return true;
                        }                            

                        // Include the keys that are in the wallet but that are for receiving
                        // addresses (which would mean the user paid itself). 
                        return !walletAddress.HdAddress.IsChangeAddress();
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
                if (this.addressByScriptLookup.TryGetValue(utxo.TxOut.ScriptPubKey.Hash, out WalletLinkedHdAddress linkedAddress))
                {
                    var address = linkedAddress.HdAddress;
                    long walletId = linkedAddress.WalletId;
                    if (walletId>0)
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
                if (this.addressByScriptLookup.TryGetValue(utxo.ScriptPubKey.Hash, out WalletLinkedHdAddress linkedAddress))
                {
                    var address = linkedAddress.HdAddress;
                    long walletId = linkedAddress.WalletId;
                    if (walletId > 0)
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
            TransactionData spentTransaction = null;
            WalletLinkedHdAddress currentWalletLinkedHdAddress = null;
            foreach (var walletLinkedHdAddress in this.addressByScriptLookup.Values)
            {
                spentTransaction = walletLinkedHdAddress.HdAddress.Transactions.SingleOrDefault(
                        t => (t.Id == spendingTransactionId) && (t.Index == spendingTransactionIndex)
                    );
                if (spentTransaction != null)
                {
                    currentWalletLinkedHdAddress = walletLinkedHdAddress;
                    break;
                }
            }

            if (spentTransaction == null | currentWalletLinkedHdAddress == null)
            {
                // Strange, why would it be null?
                this.logger.LogTrace("(-)[TX_NULL]");
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
                            destinationAddress = txOut.ScriptPubKey.GetDestinationAddress(this.network)?.ToString();
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
        /// <param name="merkleProofTemplate">Merkle template compiled against block so that there is less work on each transaction</param>
        /// <param name="transaction">The transaction from which details are added.</param>
        /// <param name="utxo">The unspent output to add to the wallet.</param>
        /// <param name="blockHeight">Height of the block.</param>
        /// <param name="block">The block containing the transaction to add.</param>
        /// <param name="isPropagated">Propagation state of the transaction.</param>
        private void AddTransactionToWallet(MerkleProofTemplate merkleProofTemplate, Transaction transaction, TxOut utxo, WalletLinkedHdAddress walletLinkedHdAddress, int? blockHeight = null, Block block = null,  bool isPropagated = true)
        {
            Guard.NotNull(transaction, nameof(transaction));
            Guard.NotNull(utxo, nameof(utxo));

            uint256 transactionHash = transaction.GetHash();

            this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(transaction), transactionHash, nameof(blockHeight), blockHeight);

            // Get the collection of transactions to add to.
            //Script script = utxo.ScriptPubKey;

            //this.addressByScriptLookup.TryGetValue(script.Hash, out WalletLinkedHdAddress walletLinkedHdAddress);
            Guard.NotNull(walletLinkedHdAddress, nameof(WalletLinkedHdAddress));
            //this.keysLookup.TryGetValue(script, out HdAddress address);
            ICollection<TransactionData> addressTransactions = walletLinkedHdAddress.HdAddress.Transactions;

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
                    ScriptPubKey = utxo.ScriptPubKey,
                    Hex = this.walletSettings.SaveTransactionHex ? transaction.ToHex() : null,
                    IsPropagated = isPropagated
                };

                // Add the Merkle proof to the (non-spending) transaction.
                if (block != null)
                {
                    if (merkleProofTemplate != null)
                    {
                        newTransaction.MerkleProof = new MerkleBlock(merkleProofTemplate, block, new[] { transactionHash }).PartialMerkleTree;
                    }
                    else
                    {
                        newTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
                    }
                    
                }

                addressTransactions.Add(newTransaction);

                this.repository.SaveTransaction(walletLinkedHdAddress.WalletId, walletLinkedHdAddress.HdAddress, newTransaction);
                
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
                    if (merkleProofTemplate != null)
                    {
                        foundTransaction.MerkleProof = new MerkleBlock(merkleProofTemplate, block, new[] { transactionHash }).PartialMerkleTree;
                    }
                    else
                    {
                        foundTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
                    }
                }
                
                if (isPropagated)
                {
                    foundTransaction.IsPropagated = true;
                }                    

                this.repository.SaveTransaction(walletLinkedHdAddress.WalletId, walletLinkedHdAddress.HdAddress, foundTransaction);
            }
            this.repository.SaveAddress(walletLinkedHdAddress.WalletId, walletLinkedHdAddress.HdAddress);
            this.TransactionFoundInternal_New(utxo.ScriptPubKey);

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

            TransactionData spentTransaction = null;
            WalletLinkedHdAddress currentWalletLinkedHdAddress = null;
            foreach (var walletLinkedHdAddress in this.addressByScriptLookup.Values)
            {
                spentTransaction = walletLinkedHdAddress.HdAddress.Transactions.SingleOrDefault(
                        t => (t.Id == spendingTransactionId) && (t.Index == spendingTransactionIndex)
                    );
                if (spentTransaction != null)
                {
                    currentWalletLinkedHdAddress = walletLinkedHdAddress;
                    break;
                }              
            }

            if (spentTransaction == null | currentWalletLinkedHdAddress == null)
            {
                // Strange, why would it be null?
                this.logger.LogTrace("(-)[TX_NULL]");
                return;
            }
            // If the details of this spending transaction are seen for the first time.
            if (spentTransaction?.SpendingDetails == null)
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
            //problem here that in memory version of wallet/address shomehow does not represent full transaction structure.
            
            this.repository.SaveAddress(currentWalletLinkedHdAddress.WalletId, currentWalletLinkedHdAddress.HdAddress);
            this.repository.SaveTransaction(currentWalletLinkedHdAddress.WalletId, currentWalletLinkedHdAddress.HdAddress, spentTransaction);            
            
            this.logger.LogTrace("(-)");
        }


        /// <summary>
        /// this is suboptimal but rolled back version for testing
        /// </summary>
        /// <param name="script"></param>
        private void TransactionFoundInternal(Script script)
        {
            this.logger.LogTrace("()");
            bool found = false;
            bool isChange = false;
            HdAccount hdAccount = null;
            Wallet wallet = null;
            foreach (string walletName in this.repository.GetAllWalletNames())
            {
                wallet = this.repository.GetWalletByName(walletName);
                foreach (HdAccount account in wallet.GetAccountsByCoinType(this.coinType))
                {
                    hdAccount = account;
                    if (account.ExternalAddresses.Any(address => address.ScriptPubKey == script))
                    {
                        isChange = false;
                        found = true;
                        break;
                    }
                    else if (account.InternalAddresses.Any(address => address.ScriptPubKey == script))
                    {
                        isChange = true;
                        found = true;
                        break;
                    }
                    else
                    {
                        continue;
                    }
                }
                if (found)
                {
                    break;
                }
            }
            if (found)
            { // Calculate how many accounts to add to keep a buffer of 20 unused addresses.
                var lastAddy = hdAccount.GetLastUsedAddress(isChange);
                int lastUsedAddressIndex = 0;
                if (lastAddy != null)
                {
                    lastUsedAddressIndex = lastAddy.Index;
                }
                int addressesCount = isChange ? hdAccount.InternalAddresses.Count() : hdAccount.ExternalAddresses.Count();
                int emptyAddressesCount = addressesCount - lastUsedAddressIndex - 1;
                int accountsToAdd = UnusedAddressesBuffer - emptyAddressesCount;
                var newAddresses = hdAccount.CreateAddresses(this.network, accountsToAdd, isChange);

                List<WalletLinkedHdAddress> walletLinkerList = new List<WalletLinkedHdAddress>();
                foreach (var address in newAddresses)
                {
                    walletLinkerList.Add(new WalletLinkedHdAddress(address, wallet.Id));
                }
                this.UpdateKeysLookupLock(walletLinkerList);

            }
            this.logger.LogTrace("()");
        }
        private void TransactionFoundInternal_New(Script script)
        {
            this.logger.LogTrace("()");
            bool found = false;
            bool isChange = false;
            HdAccount hdAccount = null;
            Wallet wallet = null;


            wallet = this.repository.GetWalletByScriptHash(script.Hash.ToString());
            if (wallet != null)
            {
                foreach (HdAccount account in wallet.GetAccountsByCoinType(this.coinType))
                {
                    hdAccount = account;
                    if (account.ExternalAddresses.Any(address => address.ScriptPubKey == script))
                    {
                        isChange = false;
                        found = true;
                        break;
                    }
                    else if (account.InternalAddresses.Any(address => address.ScriptPubKey == script))
                    {
                        isChange = true;
                        found = true;
                        break;
                    }
                    else
                    {
                        continue;
                    }
                }
                if (found)
                { // Calculate how many accounts to add to keep a buffer of 20 unused addresses.
                    var lastAddy = hdAccount.GetLastUsedAddress(isChange);
                    int lastUsedAddressIndex = 0;
                    if (lastAddy != null)
                    {
                        lastUsedAddressIndex = lastAddy.Index;
                    }
                    int addressesCount = isChange ? hdAccount.InternalAddresses.Count() : hdAccount.ExternalAddresses.Count();
                    int emptyAddressesCount = addressesCount - lastUsedAddressIndex - 1;
                    int accountsToAdd = UnusedAddressesBuffer - emptyAddressesCount;
                    var newAddresses = hdAccount.CreateAddresses(this.network, accountsToAdd, isChange);

                    List<WalletLinkedHdAddress> walletLinkerList = new List<WalletLinkedHdAddress>();
                    foreach (var address in newAddresses)
                    {
                        walletLinkerList.Add(new WalletLinkedHdAddress(address, wallet.Id));
                        this.repository.SaveAddress(wallet.Id, address);
                    }
                    this.UpdateKeysLookupLock(walletLinkerList);
                }
            }            
            
            this.logger.LogTrace("()");
        }

        /// <inheritdoc />
        public void DeleteWallet(string walletName)
        {
            this.repository.RemoveWallet(walletName);
        }
       

        /// <inheritdoc />
        public void SaveWallet(Wallet wallet,bool saveTransactions=false)
        {
            Guard.NotNull(wallet, nameof(wallet));
            this.logger.LogTrace("({0}:'{1}')", nameof(wallet), wallet.Name);

            this.repository.SaveWallet(wallet.Name, wallet, saveTransactions);
            wallet.Saved();

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
            this.logger.LogTrace("({0}:'{1}'')", nameof(chainedHeader), chainedHeader);

            // The block locator will help when the wallet
            // needs to rewind this will be used to find the fork.
            //repository.SaveBlockLocator(chainedHeader.GetLocator());
            repository.SaveLastSyncedBlock(chainedHeader);
            this.WalletTipHash = chainedHeader.HashBlock;
            this.logger.LogTrace("(-)");
        }
       
        /// <inheritdoc />
        public void UpdateLastBlockSyncedHeight(string walletName, ChainedHeader chainedHeader)
        {
            Guard.NotNull(walletName, nameof(walletName));
            Guard.NotNull(chainedHeader, nameof(chainedHeader));
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(walletName), walletName, nameof(chainedHeader), chainedHeader);

            // The block locator will help when the wallet
            // needs to rewind this will be used to find the fork.
            repository.SaveLastSyncedBlock(walletName, chainedHeader);
            this.WalletTipHash = chainedHeader.HashBlock;

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
        private Wallet GenerateWallet(string name, string encryptedSeed, byte[] chainCode, DateTimeOffset? creationTime = null)
        {
            Guard.NotEmpty(name, nameof(name));
            Guard.NotEmpty(encryptedSeed, nameof(encryptedSeed));
            Guard.NotNull(chainCode, nameof(chainCode));
            this.logger.LogTrace("({0}:'{1}')", nameof(name), name);

            // Check if any wallet file already exists, with case insensitive comparison.
            if ((Wallet)this.repository.GetWalletByName(name) != null)
            {
                this.logger.LogTrace("(-)[WALLET_ALREADY_EXISTS]");
                throw new WalletException($"Wallet with name '{name}' already exists.");
            }            

            //List<Wallet> similarWallets = this.Wallets.Where(w => w.EncryptedSeed == encryptedSeed).ToList();
            //if (similarWallets.Any())
            //{
            //    this.logger.LogTrace("(-)[SAME_PK_ALREADY_EXISTS]");
            //    throw new WalletException("Cannot create this wallet as a wallet with the same private key already exists. If you want to restore your wallet from scratch, " +
            //                                        $"please remove the file {string.Join(", ", similarWallets.Select(w => w.Name))}.{WalletFileExtension} from '{this.FileStorage.FolderPath}' and try restoring the wallet again. " +
            //                                        "Make sure you have your mnemonic and your password handy!");
            //}

            Wallet walletFile = new Wallet
            {
                Name = name,
                EncryptedSeed = encryptedSeed,
                ChainCode = chainCode,
                CreationTime = creationTime ?? this.dateTimeProvider.GetTimeOffset(),
                Network = this.network,
                AccountsRoot = new List<AccountRoot> { new AccountRoot() { Accounts = new List<HdAccount>(), CoinType = this.coinType } },
            };
            
            this.logger.LogTrace("(-)");
            return walletFile;
        }


        /// <summary>
        /// Loads the keys and transactions we're tracking in memory for faster lookups.
        /// </summary>
        public void LoadKeysLookupLock()
        {
            int count = 0;
            Stopwatch sw = new Stopwatch();
            lock (this.lockObject)
            {
                var col = this.repository.GetAllWalletPointers();
                int length = col.Count();
                foreach (WalletPointer pointer in col)
                {
                    sw.Start();
                    count++;
                    IEnumerable<HdAddress> addresses = repository.GetAllWalletAddressesByCoinType(pointer.WalletName, this.coinType);
                    foreach (HdAddress address in addresses)
                    {
                        Script script = address.ScriptPubKey;
                        WalletLinkedHdAddress walletLinkedHdAddress = new WalletLinkedHdAddress(address, pointer.WalletId);
                        this.addressByScriptLookup.TryAdd<ScriptId, WalletLinkedHdAddress>(address.ScriptPubKey.Hash, walletLinkedHdAddress);
                        if (address.Pubkey != null)
                        {
                            this.addressByScriptLookup.TryAdd<ScriptId, WalletLinkedHdAddress>(address.Pubkey.Hash, walletLinkedHdAddress);
                        }
                        foreach (var transaction in address.Transactions)
                        {
                            this.outpointLookup[new OutPoint(transaction.Id, transaction.Index)] = transaction;
                        }
                        this.addressLookup.TryAdd<string, WalletLinkedHdAddress>(address.Address, walletLinkedHdAddress);
                    }
                    sw.Stop();
                    this.logger.LogInformation($"Loading wallet from db: {pointer.WalletName} #{count} / of {length} duration {sw.ElapsedMilliseconds}ms {Math.Round((double)((double)count / (double)length) * 100, 2) }% complete");
                    sw.Reset();                    
                }
            }
        }

        /// <summary>
        /// Update the keys and transactions we're tracking in memory for faster lookups.
        /// </summary>
        public void UpdateKeysLookupLock(IEnumerable<WalletLinkedHdAddress> addresses)
        {
            if (addresses == null || !addresses.Any())
            {
                return;
            }

            lock (this.lockObject)
            {
                foreach (WalletLinkedHdAddress walletAddress in addresses)
                {
                    if (walletAddress.HdAddress.Pubkey != null)
                    {
                        this.addressByScriptLookup[walletAddress.HdAddress.Pubkey.Hash] = walletAddress;
                    }
                    this.addressByScriptLookup[walletAddress.HdAddress.ScriptPubKey.Hash] = walletAddress;
                    this.addressLookup[walletAddress.HdAddress.Address] = walletAddress;
                }
            }
        }
        private string StringToHex(Script script)
        {
            return Encoders.Hex.EncodeData(script.ToBytes(false));
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
        public IEnumerable<string> GetWalletNames()
        {
            return this.repository.GetAllWalletNames();
        }

        /// <inheritdoc />
        public Wallet GetWalletByName(string walletName)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(walletName), walletName);

            Wallet wallet = this.repository.GetWalletByName(walletName);
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
            return this.repository.GetFirstWalletBlockLocator();
        }

        /// <inheritdoc />
        public int? GetEarliestWalletHeight()
        {
            return this.repository.GetEarliestWalletHeight();            
        }

        /// <inheritdoc />
        public DateTimeOffset GetOldestWalletCreationTime()
        {
            return this.repository.GetOldestWalletCreationTime();   
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
                                this.repository.RemoveTransactionFromHdAddress(address, transaction.Id);
                            }

                            // Remove the spending transaction object containing this transaction id.
                            if (transaction.SpendingDetails != null && !transaction.SpendingDetails.IsSpentConfirmed() && idsToRemove.Contains(transaction.SpendingDetails.TransactionId))
                            {
                                result.Add((transaction.SpendingDetails.TransactionId, transaction.SpendingDetails.CreationTime));
                                address.Transactions.ElementAt(i).SpendingDetails = null;
                                this.repository.RemoveTransactionSpendingDetailsFromHdAddress(address, transaction.SpendingDetails.TransactionId); 
                            }
                        }
                    }
                }
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
                this.SaveWallet(wallet,true);
            }

            return removedTransactions;
        }

        /// <summary>
        /// Updates details of the last block synced in a wallet when the chain of headers finishes downloading.
        /// </summary>
        /// <param name="wallets">The wallets to update when the chain has downloaded.</param>
        /// <param name="date">The creation date of the block with which to update the wallet.</param>
        private void UpdateWhenChainDownloaded(IEnumerable<string> wallets, DateTime date)
        {
            this.asyncLoopFactory.RunUntil("WalletManager.DownloadChain", this.nodeLifetime.ApplicationStopping,
                () => this.chain.IsDownloaded(),
                () =>
                {
                    int heightAtDate = this.chain.GetHeightAtTime(date);

                    foreach (var walletName in wallets)
                    {
                        this.logger.LogTrace("The chain of headers has finished downloading, updating wallet '{0}' with height {1}", walletName, heightAtDate);
                        this.UpdateLastBlockSyncedHeight(walletName, this.chain.GetBlock(heightAtDate));
                        //this.SaveWallet(wallet);
                    }
                },
                (ex) =>
                {
                    // In case of an exception while waiting for the chain to be at a certain height, we just cut our losses and
                    // sync from the current height.
                    this.logger.LogError($"Exception occurred while waiting for chain to download: {ex.Message}");

                    foreach (var walletName in wallets)
                    {
                        this.UpdateLastBlockSyncedHeight(walletName, this.chain.Tip);
                    }
                },
                TimeSpans.FiveSeconds);
        }


        public Wallet GetWalletByAddress(string address)
        {
            return this.repository.GetWalletByAddress(address);
        }

        public void UpdateKeysLookupLock(IEnumerable<HdAddress> addresses, string walletName)
        {
            throw new NotImplementedException();
        }
    }
}
