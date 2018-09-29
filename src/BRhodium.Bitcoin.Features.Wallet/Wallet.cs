using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using BRhodium.Node.Utilities;
using BRhodium.Node.Utilities.JsonConverters;

namespace BRhodium.Bitcoin.Features.Wallet
{
    /// <summary>
    /// A wallet.
    /// </summary>
    public class Wallet : IBitcoinSerializable
    {
        /// <summary>
        /// Initializes a new instance of the wallet.
        /// </summary>
        public Wallet()
        {
            this.AccountsRoot = new List<AccountRoot>();
        }
        private bool changed = false;
        private byte[] _name = Array.Empty<byte>();
        private byte[] _chainCode = Array.Empty<byte>();
        private byte[] _encryptedSeed = Array.Empty<byte>();
        private UInt32 _creationTime;
        private long _id;
        private List<AccountRoot> _accountsRoot;
        private Network _network;
   
        
        public void ReadWrite(BitcoinStream stream)
        {
            byte[] networkNameShadow = Array.Empty<byte>();
            if (_network != null) {
                networkNameShadow = StringToByteArray(_network.ToString());
            }
            
            stream.ReadWrite(ref this._id);
            stream.ReadWrite(ref this._creationTime);
            stream.ReadWriteAsVarString(ref this._name);
            stream.ReadWriteAsVarString(ref this._encryptedSeed);
            stream.ReadWriteAsVarString(ref this._chainCode);
            stream.ReadWriteAsVarString(ref networkNameShadow);

            stream.ReadWrite<List<AccountRoot>, AccountRoot>(ref this._accountsRoot);
            if (stream.Serializing)
            {
                stream.Inner.Flush();
            }
            else
            {
                var network = GetNetwork(networkNameShadow);
                if (network != null) {
                    this.Network = network;
                }               
            }
        }

        private byte[] StringToByteArray(string value) {
            return System.Text.Encoding.UTF8.GetBytes(value);
        }
        private string ByteArrayToString(byte[] value)
        {
            return System.Text.Encoding.UTF8.GetString(value);
        }

        private Network GetNetwork(byte[] networkName) {
            return NetworkHelpers.GetNetwork(ByteArrayToString(networkName));
        }

        public static Wallet Load(byte[] bytes, Network network = null)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            var wallet = new Wallet();
            wallet.FromBytes(bytes, network: network);
            return wallet;
        }
        /// <summary>
        /// The name of this wallet.
        /// </summary>
        [JsonProperty(PropertyName = "name")]
        public string Name
        {
            get
            {
                if (this._name != null)
                {
                    return System.Text.Encoding.UTF8.GetString(this._name);
                }
                return null;
            }
            set
            {
                if (value != null)
                {
                    this._name = System.Text.Encoding.UTF8.GetBytes(value);
                }
                else
                {
                    this._name = Array.Empty<byte>();
                }
            }
        }

        /// <summary>
        /// The seed for this wallet, password encrypted.
        /// </summary>
        [JsonProperty(PropertyName = "encryptedSeed")]
        public string EncryptedSeed
        {
            get
            {
                if (this._encryptedSeed != null)
                {
                    return System.Text.Encoding.UTF8.GetString(this._encryptedSeed);
                }
                return null;
            }
            set
            {
                if (value != null)
                {
                    this._encryptedSeed = System.Text.Encoding.UTF8.GetBytes(value);
                }
                else
                {
                    this._encryptedSeed = Array.Empty<byte>();
                }
            }
        }

        /// <summary>
        /// The chain code.
        /// </summary>
        [JsonProperty(PropertyName = "chainCode")]
        [JsonConverter(typeof(ByteArrayConverter))]
        public byte[] ChainCode
        {
            get
            {
                if (this._chainCode == null)
                {
                    this._chainCode = Array.Empty<byte>();
                }
                return this._chainCode;
            }
            set
            {
                if (value == null)
                {
                    this._chainCode = Array.Empty<byte>();
                    return;
                }
                this._chainCode = value;
            }
        }

        /// <summary>
        /// Gets or sets the merkle path.
        /// </summary>
        [JsonProperty(PropertyName = "blockLocator", ItemConverterType = typeof(UInt256JsonConverter))]
        public List<uint256> BlockLocator { get; set; }

        /// <summary>
        /// The network this wallet is for.
        /// </summary>
        [JsonProperty(PropertyName = "network")]
        [JsonConverter(typeof(NetworkConverter))]
        public Network Network
        {
            get
            {
                return _network;
            }
            set
            {
                _network = value;
            }
        }

        /// <summary>
        /// The time this wallet was created.
        /// </summary>
        [JsonProperty(PropertyName = "creationTime")]
        [JsonConverter(typeof(DateTimeOffsetConverter))]
        public DateTimeOffset CreationTime
        {
            get
            {
                return Utils.UnixTimeToDateTime(this._creationTime);
            }
            set
            {
                this._creationTime = Utils.DateTimeToUnixTime(value);
            }
        }

        /// <summary>
        /// The root of the accounts tree.
        /// </summary>
        [JsonProperty(PropertyName = "accountsRoot")]
        public List<AccountRoot> AccountsRoot
        {
            get
            {
                return this._accountsRoot;
            }
            set
            {
                this._accountsRoot = value;
            }
        }
        [JsonIgnore]
        public long Id
        {
            get
            {
                return this._id;
            }
            internal set
            {
                this._id = value;
            }
        }

        /// <summary>
        /// This is sets a runtime flag to show if wallet has been changed since last save operation.
        /// </summary>
        public void Changed()
        {
            this.changed = true;
        }
        /// <summary>
        /// This resets a runtime chaged flag after wallet has been saved;
        /// </summary>
        public void Saved()
        {
            this.changed = false;
        }
        /// <summary>
        /// Returns bool describing if wallet has been changed since last save.
        /// </summary>
        /// <returns></returns>
        public bool IsChanged()
        {
            return this.changed;
        }

        /// <summary>
        /// Gets the accounts the wallet has for this type of coin.
        /// </summary>
        /// <param name="coinType">Type of the coin.</param>
        /// <returns>The accounts in the wallet corresponding to this type of coin.</returns>
        public IEnumerable<HdAccount> GetAccountsByCoinType(CoinType coinType)
        {
            return this.AccountsRoot.Where(a => a.CoinType == coinType).SelectMany(a => a.Accounts);
        }

        /// <summary>
        /// Gets an account from the wallet's accounts.
        /// </summary>
        /// <param name="accountName">The name of the account to retrieve.</param>
        /// <param name="coinType">The type of the coin this account is for.</param>
        /// <returns>The requested account.</returns>
        public HdAccount GetAccountByCoinType(string accountName, CoinType coinType)
        {
            AccountRoot accountRoot = this.AccountsRoot.SingleOrDefault(a => a.CoinType == coinType);
            return accountRoot?.GetAccountByName(accountName);
        }


