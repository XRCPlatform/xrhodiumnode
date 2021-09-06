using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Text;
using NBitcoin;
using Newtonsoft.Json;

namespace BRhodium.Bitcoin.Features.Wallet
{
    /// <summary>
    /// A multisig wallet as described by bip-45 
    /// </summary>
    [Serializable]
    public class WalletMultisig : Wallet, ISerializable
    {
        //private readonly MultisigScheme multisigScheme;
        public WalletMultisig()
        {

        }
        protected WalletMultisig(SerializationInfo info, StreamingContext context)
        {
            this.Name = info.GetString("name");
            this.EncryptedSeed = string.Empty;
            this.ChainCode = (byte[])info.GetValue("chainCode", typeof(byte[]));
            this.CreationTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(info.GetString("creationTime")));
            var blockLocator = (string[])info.GetValue("blockLocator", typeof(string[]));
            this.BlockLocator = blockLocator != null ? blockLocator.ToList().ConvertAll(a => new uint256(a)).ToList() : null;
            var nameNetwork = info.GetString("network");
            this.Network = Network.GetNetwork(nameNetwork.ToLowerInvariant());

            List<IAccountRoot> accountRoot = new List<IAccountRoot>();
            //foreach (var item in (ICollection<AccountRootMultisig>)info.GetValue("accountsRoot", typeof(ICollection<AccountRootMultisig>)))
            foreach (var item in (ICollection<AccountRootMultisig>)info.GetValue("accountsRoot", typeof(ICollection<AccountRootMultisig>)))
            {
                if (item is AccountRootMultisig)
                {
                    accountRoot.Add(item);
                }                
            };
            this.AccountsRoot = accountRoot;