        public HdAccount GetAccountByHdPathCoinType(string hdPath, CoinType coinType)
        {
            AccountRoot accountRoot = this.AccountsRoot.SingleOrDefault(a => a.CoinType == coinType);
            return accountRoot?.GetAccountByHdPath(hdPath);
        }

        /// <summary>
        /// Update the last block synced height and hash in the wallet.
        /// </summary>
        /// <param name="coinType">The type of the coin this account is for.</param>
        /// <param name="block">The block whose details are used to update the wallet.</param>
        public void SetLastBlockDetailsByCoinType(CoinType coinType, ChainedHeader block)
        {
            AccountRoot accountRoot = this.AccountsRoot.SingleOrDefault(a => a.CoinType == coinType);

            if (accountRoot == null) return;

            accountRoot.LastBlockSyncedHeight = block.Height;
            accountRoot.LastBlockSyncedHash = block.HashBlock;
        }

        /// <summary>
        /// Gets all the transactions by coin type.
        /// </summary>
        /// <param name="coinType">Type of the coin.</param>
        /// <returns></returns>
        public IEnumerable<TransactionData> GetAllTransactionsByCoinType(CoinType coinType)
        {
            var accounts = this.GetAccountsByCoinType(coinType).ToList();

            foreach (TransactionData txData in accounts.SelectMany(x => x.ExternalAddresses).SelectMany(x => x.Transactions))
            {
                yield return txData;
            }

            foreach (TransactionData txData in accounts.SelectMany(x => x.InternalAddresses).SelectMany(x => x.Transactions))
            {
                yield return txData;
            }
        }

        /// <summary>
        /// Gets all the pub keys contained in this wallet.
        /// </summary>
        /// <param name="coinType">Type of the coin.</param>
        /// <returns></returns>
        public IEnumerable<Script> GetAllPubKeysByCoinType(CoinType coinType)
        {
            var accounts = this.GetAccountsByCoinType(coinType).ToList();

            foreach (Script script in accounts.SelectMany(x => x.ExternalAddresses).Select(x => x.ScriptPubKey))
            {
                yield return script;
            }

            foreach (Script script in accounts.SelectMany(x => x.InternalAddresses).Select(x => x.ScriptPubKey))
            {
                yield return script;
            }
        }

        /// <summary>
        /// Gets all the addresses contained in this wallet.
        /// </summary>
        /// <param name="coinType">Type of the coin.</param>
        /// <returns>A list of all the addresses contained in this wallet.</returns>
        public IEnumerable<HdAddress> GetAllAddressesByCoinType(CoinType coinType)
        {
            var accounts = this.GetAccountsByCoinType(coinType).ToList();

            List<HdAddress> allAddresses = new List<HdAddress>();
            foreach (HdAccount account in accounts)
            {
                allAddresses.AddRange(account.GetCombinedAddresses());
            }
            return allAddresses;
        }


        /// <summary>
        /// Adds an account to the current wallet.
        /// </summary>
        /// <remarks>
        /// The name given to the account is of the form "account (i)" by default, where (i) is an incremental index starting at 0.
        /// According to BIP44, an account at index (i) can only be created when the account at index (i - 1) contains at least one transaction.
        /// </remarks>
        /// <seealso cref="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki"/>
        /// <param name="password">The password used to decrypt the wallet's <see cref="EncryptedSeed"/>.</param>
        /// <param name="coinType">The type of coin this account is for.</param>
        /// <param name="accountCreationTime">Creation time of the account to be created.</param>
        /// <returns>A new HD account.</returns>
        public HdAccount AddNewAccount(string password, CoinType coinType, DateTimeOffset accountCreationTime)
        {
            Guard.NotEmpty(password, nameof(password));

            var accountRoot = this.AccountsRoot.Single(a => a.CoinType == coinType);
            return accountRoot.AddNewAccount(password, this.EncryptedSeed, this.ChainCode, this.Network, accountCreationTime);
        }

        /// <summary>
        /// Gets the first account that contains no transaction.
        /// </summary>
        /// <returns>An unused account.</returns>
        public HdAccount GetFirstUnusedAccount(CoinType coinType)
        {
            // Get the accounts root for this type of coin.
            var accountsRoot = this.AccountsRoot.Single(a => a.CoinType == coinType);

            if (accountsRoot.Accounts.Any())
            {
                // Get an unused account.
                var firstUnusedAccount = accountsRoot.GetFirstUnusedAccount();
                if (firstUnusedAccount != null)
                {
                    return firstUnusedAccount;
                }
            }

            return null;
        }

        /// <summary>
        /// Determines whether the wallet contains the specified address.
        /// </summary>
        /// <param name="address">The address to check.</param>
        /// <returns>A value indicating whether the wallet contains the specified address.</returns>
        public bool ContainsAddress(HdAddress address)
        {
            if (!this.AccountsRoot.Any(r => r.Accounts.Any(
                a => a.ExternalAddresses.Any(i => i.Address == address.Address) ||
                     a.InternalAddresses.Any(i => i.Address == address.Address))))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the extended private key for the given address.
        /// </summary>
        /// <param name="password">The password used to encrypt/decrypt sensitive info.</param>
        /// <param name="address">The address to get the private key for.</param>
        /// <returns>The extended private key.</returns>
        public ISecret GetExtendedPrivateKeyForAddress(string password, HdAddress address)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotNull(address, nameof(address));

            // Check if the wallet contains the address.
            if (!this.ContainsAddress(address))
            {
                throw new WalletException("Address not found on wallet.");
            }

            // get extended private key
            Key privateKey = HdOperations.DecryptSeed(this.EncryptedSeed, password, this.Network);
            return HdOperations.GetExtendedPrivateKey(privateKey, this.ChainCode, address.HdPath, this.Network);
        }

        /// <summary>
        /// Lists all spendable transactions from all accounts in the wallet.
        /// </summary>
        /// <param name="coinType">Type of the coin to get transactions from.</param>
        /// <param name="currentChainHeight">Height of the current chain, used in calculating the number of confirmations.</param>
        /// <param name="confirmations">The number of confirmations required to consider a transaction spendable.</param>
        /// <returns>A collection of spendable outputs.</returns>
        public IEnumerable<UnspentOutputReference> GetAllSpendableTransactions(CoinType coinType, int currentChainHeight, int confirmations = 0)
        {
            IEnumerable<HdAccount> accounts = this.GetAccountsByCoinType(coinType);

            return accounts
                .SelectMany(x => x.GetSpendableTransactions(currentChainHeight, confirmations));
        }

    }

    /// <summary>
    /// The root for the accounts for any type of coins.
    /// </summary>
    public class AccountRoot : IBitcoinSerializable
    {
        private int _coinType;
        private List<HdAccount> _accounts;

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        public AccountRoot()
        {
            this.Accounts = new List<HdAccount>();
        }
        /// <summary>
        /// Implmenting IBitcoinSerializable member to make this object serializebale.
        /// </summary>
        /// <param name="stream"></param>
        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this._coinType);
            stream.ReadWrite<List<HdAccount>, HdAccount>(ref this._accounts);
            if (stream.Serializing)
            {
                stream.Inner.Flush();
            }
            //block sychronization position is kept separately so no need to serialize
        }
        /// <summary>
        /// The type of coin, Bitcoin or BRhodium.
        /// </summary>
        [JsonProperty(PropertyName = "coinType")]
        public CoinType CoinType
        {
            get
            {
                return (CoinType)this._coinType;
            }
            set
            {
                this._coinType = (int) value;
            }
        }

        /// <summary>
        /// The height of the last block that was synced.
        /// </summary>
        [JsonProperty(PropertyName = "lastBlockSyncedHeight", NullValueHandling = NullValueHandling.Ignore)]
        public int? LastBlockSyncedHeight { get; set; }

        /// <summary>
        /// The hash of the last block that was synced.
        /// </summary>
        [JsonProperty(PropertyName = "lastBlockSyncedHash", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 LastBlockSyncedHash { get; set; }

        /// <summary>
        /// The accounts used in the wallet.
        /// </summary>
        [JsonProperty(PropertyName = "accounts")]
        public List<HdAccount> Accounts
        {
            get
            {
                return this._accounts;
            }
            set
            {
                this._accounts = value;
            }
        }

        /// <summary>
        /// Gets the first account that contains no transaction.
        /// </summary>
        /// <returns>An unused account</returns>
        public HdAccount GetFirstUnusedAccount()
        {
            if (this.Accounts == null)
                return null;

            var unusedAccounts = this.Accounts.Where(acc => !acc.ExternalAddresses.Any() && !acc.InternalAddresses.Any()).ToList();
            if (!unusedAccounts.Any())
                return null;

            // gets the unused account with the lowest index
            var index = unusedAccounts.Min(a => a.Index);
            return unusedAccounts.Single(a => a.Index == index);
        }

        /// <summary>
        /// Gets the account matching the name passed as a parameter.
        /// </summary>
        /// <param name="accountName">The name of the account to get.</param>
        /// <returns></returns>
        /// <exception cref="System.Exception"></exception>
        public HdAccount GetAccountByName(string accountName)
        {
            if (this.Accounts == null)
                throw new WalletException($"No account with the name {accountName} could be found.");

            // get the account
            HdAccount account = this.Accounts.SingleOrDefault(a => a.Name == accountName);
            if (account == null)
                throw new WalletException($"No account with the name {accountName} could be found.");

            return account;
        }

        public HdAccount GetAccountByHdPath(string hdPath)
        {
            if (this.Accounts == null)
                throw new WalletException($"No account with the hd path {hdPath} could be found.");

            // get the account
            HdAccount account = this.Accounts.SingleOrDefault(a => hdPath.Contains(a.HdPath));
            if (account == null)
                throw new WalletException($"No account with the hd path {hdPath} could be found.");

            return account;
        }

        /// <summary>
        /// Adds an account to the current account root.
        /// </summary>
        /// <remarks>The name given to the account is of the form "account (i)" by default, where (i) is an incremental index starting at 0.
        /// According to BIP44, an account at index (i) can only be created when the account at index (i - 1) contains transactions.
        /// <seealso cref="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki"/></remarks>
        /// <param name="password">The password used to decrypt the wallet's encrypted seed.</param>
        /// <param name="encryptedSeed">The encrypted private key for this wallet.</param>
        /// <param name="chainCode">The chain code for this wallet.</param>
        /// <param name="network">The network for which this account will be created.</param>
        /// <param name="accountCreationTime">Creation time of the account to be created.</param>
        /// <returns>A new HD account.</returns>
        public HdAccount AddNewAccount(string password, string encryptedSeed, byte[] chainCode, Network network, DateTimeOffset accountCreationTime)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotEmpty(encryptedSeed, nameof(encryptedSeed));
            Guard.NotNull(chainCode, nameof(chainCode));

            // Get the current collection of accounts.
            var accounts = this.Accounts.ToList();

            int newAccountIndex = 0;
            if (accounts.Any())
            {
                newAccountIndex = accounts.Max(a => a.Index) + 1;
            }

            // Get the extended pub key used to generate addresses for this account.
            string accountHdPath = HdOperations.GetAccountHdPath((int)this.CoinType, newAccountIndex);
            Key privateKey = HdOperations.DecryptSeed(encryptedSeed, password, network);
            ExtPubKey accountExtPubKey = HdOperations.GetExtendedPublicKey(privateKey, chainCode, accountHdPath);

            var newAccount = new HdAccount
            {
                Index = newAccountIndex,
                ExtendedPubKey = accountExtPubKey.ToString(network),
                ExternalAddresses = new List<HdAddress>(),
                InternalAddresses = new List<HdAddress>(),
                Name = $"account {newAccountIndex}",
                HdPath = accountHdPath,
                CreationTime = accountCreationTime
            };

            accounts.Add(newAccount);
            this.Accounts = accounts;

            return newAccount;
        }


    }

    /// <summary>
    /// An HD account's details.
    /// </summary>
    public class HdAccount : IBitcoinSerializable
    {
        private int _index;
        private byte[] _name = Array.Empty<byte>();
        private byte[] _hdPath = Array.Empty<byte>();
        private byte[] _extendedPubKey = Array.Empty<byte>();
        private UInt32 _creationTime;

        public HdAccount()
        {
            this.ExternalAddresses = new List<HdAddress>();
            this.InternalAddresses = new List<HdAddress>();
        }

        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this._index);
            stream.ReadWrite(ref this._creationTime);
            stream.ReadWriteAsVarString(ref this._name);
            stream.ReadWriteAsVarString(ref this._hdPath);
            stream.ReadWriteAsVarString(ref this._extendedPubKey);
            if (stream.Serializing)
            {
                stream.Inner.Flush();
            }
        }

        /// <summary>
        /// The index of the account.
        /// </summary>
        /// <remarks>
        /// According to BIP44, an account at index (i) can only be created when the account
        /// at index (i - 1) contains transactions.
        /// </remarks>
        [JsonProperty(PropertyName = "index")]
        public int Index
        {
            get
            {
                return this._index;
            }
            set
            {
                this._index = value;
            }
        }

        /// <summary>
        /// The name of this account.
        /// </summary>
        [JsonProperty(PropertyName = "name")]
        public string Name
        {
            get
            {
                if (this._name != null)
                {
                    return System.Text.Encoding.UTF8.GetString(this._name);
                }
                return null;
            }
            set
            {
                if (value != null)
                {
                    this._name = System.Text.Encoding.UTF8.GetBytes(value);
                }
                else {
                    this._name = Array.Empty<byte>();
                }
            }
        }

        /// <summary>
        /// A path to the account as defined in BIP44.
        /// </summary>
        [JsonProperty(PropertyName = "hdPath")]
        public string HdPath
        {
            get
            {
                if (this._hdPath != null)
                {
                    return System.Text.Encoding.UTF8.GetString(this._hdPath);
                }
                return null;
            }
            set
            {
                if (value != null)
                {
                    this._hdPath = System.Text.Encoding.UTF8.GetBytes(value);
                }
                else
                {
                    this._hdPath = Array.Empty<byte>();
                }
            }
        }

        /// <summary>
        /// An extended pub key used to generate addresses.
        /// </summary>
        [JsonProperty(PropertyName = "extPubKey")]
        public string ExtendedPubKey
        {
            get
            {
                if (this._extendedPubKey != null)
                {
                    return System.Text.Encoding.UTF8.GetString(this._extendedPubKey);
                }
                return null;
            }
            set
            {
                if (value != null)
                {
                    this._extendedPubKey = System.Text.Encoding.UTF8.GetBytes(value);
                }
                else
                {
                    this._extendedPubKey = Array.Empty<byte>();
                }
            }
        }

        /// <summary>
        /// Gets or sets the creation time.
        /// </summary>
        [JsonProperty(PropertyName = "creationTime")]
        [JsonConverter(typeof(DateTimeOffsetConverter))]
        public DateTimeOffset CreationTime
        {
            get
            {
                return Utils.UnixTimeToDateTime(this._creationTime);
            }
            set
            {
                this._creationTime = Utils.DateTimeToUnixTime(value);
            }
        }

        /// <summary>
        /// The list of external addresses, typically used for receiving money.
        /// </summary>
        [JsonProperty(PropertyName = "externalAddresses")]
        public ICollection<HdAddress> ExternalAddresses { get; set; }

        /// <summary>
        /// The list of internal addresses, typically used to receive change.
        /// </summary>
        [JsonProperty(PropertyName = "internalAddresses")]
        public ICollection<HdAddress> InternalAddresses { get; set; }

        /// <summary>
        /// Gets the type of coin this account is for.
        /// </summary>
        /// <returns>A <see cref="CoinType"/>.</returns>
        public CoinType GetCoinType()
        {
            return (CoinType)HdOperations.GetCoinType(this.HdPath);
        }

        /// <summary>
        /// Gets the first receiving address that contains no transaction.
        /// </summary>
        /// <returns>An unused address</returns>
        public HdAddress GetFirstUnusedReceivingAddress()
        {
            return this.GetFirstUnusedAddress(false);
        }

        /// <summary>
        /// Gets the first change address that contains no transaction.
        /// </summary>
        /// <returns>An unused address</returns>
        public HdAddress GetFirstUnusedChangeAddress()
        {
            return this.GetFirstUnusedAddress(true);
        }

        /// <summary>
        /// Gets the first receiving address that contains no transaction.
        /// </summary>
        /// <returns>An unused address</returns>
        private HdAddress GetFirstUnusedAddress(bool isChange)
        {
            IEnumerable<HdAddress> addresses = isChange ? this.InternalAddresses : this.ExternalAddresses;
            if (addresses == null)
                return null;

            var unusedAddresses = addresses.Where(acc => !acc.Transactions.Any()).ToList();
            if (!unusedAddresses.Any())
            {
                return null;
            }

            // gets the unused address with the lowest index
            var index = unusedAddresses.Min(a => a.Index);
            return unusedAddresses.Single(a => a.Index == index);
        }

        /// <summary>
        /// Gets the last address that contains transactions.
        /// </summary>
        /// <param name="isChange">Whether the address is a change (internal) address or receiving (external) address.</param>
        /// <returns></returns>
        public HdAddress GetLastUsedAddress(bool isChange)
        {
            IEnumerable<HdAddress> addresses = isChange ? this.InternalAddresses : this.ExternalAddresses;
            if (addresses == null)
                return null;

            var usedAddresses = addresses.Where(acc => acc.Transactions.Any()).ToList();
            if (!usedAddresses.Any())
            {
                return null;
            }

            // gets the used address with the highest index
            var index = usedAddresses.Max(a => a.Index);
            return usedAddresses.Single(a => a.Index == index);
        }

        /// <summary>
        /// Gets a collection of transactions by id.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns></returns>
        public IEnumerable<TransactionData> GetTransactionsById(uint256 id)
        {
            Guard.NotNull(id, nameof(id));

            var addresses = this.GetCombinedAddresses();
            return addresses.Where(r => r.Transactions != null).SelectMany(a => a.Transactions.Where(t => t.Id == id));
        }

        /// <summary>
        /// Gets a collection of transactions with spendable outputs.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<TransactionData> GetSpendableTransactions()
        {
            var addresses = this.GetCombinedAddresses();
            return addresses.Where(r => r.Transactions != null).SelectMany(a => a.Transactions.Where(t => t.IsSpendable()));
        }

        /// <summary>
        /// Get the accounts total spendable value for both confirmed and unconfirmed UTXO.
        /// </summary>
        public (Money ConfirmedAmount, Money UnConfirmedAmount) GetSpendableAmount()
        {
            var allTransactions = this.ExternalAddresses.SelectMany(a => a.Transactions)
                .Concat(this.InternalAddresses.SelectMany(i => i.Transactions)).ToList();

            var confirmed = allTransactions.Sum(t => t.SpendableAmount(true));
            var total = allTransactions.Sum(t => t.SpendableAmount(false));

            return (confirmed, total - confirmed);
        }

        /// <summary>
        /// Finds the addresses in which a transaction is contained.
        /// </summary>
        /// <remarks>
        /// Returns a collection because a transaction can be contained in a change address as well as in a receive address (as a spend).
        /// </remarks>
        /// <param name="predicate">A predicate by which to filter the transactions.</param>
        /// <returns></returns>
        public IEnumerable<HdAddress> FindAddressesForTransaction(Func<TransactionData, bool> predicate)
        {
            Guard.NotNull(predicate, nameof(predicate));

            var addresses = this.GetCombinedAddresses();
            return addresses.Where(t => t.Transactions != null).Where(a => a.Transactions.Any(predicate));
        }

        /// <summary>
        /// Return both the external and internal (change) address from an account.
        /// </summary>
        /// <returns>All addresses that belong to this account.</returns>
        public IEnumerable<HdAddress> GetCombinedAddresses()
        {
            IEnumerable<HdAddress> addresses = new List<HdAddress>();
            if (this.ExternalAddresses != null)
            {
                addresses = this.ExternalAddresses;
            }

            if (this.InternalAddresses != null)
            {
                addresses = addresses.Concat(this.InternalAddresses);
            }

            return addresses;
        }

        /// <summary>
        /// Creates a number of additional addresses in the current account.
        /// </summary>
        /// <remarks>
        /// The name given to the account is of the form "account (i)" by default, where (i) is an incremental index starting at 0.
        /// According to BIP44, an account at index (i) can only be created when the account at index (i - 1) contains at least one transaction.
        /// </remarks>
        /// <param name="network">The network these addresses will be for.</param>
        /// <param name="addressesQuantity">The number of addresses to create.</param>
        /// <param name="isChange">Whether the addresses added are change (internal) addresses or receiving (external) addresses.</param>
        /// <returns>The created addresses.</returns>
        public IEnumerable<HdAddress> CreateAddresses(Network network, int addressesQuantity, bool isChange = false)
        {
            var addresses = isChange ? this.InternalAddresses : this.ExternalAddresses;

            // Get the index of the last address.
            int firstNewAddressIndex = 0;
            if (addresses.Any())
            {
                firstNewAddressIndex = addresses.Max(add => add.Index) + 1;
            }

            List<HdAddress> addressesCreated = new List<HdAddress>();
            for (int i = firstNewAddressIndex; i < firstNewAddressIndex + addressesQuantity; i++)
            {
                // Generate a new address.
                PubKey pubkey = HdOperations.GeneratePublicKey(this.ExtendedPubKey, i, isChange);
                BitcoinPubKeyAddress address = pubkey.GetAddress(network);

                // Add the new address details to the list of addresses.
                HdAddress newAddress = new HdAddress
                {
                    Index = i,
                    HdPath = HdOperations.CreateHdPath((int)this.GetCoinType(), this.Index, i, isChange),
                    ScriptPubKey = address.ScriptPubKey,
                    Pubkey = pubkey.ScriptPubKey,
                    Address = address.ToString(),
                    Transactions = new List<TransactionData>()
                };

                addresses.Add(newAddress);
                addressesCreated.Add(newAddress);
            }

            if (isChange)
            {
                this.InternalAddresses = addresses;
            }
            else
            {
                this.ExternalAddresses = addresses;
            }

            return addressesCreated;
        }

        /// <summary>
        /// Creates a number of additional addresses in the current account.
        /// </summary>
        /// <remarks>
        /// The name given to the account is of the form "account (i)" by default, where (i) is an incremental index starting at 0.
        /// According to BIP44, an account at index (i) can only be created when the account at index (i - 1) contains at least one transaction.
        /// </remarks>
        /// <param name="network">The network.</param>
        /// <param name="pubKey">The pub key.</param>
        /// <returns>The created address.</returns>
        public HdAddress CreateAddresses(Network network, string pubKey)
        {
            var addresses = this.ExternalAddresses;

            // Get the index of the last address.
            int firstNewAddressIndex = 0;
            if (addresses.Any())
            {
                firstNewAddressIndex = addresses.Max(add => add.Index) + 1;
            }

            var pubkey = new PubKey(pubKey);
            BitcoinPubKeyAddress address = pubkey.GetAddress(network);

            // Add the new address details to the list of addresses.
            HdAddress newAddress = new HdAddress
            {
                Index = firstNewAddressIndex,
                HdPath = HdOperations.CreateHdPath((int)this.GetCoinType(), this.Index, firstNewAddressIndex),
                ScriptPubKey = address.ScriptPubKey,
                Pubkey = pubkey.ScriptPubKey,
                Address = address.ToString(),
                Transactions = new List<TransactionData>()
            };

            addresses.Add(newAddress);

            this.ExternalAddresses = addresses;

            return newAddress;
        }

        /// <summary>
        /// BETA - Imports the address.
        /// </summary>
        /// <param name="network">The network.</param>
        /// <param name="base58Address">The base58 address.</param>
        /// <returns>The created address.</returns>
        public HdAddress ImportAddress(Network network, string base58Address)
        {
            var addresses = this.ExternalAddresses;

            // Get the index of the last address.
            int firstNewAddressIndex = 0;
            if (addresses.Any())
            {
                firstNewAddressIndex = addresses.Max(add => add.Index) + 1;
            }

            var address = new BitcoinPubKeyAddress(base58Address, network);
            var pubKeyTemplate = new PayToPubkeyTemplate();
            var pubkey = pubKeyTemplate.ExtractScriptPubKeyParameters(address.ScriptPubKey);

            // Add the new address details to the list of addresses.
            HdAddress importAddress = new HdAddress
            {
                Index = firstNewAddressIndex,
                HdPath = HdOperations.CreateHdPath((int)this.GetCoinType(), this.Index, firstNewAddressIndex),
                ScriptPubKey = address.ScriptPubKey,
                Pubkey = pubkey.ScriptPubKey,
                Address = address.ToString(),
                Transactions = new List<TransactionData>()
            };

            addresses.Add(importAddress);

            this.ExternalAddresses = addresses;

            return importAddress;
        }

        /// <summary>
        /// Lists all spendable transactions in the current account.
        /// </summary>
        /// <param name="currentChainHeight">The current height of the chain. Used for calculating the number of confirmations a transaction has.</param>
        /// <param name="confirmations">The minimum number of confirmations required for transactions to be considered.</param>
        /// <returns>A collection of spendable outputs that belong to the given account.</returns>
        public IEnumerable<UnspentOutputReference> GetSpendableTransactions(int currentChainHeight, int confirmations = 0)
        {
            // This will take all the spendable coins that belong to the account and keep the reference to the HDAddress and HDAccount.
            // This is useful so later the private key can be calculated just from a given UTXO.
            foreach (var address in this.GetCombinedAddresses())
            {
                // A block that is at the tip has 1 confirmation.
                // When calculating the confirmations the tip must be advanced by one.

                int countFrom = currentChainHeight + 1;
                foreach (TransactionData transactionData in address.UnspentTransactions())
                {
                    int? confirmationCount = 0;
                    if (transactionData.BlockHeight != null)
                        confirmationCount = countFrom >= transactionData.BlockHeight ? countFrom - transactionData.BlockHeight : 0;

                    if (confirmationCount >= confirmations)
                    {
                        yield return new UnspentOutputReference
                        {
                            Account = this,
                            Address = address,
                            Transaction = transactionData
                        };
                    }
                }
            }
        }

    }
    /// <summary>
    /// Provides a link composition between wallets and addresses from cached objects
    /// </summary>
    public class WalletLinkedHdAddress{
        private readonly HdAddress hdAddress;
        private readonly long walletId;
        /// <summary>
        /// Creates an instance of the linker object.
        /// </summary>
        /// <param name="hdAddress">Address ref</param>
        /// <param name="wallet">Wallet ref</param>
        public WalletLinkedHdAddress(HdAddress hdAddress, long walletId)
        {
            this.hdAddress = hdAddress;
            this.walletId = walletId;
        }

        public HdAddress HdAddress
        {
            get {
                return this.hdAddress;
            }
        }

        public long WalletId
        {
            get
            {
                return this.walletId;
            }
        }
    }
    /// <summary>
    /// An HD address.
    /// </summary>
    public class HdAddress : IBitcoinSerializable
    {
        private int _index;
        private Script _scriptPubKey;
        private Script _pubkey;
        private byte[] _address = Array.Empty<byte>();
        private byte[] _hdPath = Array.Empty<byte>();
        private long _id;
        private long _walletid;
        private List<TransactionData> _transactions;

        public HdAddress()
        {
            this.Transactions = new List<TransactionData>();
        }
        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this._id);
            stream.ReadWrite(ref this._walletid);
            stream.ReadWrite(ref this._index);
            stream.ReadWrite(ref this._scriptPubKey);
            stream.ReadWrite(ref this._pubkey);
            stream.ReadWriteAsVarString(ref this._address);
            stream.ReadWriteAsVarString(ref this._hdPath);
            bool hasTransactions = false;

            if (stream.Serializing)
            {
                if (this._transactions.Count > 0)
                {
                    hasTransactions = true;                   
                }
                stream.ReadWrite(ref hasTransactions);
                if (hasTransactions)
                {
                    stream.ReadWrite<List<TransactionData>, TransactionData>(ref this._transactions);
                }

                stream.Inner.Flush();
            }
            else {
                stream.ReadWrite(ref hasTransactions);
                if (hasTransactions)
                {
                    stream.ReadWrite<List<TransactionData>, TransactionData>(ref this._transactions);
                }                
            }
        }
        /// <summary>
        /// The index of the address.
        /// </summary>
        [JsonProperty(PropertyName = "index")]
        public int Index
        {
            get
            {
                return this._index;
            }
            set
            {
                this._index = value;
            }
        }

        /// <summary>
        /// The script pub key for this address.
        /// </summary>
        [JsonProperty(PropertyName = "scriptPubKey")]
        [JsonConverter(typeof(ScriptJsonConverter))]
        public Script ScriptPubKey
        {
            get
            {
                return this._scriptPubKey;
            }
            set
            {
                this._scriptPubKey = value;
            }
        }

        /// <summary>
        /// The script pub key for this address.
        /// </summary>
        [JsonProperty(PropertyName = "pubkey")]
        [JsonConverter(typeof(ScriptJsonConverter))]
        public Script Pubkey
        {
            get
            {
                return this._pubkey;
            }
            set
            {
                this._pubkey = value;
            }
        }

        /// <summary>
        /// The Base58 representation of this address.
        /// </summary>
        [JsonProperty(PropertyName = "address")]
        public string Address
        {
            get
            {
                if (this._address != null)
                {
                    return System.Text.Encoding.UTF8.GetString(this._address);
                }
                return null;
            }
            set
            {
                if (value != null)
                {
                    this._address = System.Text.Encoding.UTF8.GetBytes(value);
                }
                else
                {
                    this._address = Array.Empty<byte>();
                }
            }
        }

        /// <summary>
        /// A path to the address as defined in BIP44.
        /// </summary>
        [JsonProperty(PropertyName = "hdPath")]
        public string HdPath
        {
            get
            {
                if (this._hdPath != null)
                {
                    return System.Text.Encoding.UTF8.GetString(this._hdPath);
                }
                return null;
            }
            set
            {
                if (value != null)
                {
                    this._hdPath = System.Text.Encoding.UTF8.GetBytes(value);
                }
                else
                {
                    this._hdPath = Array.Empty<byte>();
                }
            }
        }

        /// <summary>
        /// A list of transactions involving this address.
        /// </summary>
        [JsonProperty(PropertyName = "transactions")]
        public List<TransactionData> Transactions
        {
            get
            {
                return this._transactions;
            }
            set
            {
                this._transactions = value;
            }
        }
        [JsonIgnore]
        public long WalletId
        {
            get
            {
                return this._walletid;
            }
            internal set
            {
                this._walletid = value;
            }
        }
        [JsonIgnore]
        public long Id
        {
            get
            {
                return this._id;
            }
            internal set
            {
                this._id = value;
            }
        }

        /// <summary>
        /// Determines whether this is a change address or a receive address.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if it is a change address; otherwise, <c>false</c>.
        /// </returns>
        public bool IsChangeAddress()
        {
            return HdOperations.IsChangeAddress(this.HdPath);
        }

        /// <summary>
        /// List all spendable transactions in an address.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<TransactionData> UnspentTransactions()
        {
            if (this.Transactions == null)
            {
                return new List<TransactionData>();
            }

            return this.Transactions.Where(t => t.IsSpendable());
        }

        /// <summary>
        /// Get the address total spendable value for both confirmed and unconfirmed UTXO.
        /// </summary>
        public (Money confirmedAmount, Money unConfirmedAmount) GetSpendableAmount()
        {
            List<TransactionData> allTransactions = this.Transactions.ToList();

            long confirmed = allTransactions.Sum(t => t.SpendableAmount(true));
            long total = allTransactions.Sum(t => t.SpendableAmount(false));

            return (confirmed, total - confirmed);
        }
    }

    /// <summary>
    /// An object containing transaction data.
    /// </summary>
    public class TransactionData : IBitcoinSerializable
    {
        private uint256 _id;
        private long _amount;
        private int _index;
        private int? _blockHeight;
        private uint256 _blockHash;
        private long _creationTime;
        private PartialMerkleTree _merkleProof;
        private Script _scriptPubKey;
        private byte[] _hex;
        private bool? _isPropagated;
        private bool _isPropagatedProxy;
        private SpendingDetails _spendingDetails;

        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this._id);
            stream.ReadWrite(ref this._amount);
            stream.ReadWrite(ref this._index);
            stream.ReadWrite(ref this._blockHash);
            stream.ReadWrite(ref this._creationTime);
            stream.ReadWrite(ref this._merkleProof);
            stream.ReadWrite(ref this._scriptPubKey);            
            stream.ReadWrite(ref this._isPropagatedProxy);
            
            bool hasSpending = false;
            bool hasHex = false;

            if (stream.Serializing)
            {
                if (this._spendingDetails != null && !this._spendingDetails.TransactionId.Equals(new uint256()))
                {
                    hasSpending = true;
                    stream.ReadWrite(ref hasSpending);
                    stream.ReadWrite<SpendingDetails>(ref this._spendingDetails);
                }
                else
                {
                    hasSpending = false;
                    stream.ReadWrite(ref hasSpending);
                }
                if (this._hex != null && this._hex.Length>0)
                {
                    hasHex = true;
                    stream.ReadWrite(ref hasHex);
                    stream.ReadWriteAsVarString(ref this._hex);
                }
                else
                {
                    hasHex = false;
                    stream.ReadWrite(ref hasHex);
                }

                if (this._blockHeight.HasValue)
                {
                    stream.ReadWrite((int)this._blockHeight.Value);
                }
                else {
                    stream.ReadWrite(0);
                }

                stream.Inner.Flush();
            }
            else
            {
                stream.ReadWrite(ref hasSpending);
                if (hasSpending) {
                    stream.ReadWrite<SpendingDetails>(ref this._spendingDetails);
                }

                stream.ReadWrite(ref hasHex);
                if (hasHex)
                {
                    stream.ReadWriteAsVarString(ref this._hex);
                }

                int tempValue = 0;
                stream.ReadWrite(ref tempValue);
                if (tempValue > 0)
                {
                    this._blockHeight = tempValue;
                }
            }
        }

        /// <summary>
        /// Transaction id.
        /// </summary>
        [JsonProperty(PropertyName = "id")]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 Id
        {
            get
            {
                return this._id;
            }
            set
            {
                this._id = value;
            }
        }

        /// <summary>
        /// The transaction amount.
        /// </summary>
        [JsonProperty(PropertyName = "amount")]
        [JsonConverter(typeof(MoneyJsonConverter))]
        public Money Amount
        {
            get
            {
                return new Money(this._amount);
            }
            set
            {
                this._amount = value.Satoshi;
            }
        }


        /// <summary>
        /// The index of this scriptPubKey in the transaction it is contained.
        /// </summary>
        /// <remarks>
        /// This is effectively the index of the output, the position of the output in the parent transaction.
        /// </remarks>
        [JsonProperty(PropertyName = "index", NullValueHandling = NullValueHandling.Ignore)]
        public int Index
        {
            get
            {
                return this._index;
            }
            set
            {
                this._index = value;
            }
        }

        /// <summary>
        /// The height of the block including this transaction.
        /// </summary>
        [JsonProperty(PropertyName = "blockHeight", NullValueHandling = NullValueHandling.Ignore)]
        public int? BlockHeight
        {
            get
            {
                return this._blockHeight;
            }
            set
            {
                this._blockHeight = value;  
            }
        }

        /// <summary>
        /// The hash of the block including this transaction.
        /// </summary>
        [JsonProperty(PropertyName = "blockHash", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 BlockHash
        {
            get
            {
                return this._blockHash;
            }
            set
            {
                this._blockHash = value;
            }
        }

        /// <summary>
        /// Gets or sets the creation time.
        /// </summary>
        [JsonProperty(PropertyName = "creationTime")]
        [JsonConverter(typeof(DateTimeOffsetConverter))]
        public DateTimeOffset CreationTime
        {
            get
            {
                return Utils.UnixTimeToDateTime(this._creationTime);
            }
            set
            {
                this._creationTime = Utils.DateTimeToUnixTime(value);
            }
        }

        /// <summary>
        /// Gets or sets the Merkle proof for this transaction.
        /// </summary>
        [JsonProperty(PropertyName = "merkleProof", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(PartialMerkleTreeBitcoinSerializableJsonConverter))]
        public PartialMerkleTree MerkleProof
        {
            get
            {
                return this._merkleProof;
            }
            set
            {
                this._merkleProof = value;
            }
        }

        /// <summary>
        /// The script pub key for this address.
        /// </summary>
        [JsonProperty(PropertyName = "scriptPubKey")]
        [JsonConverter(typeof(ScriptJsonConverter))]
        public Script ScriptPubKey
        {
            get
            {
                return this._scriptPubKey;
            }
            set
            {
                this._scriptPubKey = value;
            }
        }

        /// <summary>
        /// Hexadecimal representation of this transaction.
        /// </summary>
        [JsonProperty(PropertyName = "hex", NullValueHandling = NullValueHandling.Ignore)]
        public string Hex
        {
            get
            {
                if (this._hex != null && this._hex.Length>0)
                {
                    return System.Text.Encoding.UTF8.GetString(this._hex);
                }
                return null;
            }
            set
            {
                if (value != null)
                {
                    this._hex = System.Text.Encoding.UTF8.GetBytes(value);
                }
                else
                {
                    this._hex = Array.Empty<byte>();
                }
            }
        }

        /// <summary>
        /// Propagation state of this transaction.
        /// </summary>
        /// <remarks>Assume it's <c>true</c> if the field is <c>null</c>.</remarks>
        [JsonProperty(PropertyName = "isPropagated", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsPropagated
        {
            get
            {
                return this._isPropagated;
            }
            set
            {
                this._isPropagated = value;
                if (value.HasValue)
                {
                    this._isPropagatedProxy = (bool)value.Value;
                }
                else
                {
                    this._isPropagatedProxy = true; // based solely on property comment above if null then assume true
                }
            }
        }

        /// <summary>
        /// Gets or sets the full transaction object.
        /// </summary>
        [JsonIgnore]
        public Transaction Transaction => this.Hex == null ? null : Transaction.Parse(this.Hex);

        /// <summary>
        /// The details of the transaction in which the output referenced in this transaction is spent.
        /// </summary>
        [JsonProperty(PropertyName = "spendingDetails", NullValueHandling = NullValueHandling.Ignore)]
        public SpendingDetails SpendingDetails
        {
            get
            {
                return this._spendingDetails;
            }
            set
            {
                this._spendingDetails = value;
            }
        }

        /// <summary>
        /// Determines whether this transaction is confirmed.
        /// </summary>
        public bool IsConfirmed()
        {
            return this.BlockHeight != null;
        }

        /// <summary>
        /// Indicates an output is spendable.
        /// </summary>
        public bool IsSpendable()
        {
            return this.SpendingDetails == null;
        }

        public Money SpendableAmount(bool confirmedOnly)
        {
            // This method only returns a UTXO that has no spending output.
            // If a spending output exists (even if its not confirmed) this will return as zero balance.
            if (this.IsSpendable())
            {
                // If the 'confirmedOnly' flag is set check that the UTXO is confirmed.
                if (confirmedOnly && !this.IsConfirmed())
                {
                    return Money.Zero;
                }

                return this.Amount;
            }

            return Money.Zero;
        }
    }

    /// <summary>
    /// An object representing a payment.
    /// </summary>
    public class PaymentDetails : IBitcoinSerializable
    {
        private Script _destinationScriptPubKey;
        private byte[] _destinationAddress = Array.Empty<byte>();
        private long _amount;

        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this._destinationScriptPubKey);
            stream.ReadWrite(ref this._amount);
            stream.ReadWriteAsVarString(ref this._destinationAddress);
            if (stream.Serializing)
            {
                stream.Inner.Flush();
            }
        }

        /// <summary>
        /// The script pub key of the destination address.
        /// </summary>
        [JsonProperty(PropertyName = "destinationScriptPubKey")]
        [JsonConverter(typeof(ScriptJsonConverter))]
        public Script DestinationScriptPubKey
        {
            get
            {
                return this._destinationScriptPubKey;
            }
            set
            {
                this._destinationScriptPubKey = value;
            }
        }

        /// <summary>
        /// The Base58 representation of the destination  address.
        /// </summary>
        [JsonProperty(PropertyName = "destinationAddress")]
        public string DestinationAddress
        {
            get
            {
                if (this._destinationAddress != null)
                {
                    return System.Text.Encoding.UTF8.GetString(this._destinationAddress);
                }
                return null;
            }
            set
            {
                if (value != null)
                {
                    this._destinationAddress = System.Text.Encoding.UTF8.GetBytes(value);
                }
                else
                {
                    this._destinationAddress = Array.Empty<byte>();
                }
            }
        }

        /// <summary>
        /// The transaction amount.
        /// </summary>
        [JsonProperty(PropertyName = "amount")]
        [JsonConverter(typeof(MoneyJsonConverter))]
        public Money Amount
        {
            get
            {
                return new Money(this._amount);
            }
            set
            {
                this._amount = value.Satoshi;
            }
        }


    }

    public class SpendingDetails : IBitcoinSerializable
    {
        private uint256 _transactionId;
        private List<PaymentDetails> _payments;
        private int? _blockHeight;        
        private uint _creationTime;
        private byte[] _hex = Array.Empty<byte>();

        public SpendingDetails()
        {
            this.Payments = new List<PaymentDetails>();
        }

        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this._transactionId);            
            stream.ReadWrite(ref this._creationTime);     
            stream.ReadWrite<List<PaymentDetails>, PaymentDetails>(ref this._payments);

            bool hasHex = false;
            bool hasHeight = false;
            if (stream.Serializing)
            {
               
                if (this._hex != null && this._hex.Length > 0)
                {
                    hasHex = true;
                    stream.ReadWrite(ref hasHex);
                    stream.ReadWriteAsVarString(ref this._hex);
                }
                else
                {
                    hasHex = false;
                    stream.ReadWrite(ref hasHex);
                }

                if (this._blockHeight.HasValue)
                {
                    stream.ReadWrite((int)this._blockHeight.Value);
                }
                else
                {
                    stream.ReadWrite(0);
                }


                stream.Inner.Flush();
            }
            else
            {
                stream.ReadWrite(ref hasHex);
                if (hasHex)
                {
                    stream.ReadWriteAsVarString(ref this._hex);
                }

                int tempValue = 0;
                stream.ReadWrite(ref tempValue);
                if (tempValue > 0)
                {
                    this._blockHeight = tempValue;
                }
            }
        }

        /// <summary>
        /// The id of the transaction in which the output referenced in this transaction is spent.
        /// </summary>
        [JsonProperty(PropertyName = "transactionId", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 TransactionId
        {
            get
            {
                return this._transactionId;
            }
            set
            {
                this._transactionId = value;
            }
        }

        /// <summary>
        /// A list of payments made out in this transaction.
        /// </summary>
        [JsonProperty(PropertyName = "payments", NullValueHandling = NullValueHandling.Ignore)]
        public List<PaymentDetails> Payments
        {
            get
            {
                return this._payments;
            }
            set
            {
                this._payments = value;
            }
        }

        /// <summary>
        /// The height of the block including this transaction.
        /// </summary>
        [JsonProperty(PropertyName = "blockHeight", NullValueHandling = NullValueHandling.Ignore)]
        public int? BlockHeight
        {
            get
            {
                return this._blockHeight;
            }
            set
            {
                this._blockHeight = value;
            }
        }

        /// <summary>
        /// Gets or sets the creation time.
        /// </summary>
        [JsonProperty(PropertyName = "creationTime")]
        [JsonConverter(typeof(DateTimeOffsetConverter))]
        public DateTimeOffset CreationTime
        {
            get
            {
                return Utils.UnixTimeToDateTime(this._creationTime);
            }
            set
            {
                this._creationTime = Utils.DateTimeToUnixTime(value);
            }
        }

        /// <summary>
        /// Hexadecimal representation of this spending transaction.
        /// </summary>
        [JsonProperty(PropertyName = "hex", NullValueHandling = NullValueHandling.Ignore)]
        public string Hex
        {
            get
            {
                if (this._hex != null)
                {
                    return System.Text.Encoding.UTF8.GetString(this._hex);
                }
                return null;
            }
            set
            {
                if (value != null)
                {
                    this._hex = System.Text.Encoding.UTF8.GetBytes(value);
                }
                else
                {
                    this._hex = Array.Empty<byte>();
                }
            }
        }

        /// <summary>
        /// Gets or sets the full transaction object.
        /// </summary>
        [JsonIgnore]
        public Transaction Transaction => this.Hex == null ? null : Transaction.Parse(this.Hex);

        /// <summary>
        /// Determines whether this transaction being spent is confirmed.
        /// </summary>
        public bool IsSpentConfirmed()
        {
            return this.BlockHeight != null;
        }

    }

    /// <summary>
    /// Represents an UTXO that keeps a reference to <see cref="HdAddress"/> and <see cref="HdAccount"/>.
    /// </summary>
    /// <remarks>
    /// This is useful when an UTXO needs access to its HD properties like the HD path when reconstructing a private key.
    /// </remarks>
    public class UnspentOutputReference : IBitcoinSerializable
    {
        private HdAccount _account;
        private HdAddress _address;
        private TransactionData _transaction;

        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this._account);
            stream.ReadWrite(ref this._address);
            stream.ReadWrite(ref this._transaction);
            if (stream.Serializing)
            {
                stream.Inner.Flush();
            }
        }
        /// <summary>
        /// The account associated with this UTXO
        /// </summary>
        public HdAccount Account
        {
            get
            {
                return this._account;
            }
            set
            {
                this._account = value;
            }
        }

        /// <summary>
        /// The address associated with this UTXO
        /// </summary>
        public HdAddress Address
        {
            get
            {
                return this._address;
            }
            set
            {
                this._address = value;
            }
        }

        /// <summary>
        /// The transaction representing the UTXO.
        /// </summary>
        public TransactionData Transaction
        {
            get
            {
                return this._transaction;
            }
            set
            {
                this._transaction = value;
            }
        }



        /// <summary>
        /// Convert the <see cref="TransactionData"/> to an <see cref="OutPoint"/>
        /// </summary>
        /// <returns>The corresponding <see cref="OutPoint"/>.</returns>
        public OutPoint ToOutPoint()
        {
            return new OutPoint(this.Transaction.Id, (uint)this.Transaction.Index);
        }
    }
}