            this.IsMultisig = info.GetBoolean("isMultisig");
            //this.multisigScheme = (MultisigScheme)info.GetValue("multisigScheme", typeof(MultisigScheme));
        }

        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("name", this.Name);
            info.AddValue("encryptedSeed", string.Empty);
            info.AddValue("chainCode", this.ChainCode);
            info.AddValue("creationTime", this.CreationTime.ToUnixTimeSeconds().ToString());
            info.AddValue("blockLocator", this.BlockLocator != null ? this.BlockLocator.ToList().ConvertAll(a => a.ToString()).ToArray() : null);
            info.AddValue("network", this.Network.Name);
            info.AddValue("accountsRoot", this.AccountsRoot);
            info.AddValue("isMultisig", this.IsMultisig);
           // info.AddValue("multisigScheme", this.multisigScheme);
        }

        public WalletMultisig(Network network)
        {
            //this.multisigScheme = multisigScheme;
            this.Network = network;
            this.IsMultisig = true;
            this.EncryptedSeed = string.Empty;
            this.AccountsRoot = new List<IAccountRoot>();
        }

        //[JsonProperty(PropertyName = "multisigScheme")]
        //public MultisigScheme MultisigScheme
        //{
        //    get
        //    {
        //        return this.multisigScheme;
        //    }
        //}

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
        public HdAccountMultisig AddNewAccount(MultisigScheme scheme, CoinType coinType, DateTimeOffset accountCreationTime)
        {
            return AddNewAccount(scheme, coinType, this.Network, accountCreationTime);
        }

        /// <summary>
        /// Creates an account as expected in bip-44 account structure.
        /// </summary>
        /// <param name="chainCode"></param>
        /// <param name="network"></param>
        /// <param name="accountCreationTime"></param>
        /// <returns></returns>
        public HdAccountMultisig AddNewAccount(MultisigScheme multisigScheme, CoinType coinType ,Network network, DateTimeOffset accountCreationTime)
        {
            // Get the current collection of accounts.
            var accounts = this.AccountsRoot.FirstOrDefault().Accounts;

            int newAccountIndex = 0;
            if (accounts.Any())
            {
                newAccountIndex = accounts.Max(a => a.Index) + 1;
            }

            string accountHdPath = $"m/45'/{(int)coinType}'/{newAccountIndex}'";

            var newAccount = new HdAccountMultisig(multisigScheme)
            {
                Index = newAccountIndex,
                ExternalAddresses = new List<HdAddress>(),
                InternalAddresses = new List<HdAddress>(),
                Name = $"account {newAccountIndex}",
                HdPath = accountHdPath,
                CreationTime = accountCreationTime
            };

            accounts.Add(newAccount);

            return newAccount;
        }      

    }

    /// <summary>
    /// Provides owerrden mothods for account creation in multisig wallet.
    /// </summary>
    [Serializable]
    public class AccountRootMultisig : AccountRoot, IAccountRoot, ISerializable
    {
        protected AccountRootMultisig(SerializationInfo info, StreamingContext context)
        {
            foreach (SerializationEntry entry in info)
            {
                switch (entry.Name)
                {
                    case "coinType":
                        this.CoinType = (CoinType)info.GetValue("coinType", typeof(CoinType));
                        break;
                    case "lastBlockSyncedHeight":
                        this.LastBlockSyncedHeight = (int?)info.GetValue("lastBlockSyncedHeight", typeof(int?));
                        break;
                    case "lastBlockSyncedHash":
                        var lastBlockSyncedHash = info.GetString("lastBlockSyncedHash");
                        this.LastBlockSyncedHash = lastBlockSyncedHash != null ? new uint256(lastBlockSyncedHash) : null;
                        break;
                    case "accounts":
                        List<IHdAccount> accounts = new List<IHdAccount>();
                        foreach (var item in (ICollection<HdAccountMultisig>) info.GetValue("accounts", typeof(ICollection<HdAccountMultisig>)))
                        {
                            accounts.Add(item);
                        };
                        this.Accounts = accounts;
                        break;
                }
            }
        }

        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("coinType", this.CoinType);
            if (this.LastBlockSyncedHeight != null) info.AddValue("lastBlockSyncedHeight", this.LastBlockSyncedHeight);
            if (this.LastBlockSyncedHash != null) info.AddValue("lastBlockSyncedHash", this.LastBlockSyncedHash != null ? this.LastBlockSyncedHash.ToString() : null);
            info.AddValue("accounts", this.Accounts);
        }

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        public AccountRootMultisig()
        {
            this.Accounts = new List<IHdAccount>();
        }
      
    }
    [Serializable]
    public class HdAccountMultisig : HdAccount, IHdAccount, ISerializable
    {
        
        public HdAccountMultisig(MultisigScheme scheme)
        {
            this.ExtendedPubKey = "N/A";
            this.MultisigScheme = scheme;
        }
        
        protected HdAccountMultisig(SerializationInfo info, StreamingContext context)
        {
            foreach (SerializationEntry entry in info)
            {
                switch (entry.Name)
                {
                    case "index":
                        this.Index = info.GetInt32("index");
                        break;
                    case "name":
                        this.Name = info.GetString("name");
                        break;
                    case "hdPath":
                        this.HdPath = info.GetString("hdPath");
                        break;
                    case "extPubKey":
                        this.ExtendedPubKey = info.GetString("extPubKey");
                        break;
                    case "creationTime":
                        this.CreationTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(info.GetString("creationTime")));
                        break;
                    case "externalAddresses":
                        this.ExternalAddresses = (ICollection<HdAddress>)info.GetValue("externalAddresses", typeof(ICollection<HdAddress>));
                        break;
                    case "internalAddresses":
                        this.InternalAddresses = (ICollection<HdAddress>)info.GetValue("internalAddresses", typeof(ICollection<HdAddress>));
                        break;
                    case "scheme":
                        this.MultisigScheme = (MultisigScheme)info.GetValue("scheme", typeof(MultisigScheme));
                        break;
                }
            }
        }

        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("index", this.Index);
            info.AddValue("name", this.Name);
            info.AddValue("hdPath", this.HdPath);
            info.AddValue("extPubKey", this.ExtendedPubKey);
            info.AddValue("creationTime", this.CreationTime.ToUnixTimeSeconds().ToString());
            info.AddValue("externalAddresses", this.ExternalAddresses);
            info.AddValue("internalAddresses", this.InternalAddresses);
            info.AddValue("scheme", this.MultisigScheme);
        }

        [JsonProperty(PropertyName = "multisigScheme")]
        public MultisigScheme MultisigScheme { get; set; }
        /// Generates an HD public key derived from an extended public key.
        /// </summary>
        /// <param name="accountExtPubKey">The extended public key used to generate child keys.</param>
        /// <param name="index">The index of the child key to generate.</param>
        /// <param name="isChange">A value indicating whether the public key to generate corresponds to a change address.</param>
        /// <returns>
        /// An HD public key derived from an extended public key.
        /// </returns>

        public Script GeneratePublicKey(int hdPathIndex, bool isChange = false)
        {
            List<PubKey> derivedPubKeys = new List<PubKey>();
            foreach (var xpub in this.MultisigScheme.XPubs)
            {
                derivedPubKeys.Add(HdOperations.GeneratePublicKey(xpub, hdPathIndex, isChange));
            }
            var sortedkeys = LexographicalSort(derivedPubKeys);

            Script redeemScript = PayToMultiSigTemplate.Instance.GenerateScriptPubKey(this.MultisigScheme.Threashold, sortedkeys.ToArray());
            return redeemScript;
        }

        /// <summary>
        /// Creates a number of additional addresses in the current account.
        /// </summary>
        /// <remarks>
        /// The name given to the account is of the form "account (i)" by default, where (i) is an incremental index starting at 0.
        /// According to BIP44, an account at index (i) can only be created when the account at index (i - 1) contains at least one transaction.
        /// </remarks>
        /// <param name="wallet">Instance of a multisig wallet that allows access to multisig scheme, pubkeys threashold and etc</param>
        /// <param name="addressesQuantity">The number of addresses to create.</param>
        /// <param name="isChange">Whether the addresses added are change (internal) addresses or receiving (external) addresses.</param>
        /// <returns>The created addresses.</returns>
        public override IEnumerable<HdAddress> CreateAddresses(Network network, int addressesQuantity, bool isChange = false)
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
                var pubkey = GeneratePublicKey(i, isChange);
                BitcoinAddress address = pubkey.Hash.GetAddress(network);
                // Add the new address details to the list of addresses.
                HdAddress newAddress = new HdAddress
                {
                    Index = i,
                    HdPath = CreateHdPath((int)this.GetCoinType(), this.Index, i, isChange),
                    ScriptPubKey = address.ScriptPubKey,
                    Pubkey = pubkey,
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

        public static string CreateHdPath(int coinType, int accountIndex, int addressIndex, bool isChange = false)
        {
            int change = isChange ? 1 : 0;
            return $"m/45'/{coinType}'/{accountIndex}'/{change}/{addressIndex}";
        }

        private static IEnumerable<PubKey> LexographicalSort(IEnumerable<PubKey> pubKeys)
        {
            List<PubKey> list = new List<PubKey>();
            var e = pubKeys.Select(s => s.ToHex());
            var sorted = e.OrderByDescending(s => s.Length).ThenBy(r => r);
            foreach (var item in sorted)
            {
                list.Add(new PubKey(item));
            }
            return list;
        }
    }
}
