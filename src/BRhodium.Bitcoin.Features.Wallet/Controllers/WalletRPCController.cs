using System.Linq.Expressions;
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
using TransactionVerboseModel = BRhodium.Bitcoin.Features.Wallet.Models.TransactionVerboseModel;
using System.Security;
using BRhodium.Node.Base;
using NBitcoin.DataEncoders;
using BRhodium.Bitcoin.Features.Consensus;

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
        private readonly ILogger logger;
        private readonly ILoggerFactory loggerFactory;
        private readonly IBlockRepository blockRepository;
        private readonly IWalletSyncManager walletSyncManager;
        private readonly ConcurrentChain chain;
        private BlockStoreCache blockStoreCache { get; set; }

        private bool useDeprecatedWalletRPC;

        private IWalletManager walletManager { get; set; }
        private IConsensusLoop ConsensusLoop { get; set; }
        private IBroadcasterManager broadcasterManager { get; set; }
        private IWalletFeePolicy walletFeePolicy { get; set; }
        private IWalletKeyPool walletKeyPool { get; set; }

        /// <summary>wallet address mapping on the node</summary>
        public static ConcurrentDictionary<string, string> WalletsByAddressMap = new ConcurrentDictionary<string, string>();

        /// <summary>Hd addresses to address list</summary>
        public static ConcurrentDictionary<string, HdAddress> HdAddressByAddressMap = new ConcurrentDictionary<string, HdAddress>();
        private static bool inRescan = false;

        public WalletRPCController(
            IServiceProvider serviceProvider,
            IWalletManager walletManager,
            ILoggerFactory loggerFactory,
            IFullNode fullNode,
            NodeSettings nodeSettings,
            Network network,
            ConcurrentChain chain,
            IConnectionManager connectionManager,
            IWalletFeePolicy walletFeePolicy,
            IWalletKeyPool walletKeyPool,
            IBlockRepository blockRepository,
            IBroadcasterManager broadcasterManager,
            IWalletSyncManager walletSyncManager,
            IChainState chainState = null,
            IConsensusLoop consensusLoop = null) : base(fullNode, nodeSettings, network, chain, chainState, connectionManager)
        {
            this.walletManager = walletManager;
            this.serviceProvider = serviceProvider;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.broadcasterManager = broadcasterManager;
            this.walletSyncManager = walletSyncManager;
            this.ConsensusLoop = consensusLoop;
            this.walletFeePolicy = walletFeePolicy;
            this.walletKeyPool = walletKeyPool;
            this.chain = chain;
            this.loggerFactory = loggerFactory;
            this.blockRepository = blockRepository;
            this.blockStoreCache = new BlockStoreCache(this.blockRepository, DateTimeProvider.Default, this.loggerFactory, this.Settings);
            this.useDeprecatedWalletRPC = this.walletManager.WalletSettings.UseDeprecatedWalletRPC;
        }

        /// <summary>
        /// Wallets the password with unicode support.
        ///
        /// <p>Example: <br/>
        /// Set the password for 2 minutes to perform a transaction<br/>
        /// walletpassword "my pass phrase" 120</p>
        /// <p>Perform a send(requires password set)<br/>
        /// sendtoaddress "1M72Sfpbz1BPpXFHz9m3CdqATR44Jvaydd" 1.0</p>
        /// <p>Clear the password since we are done before 2 minutes is up<br/>
        /// walletlock </p>
        /// </summary>
        /// <param name="walletName">The wallet name.</param>
        /// <param name="passwordBase64">The password in Base64.</param>
        /// <param name="timeout">The timeout in seconds. Limited to 1073741824 sec.</param>
        /// <returns>(bool) True or fail</returns>
        [ActionName("walletpasswordbase64")]
        [ActionDescription("Wallets the password.")]
        public IActionResult WalletpasswordBase64(string walletName, string passwordBase64, int timeout)
        {
            var password = Encoding.UTF8.GetString(Convert.FromBase64String(passwordBase64));
            return Walletpassword(walletName, password, timeout);
        }

        /// <summary>
        /// Wallets the password.
        ///
        /// <p>Example: <br/>
        /// Set the password for 2 minutes to perform a transaction<br/>
        /// walletpassword "my pass phrase" 120</p>
        /// <p>Perform a send(requires password set)<br/>
        /// sendtoaddress "1M72Sfpbz1BPpXFHz9m3CdqATR44Jvaydd" 1.0</p>
        /// <p>Clear the password since we are done before 2 minutes is up<br/>
        /// walletlock </p>
        /// </summary>
        /// <param name="walletName">The wallet name.</param>
        /// <param name="password">The password.</param>
        /// <param name="timeout">The timeout in seconds. Limited to 1073741824 sec.</param>
        /// <returns>(bool) True or fail</returns>
        [ActionName("walletpassword")]
        [ActionDescription("Wallets the password.")]
        public IActionResult Walletpassword(string walletName, string password, int timeout)
        {
            try
            {
                Guard.NotEmpty(walletName, nameof(walletName));
                Guard.NotEmpty(password, nameof(password));
                if ((timeout <= 0) || (timeout > 1073741824))
                {
                    throw new ArgumentNullException("timeout");
                }

                var dateExpiration = DateTime.Now.AddSeconds(timeout);
                this.walletManager.WalletSecrets.UnlockWallet(walletName, password, dateExpiration);
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
        /// Set the password for 2 minutes to perform a transaction<br/>
        /// walletpassword "my pass phrase" 120</p>
        /// <p>Perform a send(requires password set)<br/>
        /// sendtoaddress "1M72Sfpbz1BPpXFHz9m3CdqATR44Jvaydd" 1.0</p>
        /// <p>Clear the password since we are done before 2 minutes is up<br/>
        /// walletlock </p>
        /// </summary>
        /// <param name="address">The address.</param>
        /// <returns>(string) Private key.</returns>
        [ActionName("dumpprivkey")]
        [ActionDescription("Dumps the priv key.")]
        public IActionResult DumpPrivKey(string address)
        {
            try
            {
                if (string.IsNullOrEmpty(address))
                {
                    throw new ArgumentNullException("address");
                }
                HdAddress hdAddress = null;
                //we need to find wallet
                string walletCombix = WalletsByAddressMap.TryGet<string, string>(address);
                if (walletCombix == null)
                {
                    bool isFound = false;

                    foreach (var currWalletName in this.walletManager.GetWalletsNames())
                    {
                        foreach (var currAccount in this.walletManager.GetAccounts(currWalletName))
                        {
                            foreach (var walletAddress in currAccount.GetCombinedAddresses())
                            {
                                if (walletAddress.Address.ToString().Equals(address))
                                {
                                    isFound = true;
                                    walletCombix = $"{currAccount.Name}/{currWalletName}";
                                    WalletsByAddressMap.TryAdd<string, string>(address, walletCombix);
                                    HdAddressByAddressMap.TryAdd<string, HdAddress>(address, walletAddress);
                                    hdAddress = walletAddress;
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
                    throw new RPCException(RPCErrorCode.RPC_INVALID_ADDRESS_OR_KEY, "Wallet not initialized", null, false);
                }
                string walletName = walletCombix.Split('/')[1];
                var mywallet = this.walletManager.GetWallet(walletName);

                //if wallet combix was cached
                if (HdAddressByAddressMap.ContainsKey(address) && hdAddress == null)
                {
                   HdAddressByAddressMap.TryGetValue(address, out hdAddress);
                }

                var password = this.walletManager.WalletSecrets.GetWalletPassword(mywallet.Name);

                if (hdAddress == null)
                {
                    throw new RPCException(RPCErrorCode.RPC_INVALID_ADDRESS_OR_KEY, "Wallet not initialized", null, false);
                }
                try
                {
                    if (password != null && hdAddress != null)
                    {
                        var pk = mywallet.GetExtendedPrivateKeyForAddress(password, hdAddress).PrivateKey.GetWif(this.Network);
                        string privatekey = pk.ToString();
                        return this.Json(ResultHelper.BuildResultResponse(privatekey));
                    }                   
                }
                catch (Exception)
                {

                }

                throw new ArgumentNullException("password");
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
        /// Set the password for 2 minutes to perform a transaction<br/>
        /// walletpassword "my pass phrase" 120</p>
        /// <p>Perform a send(requires password set)<br/>
        /// sendtoaddress "1M72Sfpbz1BPpXFHz9m3CdqATR44Jvaydd" 1.0</p>
        /// <p>Clear the password since we are done before 2 minutes is up<br/>
        /// walletlock </p>
        /// </summary>
        /// <param name="walletName">The wallet name.</param>
        /// <returns>(bool) True or fail.</returns>
        [ActionName("walletlock")]
        [ActionDescription("Lock the wallets.")]
        public IActionResult WalletLock(string walletName = null)
        {
            try
            {
                this.walletManager.WalletSecrets.LockWallet(walletName);
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets account balance.
        ///
        /// <p>If [walletName] is not specified, returns the server's total available balance.<br/>
        /// If [walletName] is specified, returns the balance in the account.<br/>
        /// If [walletName] is "*", get the balance of all accounts.</p>
        /// </summary>
        /// <param name="walletName">The wallet name.</param>
        /// <returns>(string) The balance of the account or the total wallet in XRC.</returns>
        [ActionName("getbalance")]
        [ActionDescription("Gets account balance.")]
        public IActionResult GetBalance(string walletName)
        {
            try
            {
                WalletBalanceModel model = new WalletBalanceModel();

                var accounts = new List<HdAccount>();
                if (walletName == "*")
                {
                    foreach (var name in this.walletManager.Wallets.Keys)
                    {
                        foreach (var account in this.walletManager.GetAccounts(name))
                        {
                            accounts.Add(account);
                        }

                    }
                }
                else
                {
                    accounts = this.walletManager.GetAccounts(walletName).ToList();
                }

                var totalBalance = new Money(0);

                foreach (var account in accounts)
                {
                    var result = account.GetSpendableAmount(this.chain);

                    List<Money> balances = new List<Money>();
                    balances.Add(totalBalance);
                    balances.Add(result.ConfirmedAmount);
                    balances.Add(result.UnConfirmedAmount);
                    totalBalance = MoneyExtensions.Sum(balances);
                }

                var balance = totalBalance.ToUnit(MoneyUnit.XRC);

                return this.Json(ResultHelper.BuildResultResponse(balance));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Returns information about a bitcoin address.
        /// </summary>
        /// <param name="address">bech32 or base58 BitcoinAddress to validate.</param>
        /// <returns>(ValidatedAddress) Object with address information containing a boolean indicating address validity.</returns>
        [ActionName("validateaddress")]
        [ActionDescription("Returns information about a bitcoin address.")]
        public IActionResult ValidateAddress(string address)
        {
            try
            {
                if (string.IsNullOrEmpty(address))
                    throw new ArgumentNullException("address");

                var res = new ValidatedAddress();
                res.IsValid = false;
                res.Address = address;
                res.IsWatchOnly = false;
                res.IsScript = false;

                try
                {
                    // P2PKH
                    if (BitcoinPubKeyAddress.IsValid(address, ref this.Network))
                    {
                        res.IsValid = true;
                        res.ScriptPubKey = new BitcoinPubKeyAddress(address, this.Network).ScriptPubKey.ToHex();
                    }
                    // P2SH
                    else if (BitcoinScriptAddress.IsValid(address, ref this.Network))
                    {
                        res.IsValid = true;
                        res.IsScript = true;
                        res.ScriptPubKey = new BitcoinScriptAddress(address, this.Network).ScriptPubKey.ToHex();
                    }
                } catch (FormatException exc)
                {
                    res.IsValid = false;
                }

                string walletCombix = WalletsByAddressMap.TryGet<string, string>(address);
                if (walletCombix != null)
                {
                    res.IsMine = true;
                }

                if (!res.IsMine)
                {
                    foreach (var currWalletName in this.walletManager.GetWalletsNames())
                    {
                        foreach (var currAccount in this.walletManager.GetAccounts(currWalletName))
                        {
                            foreach (var walletAddress in currAccount.GetCombinedAddresses())
                            {
                                if (walletAddress.Address.ToString().Equals(address))
                                {
                                    walletCombix = $"{currAccount.Name}/{currWalletName}";
                                    WalletsByAddressMap.TryAdd<string, string>(address, walletCombix);
                                    HdAddressByAddressMap.TryAdd<string, HdAddress>(address, walletAddress);
                                    res.IsMine = true;
                                    break;
                                }
                            }

                            if (res.IsMine) break;
                        }

                        if (res.IsMine) break;
                    }
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
        /// Gets the account as an wallet combix.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <returns>(string) Return wallet combix as string.</returns>
        [ActionName("getaccount")]
        [ActionDescription("Gets the account as an wallet combix.")]
        public IActionResult GetAccount(string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                throw new ArgumentNullException("address");
            }

            string walletCombix = WalletsByAddressMap.TryGet<string, string>(address);
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
                            WalletsByAddressMap.TryAdd<string, string>(address, walletCombix);
                            HdAddressByAddressMap.TryAdd<string, HdAddress>(address, walletAddress);
                            return this.Json(ResultHelper.BuildResultResponse(walletCombix));
                        }
                    }
                }
            }

            //if this point is reached the address is not in any wallets
            throw new RPCException(RPCErrorCode.RPC_INVALID_ADDRESS_OR_KEY, "Wallet not initialized", null, false);
        }

        /// <summary>
        /// The getaccountaddress RPC is a DEPRECATED RPC. It is intended to create an "account"
        /// and an address in that account if one doesn't exist. It will return the next available
        /// address without a balance.
        ///
        /// Since the original description of this RPC does not take into account HD paths, for this
        /// API, it will just default to the 0th account in a wallet.
        ///
        /// This is being supported only because certain software still expects old-style accounts.
        ///
        /// This will NOT generate new wallets or accounts. These need to be done SEPARATELY.
        /// </summary>
        /// <param name="account"></param>
        /// <returns></returns>

        [ActionName("getaccountaddress")]
        [ActionDescription("DEPRECATED. Do not use for new applications.")]
        public IActionResult GetAccountAddress(string account)
        {
            try
            {
                var hdAccount = WalletRPCUtil.GetAccountFromWalletForDeprecatedRpcs((WalletManager)this.walletManager, this.Network, account);
                return this.Json(
                    ResultHelper.BuildResultResponse(hdAccount.GetFirstUnusedReceivingAddress().Address)
                );
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// The getaddressesbyaccount RPC is a DEPRECATED RPC. This will list ONLY the addresses
        /// in the wallet specified under the first derivation path and only the receiving addresses since
        /// there is no expectation around this RPC to delineate what each address is.
        /// </summary>
        /// <param name="account"></param>
        /// <returns></returns>
        [ActionName("getaddressesbyaccount")]
        [ActionDescription("DEPRECATED. Do not use for new applications.")]
        public IActionResult GetAddressesByAccount(string account)
        {
            try
            {
                var hdAccount = WalletRPCUtil.GetAccountFromWalletForDeprecatedRpcs(
                    (WalletManager) walletManager,
                    this.Network,
                    account
                );
                return this.Json(
                    ResultHelper.BuildResultResponse(
                        hdAccount.ExternalAddresses
                        .Select(address => address.Address)
                        .ToList()
                    )
                );
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }


        /// <summary>
        /// Generates the new wallet.
        /// </summary>
        /// <param name="walletName">Name of the wallet.</param>
        /// <param name="passwordBase64">The password in BASE64 format.</param>
        /// <returns>(string) Return Mnemonic BIP39 format.</returns>
        [ActionName("generatenewwalletbase64")]
        [ActionDescription("Generates the new wallet.")]
        public Mnemonic GenerateNewWalletBase64(string walletName, string passwordBase64)
        {
            var password = Encoding.UTF8.GetString(Convert.FromBase64String(passwordBase64));
            return GenerateNewWallet(walletName, password);
        }

        /// <summary>
        /// Generates the new wallet.
        /// </summary>
        /// <param name="walletName">Name of the wallet.</param>
        /// <param name="password">The password.</param>
        /// <returns>(string) Return Mnemonic BIP39 format.</returns>
        [ActionName("generatenewwallet")]
        [ActionDescription("Generates the new wallet.")]
        public Mnemonic GenerateNewWallet(string walletName, string password)
        {
            if (string.IsNullOrEmpty(walletName))
            {
                throw new ArgumentNullException("walletName");
            }
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentNullException("password");
            }

            var w = this.walletManager as WalletManager;
            return w.CreateWallet(password, walletName);
        }

        /// <summary>
        /// Gets the wallet.
        /// </summary>
        /// <param name="walletName">Name of the wallet.</param>
        /// <returns>(HdAccount) Object with information.</returns>
        [ActionName("getwallet")]
        [ActionDescription("Gets the wallet.")]
        public HdAccount GetWallet(string walletName)
        {
            if (string.IsNullOrEmpty(walletName))
            {
                throw new ArgumentNullException("walletName");
            }

            var w = this.walletManager;
            var wallet = w.GetWalletByName(walletName);
            return wallet.GetAccountsByCoinType((CoinType)this.Network.Consensus.CoinType).ToArray().First();
        }

        /// <summary>
        /// Send to many addresses from a specified hd acccount name.
        ///
        /// The actual function to process this can take the hdAccountName in different ways:
        /// "" = default account and default hd derivation path
        /// "name" = uses the wallet name and the default hd derivation path
        /// "account 1/name" = custom wallet name and hd derivation path
        /// </summary>
        /// <param name="hdAcccountName">Name of the hd acccount.</param>
        /// <param name="toBitcoinAddresses">(string) To bitcoin addresses.</param>
        /// <param name="minconf">(int) The minconf.</param>
        /// <param name="password">(string) The password.</param>
        /// <returns>(uint256) Transaction hash.</returns>
        [ActionName("sendmany")]
        [ActionDescription("Send manies the specified hd acccount name.")]
        public uint256 Sendmany(string hdAcccountName, string toBitcoinAddresses, int minconf, string password)
        {
            return SendmanyProcessing(hdAcccountName, toBitcoinAddresses, minconf, password, FeeType.Low);
        }

        /// <summary>
        /// Send manies the specified hd acccount name.
        /// </summary>
        /// <param name="hdAcccountName">Name of the hd acccount.</param>
        /// <param name="toBitcoinAddresses">(string) To bitcoin addresses.</param>
        /// <param name="minconf">(int) The minconf.</param>
        /// <param name="passwordBase64">(string) The password in Base64.</param>
        /// <returns>(uint256) Transaction hash.</returns>
        [ActionName("sendmanybase64")]
        [ActionDescription("Send manies the specified hd acccount name.")]
        public uint256 SendmanyBase64(string hdAcccountName, string toBitcoinAddresses, int minconf, string passwordBase64)
        {
            var password = Encoding.UTF8.GetString(Convert.FromBase64String(passwordBase64));
            return SendmanyProcessing(hdAcccountName, toBitcoinAddresses, minconf, password, FeeType.Low);
        }

        /// <summary>
        /// Send manies the specified hd acccount name.
        /// </summary>
        /// <param name="hdAcccountName">Name of the hd acccount.</param>
        /// <param name="toBitcoinAddresses">(string) To bitcoin addresses.</param>
        /// <param name="minconf">(int) The minconf.</param>
        /// <param name="password">(string) The password.</param>
        /// <param name="feeType">Fee type VERYLOW, LOW, MEDIUM, HIGH, VERYHIGH.</param>
        /// <returns>(uint256) Transaction hash.</returns>
        [ActionName("sendmanyfee")]
        [ActionDescription("Send manies the specified hd acccount name.")]
        public uint256 SendmanyFee(string hdAcccountName, string toBitcoinAddresses, int minconf, string password, string feeType)
        {
            var convFeeType = FeeType.Low;
            Enum.TryParse<FeeType>(feeType, true, out convFeeType);
            return SendmanyProcessing(hdAcccountName, toBitcoinAddresses, minconf, password, convFeeType);
        }

        /// <summary>
        /// Send manies the specified hd acccount name.
        /// </summary>
        /// <param name="hdAcccountName">Name of the hd acccount.</param>
        /// <param name="toBitcoinAddresses">(string) To bitcoin addresses.</param>
        /// <param name="minconf">(int) The minconf.</param>
        /// <param name="passwordBase64">(string) The password in Base64.</param>
        /// <param name="feeType">Fee type VERYLOW, LOW, MEDIUM, HIGH, VERYHIGH.</param>
        /// <returns>(uint256) Transaction hash.</returns>
        [ActionName("sendmanyfeebase64")]
        [ActionDescription("Send manies the specified hd acccount name.")]
        public uint256 SendmanyFeeBase64(string hdAcccountName, string toBitcoinAddresses, int minconf, string passwordBase64, string feeType)
        {
            var convFeeType = FeeType.Low;
            Enum.TryParse(feeType, true, out convFeeType);
            var password = Encoding.UTF8.GetString(Convert.FromBase64String(passwordBase64));
            return SendmanyProcessing(hdAcccountName, toBitcoinAddresses, minconf, password, convFeeType);
        }

        /// <summary>
        /// Private function to process SendMany function.
        /// </summary>
        private uint256 SendmanyProcessing(string hdAcccountName, string toBitcoinAddresses, int minconf, string password, FeeType feeType)
        {
            string accountName = "";
            string walletName = "";
            if (string.IsNullOrEmpty(hdAcccountName))
            {
                hdAcccountName = WalletRPCUtil.DEFAULT_WALLET + "/" + DEFAULT_ACCOUNT_NAME;
            }

            Guard.NotEmpty(toBitcoinAddresses, nameof(toBitcoinAddresses));

            if (hdAcccountName.Contains("/"))
            {
                var nameParts = hdAcccountName.Split('/');
                walletName = nameParts[0];
                accountName = nameParts[1];
            }
            else
            {
                walletName = hdAcccountName;
                accountName = DEFAULT_ACCOUNT_NAME;
            }

            if (this.useDeprecatedWalletRPC)
            {
                password = this.walletManager.WalletSecrets.GetWalletPassword(WalletRPCUtil.DEFAULT_WALLET);
            }

            var transaction = this.FullNode.NodeService<IWalletTransactionHandler>() as WalletTransactionHandler;
            var w = this.walletManager as WalletManager;
            var walletReference = new WalletAccountReference(walletName, accountName);
            Dictionary<string, decimal> toBitcoinAddress = JsonConvert.DeserializeObject<Dictionary<string, decimal>>(toBitcoinAddresses);

            List<Recipient> recipients = new List<Recipient>();
            foreach (var item in toBitcoinAddress)
            {
                recipients.Add(new Recipient
                {
                    Amount = new Money(item.Value, MoneyUnit.XRC),
                    ScriptPubKey = BitcoinAddress.Create(item.Key, this.Network).ScriptPubKey
                });
            };

            var context = new TransactionBuildContext(walletReference, recipients, password)
            {
                MinConfirmations = minconf,
                FeeType = feeType,
                Sign = true
            };

            var controller = this.FullNode.NodeService<WalletController>();

            var fundTransaction = transaction.BuildTransaction(context);
            var response = controller.SendTransaction(new SendTransactionRequest(fundTransaction.ToHex()));
            if (response.GetType() == typeof(ErrorResult)) return null;

            return fundTransaction.GetHash();
        }

        /// <summary>
        /// Sends some amount to specified address.
        ///
        /// Bitcoin RPC only uses the default account, so if -usedeprecatedwalletRPC
        /// is turned on, it will change this interface to support that.
        ///
        /// Parameter list only applies to non-deprecated usage.
        // /// </summary>
        /// <param name="param1">The wallet name.</param>
        /// <param name="param2">Password for your wallet.</param>
        /// <param name="param3">Target address.</param>
        /// <param name="param4">The amount in XRC.</param>
        /// <returns>(string) The transaction id.</returns>
        /// <returns>(string) The transaction id.</returns>
        [ActionName("sendtoaddress")]
        [ActionDescription("Sends some amount to specified address.")]
        public IActionResult SendToAddress(string param1, string param2, string param3, string param4 = null)
        {
            if (this.useDeprecatedWalletRPC)
            {
                var address = param1;
                var amount = Decimal.Parse(param2);
                var password = this.walletManager.WalletSecrets.GetWalletPassword(WalletRPCUtil.DEFAULT_WALLET);

                Guard.NotEmpty(password, nameof(password));
                Guard.NotEmpty(address, nameof(address));
                Guard.NotEmpty(amount.ToString(), nameof(amount));
                return SendToAddressResponse(WalletRPCUtil.DEFAULT_WALLET, password, address, amount, FeeType.Low);
            }
            else
            {
                var walletName = param1;
                var password = param2;
                var address = param3;
                var amount = Decimal.Parse(param4);

                Guard.NotEmpty(walletName, nameof(walletName));
                Guard.NotEmpty(address, nameof(address));
                Guard.NotEmpty(password, nameof(password));
                Guard.NotEmpty(amount.ToString(), nameof(amount));
                return SendToAddressResponse(walletName, password, address, amount, FeeType.Low);
            }
        }

        /// <summary>
        /// Sends some amount to specified address.
        /// </summary>
        /// <param name="walletName">The wallet name.</param>
        /// <param name="passwordBase64">Password base64 for your wallet.</param>
        /// <param name="address">Target address.</param>
        /// <param name="amount">The amount in XRC.</param>
        /// <returns>(string) The transaction id.</returns>
        [ActionName("sendtoaddressbase64")]
        [ActionDescription("Sends some amount to specified address.")]
        public IActionResult SendToAddressBase64(string walletName, string passwordBase64, string address, decimal amount)
        {
            var password = Encoding.UTF8.GetString(Convert.FromBase64String(passwordBase64));
            return SendToAddressResponse(walletName, password, address, amount, FeeType.Low);
        }

        /// <summary>
        /// Sends some amount to specified address.
        /// </summary>
        /// <param name="walletName">The wallet name.</param>
        /// <param name="password">Password for your wallet.</param>
        /// <param name="address">Target address.</param>
        /// <param name="amount">The amount in XRC.</param>
        /// <param name="feeType">Fee type VERYLOW, LOW, MEDIUM, HIGH, VERYHIGH.</param>
        /// <returns>(string) The transaction id.</returns>
        [ActionName("sendtoaddressfee")]
        [ActionDescription("Sends some amount to specified address.")]
        public IActionResult SendToAddressFee(string walletName, string password, string address, decimal amount, string feeType)
        {
            var convFeeType = FeeType.Low;
            Enum.TryParse(feeType, true, out convFeeType);
            return SendToAddressResponse(walletName, password, address, amount, convFeeType);
        }

        /// <summary>
        /// Sends some amount to specified address.
        /// </summary>
        /// <param name="walletName">The wallet name.</param>
        /// <param name="passwordBase64">Password base64 for your wallet.</param>
        /// <param name="address">Target address.</param>
        /// <param name="amount">The amount in XRC.</param>
        /// <param name="feeType">Fee type VERYLOW, LOW, MEDIUM, HIGH, VERYHIGH.</param>
        /// <returns>(string) The transaction id.</returns>
        [ActionName("sendtoaddressfeebase64")]
        [ActionDescription("Sends some amount to specified address.")]
        public IActionResult SendToAddressFeeBase64(string walletName, string passwordBase64, string address, decimal amount, string feeType)
        {
            var convFeeType = FeeType.Low;
            Enum.TryParse(feeType, true, out convFeeType);
            var password = Encoding.UTF8.GetString(Convert.FromBase64String(passwordBase64));
            return SendToAddressResponse(walletName, password, address, amount, convFeeType);
        }

        /// <summary>
        /// Private function with response for SendToAddress function.
        /// </summary>
        private IActionResult SendToAddressResponse(string walletName, string password, string address, decimal amount, FeeType feeType)
        {
            try
            {

                if (amount <= 0)
                {
                    throw new ArgumentNullException("amount");
                }

                var mywallet = this.walletManager.GetWallet(walletName);

                var money = new Money(amount, MoneyUnit.XRC);
                var hdaccount = mywallet.GetAccountsByCoinType((CoinType)this.Network.Consensus.CoinType).ToArray().First();
                var transaction = SendMoneyProcessing(hdaccount.Name, walletName, address, password, money.Satoshi, feeType);

                return this.Json(ResultHelper.BuildResultResponse(transaction.GetHash().ToString()));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Sends the money.
        /// </summary>
        /// <param name="hdAcccountName">Name of the hd acccount.</param>
        /// <param name="walletName">Name of the wallet.</param>
        /// <param name="targetAddress">The target address.</param>
        /// <param name="password">The password.</param>
        /// <param name="satoshi">The satoshi.</param>
        /// <returns>(Transaction) Object with information.</returns>
        [ActionName("sendmoney")]
        [ActionDescription("Sends the money.")]
        public Transaction SendMoney(string hdAcccountName, string walletName, string targetAddress, string password, decimal satoshi)
        {
            return SendMoneyProcessing(hdAcccountName, walletName, targetAddress, password, satoshi, FeeType.Low);
        }

        /// <summary>
        /// Sends the money.
        /// </summary>
        /// <param name="hdAcccountName">Name of the hd acccount.</param>
        /// <param name="walletName">Name of the wallet.</param>
        /// <param name="targetAddress">The target address.</param>
        /// <param name="passwordBase64">The password in Base64.</param>
        /// <param name="satoshi">The satoshi.</param>
        /// <returns>(Transaction) Object with information.</returns>
        [ActionName("sendmoneybase64")]
        [ActionDescription("Sends the money.")]
        public IActionResult SendMoneyBase64(string hdAcccountName, string walletName, string targetAddress, string passwordBase64, decimal satoshi)
        {
            var password = Encoding.UTF8.GetString(Convert.FromBase64String(passwordBase64));
            return SendMoneyResponse(hdAcccountName, walletName, targetAddress, password, satoshi, FeeType.Low);
        }

        /// <summary>
        /// Sends the money.
        /// </summary>
        /// <param name="hdAcccountName">Name of the hd acccount.</param>
        /// <param name="walletName">Name of the wallet.</param>
        /// <param name="targetAddress">The target address.</param>
        /// <param name="password">The password.</param>
        /// <param name="satoshi">The satoshi.</param>
        /// <param name="feeType">Fee type VERYLOW, LOW, MEDIUM, HIGH, VERYHIGH.</param>
        /// <returns>(Transaction) Object with information.</returns>
        [ActionName("sendmoneyfee")]
        [ActionDescription("Sends the money.")]
        public Transaction SendMoneyFee(string hdAcccountName, string walletName, string targetAddress, string password, decimal satoshi, string feeType)
        {
            var convFeeType = FeeType.Low;
            Enum.TryParse(feeType, true, out convFeeType);
            return SendMoneyProcessing(hdAcccountName, walletName, targetAddress, password, satoshi, FeeType.Low);
        }

        /// <summary>
        /// Sends the money.
        /// </summary>
        /// <param name="hdAcccountName">Name of the hd acccount.</param>
        /// <param name="walletName">Name of the wallet.</param>
        /// <param name="targetAddress">The target address.</param>
        /// <param name="passwordBase64">The password in Base64.</param>
        /// <param name="satoshi">The satoshi.</param>
        /// <param name="feeType">Fee type VERYLOW, LOW, MEDIUM, HIGH, VERYHIGH.</param>
        /// <returns>(Transaction) Object with information.</returns>
        [ActionName("sendmoneyfeebase64")]
        [ActionDescription("Sends the money.")]
        public IActionResult SendMoneyFeeBase64(string hdAcccountName, string walletName, string targetAddress, string passwordBase64, decimal satoshi, string feeType)
        {
            var convFeeType = FeeType.Low;
            Enum.TryParse(feeType, true, out convFeeType);
            var password = Encoding.UTF8.GetString(Convert.FromBase64String(passwordBase64));
            return SendMoneyResponse(hdAcccountName, walletName, targetAddress, password, satoshi, FeeType.Low);
        }

        /// <summary>
        /// Private function with response for SendMoney function.
        /// </summary>
        private IActionResult SendMoneyResponse(string hdAcccountName, string walletName, string targetAddress, string password, decimal satoshi, FeeType feeType)
        {
            try
            {
                var tx = SendMoneyProcessing(hdAcccountName, walletName, targetAddress, password, satoshi, feeType);
                return this.Json(ResultHelper.BuildResultResponse(tx));
            }
            catch (SecurityException e)
            {
                return this.Json(ResultHelper.BuildResultResponse(-1, e.Message, 0));
            }
            catch (FormatException e)
            {
                return this.Json(ResultHelper.BuildResultResponse(-2, e.Message, 0));
            }
            catch (WalletException e)
            {
                return this.Json(ResultHelper.BuildResultResponse(-3, e.Message, 0));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return this.Json(ResultHelper.BuildResultResponse(0, e.Message, 0));
            }
        }

        /// <summary>
        /// Private function to process SendMoney function.
        /// </summary>
        private Transaction SendMoneyProcessing(string hdAcccountName, string walletName, string targetAddress, string password, decimal satoshi, FeeType feeType)
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

                var maturity = (int)this.Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;

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
                    MinConfirmations = maturity,
                    FeeType = feeType,
                    Sign = true
                };

                var controller = this.FullNode.NodeService<WalletController>();

                var fundTransaction = transaction.BuildTransaction(context);
                var response = controller.SendTransaction(new SendTransactionRequest(fundTransaction.ToHex()));
                if (response.GetType() == typeof(ErrorResult)) return null;

                return fundTransaction;
            }

            return null;
        }

        /// <summary>
        /// Retrieves the history of a wallet.
        /// </summary>
        /// <param name="walletName">The wallet name.</param>
        /// <param name="hdAcccountName">The hdAcccountName. Default = "account 0".</param>
        /// <returns>(WalletHistoryModel) Object with history.</returns>
        [ActionName("gethistory")]
        [ActionDescription("Retrieves the history of a wallet.")]
        public IActionResult GetHistory(string walletName, string hdAcccountName = DEFAULT_ACCOUNT_NAME)
        {
            Guard.NotNull(walletName, nameof(walletName));
            if (string.IsNullOrEmpty(hdAcccountName)) {
                hdAcccountName = DEFAULT_ACCOUNT_NAME;
            }

            try
            {
                WalletHistoryModel model = new WalletHistoryModel();

                // Get a list of all the transactions found in an account (or in a wallet if no account is specified), with the addresses associated with them.
                IEnumerable<AccountHistory> accountsHistory = this.walletManager.GetHistory(walletName, hdAcccountName);

                var feeRate = new FeeRate(this.Settings.MinTxFeeRate.FeePerK);
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

                            //var currentTransaction = blockManager.BlockRepository.GetTrxAsync(spendingTransactionId).GetAwaiter().GetResult();
                            //if (currentTransaction != null) sentItem.Fee = feeRate.GetFee(currentTransaction);

                            // The fee is calculated as follows: funds in utxo - amount spent - amount sent as change.
                            //if (sentItem.Fee == null) //try calculation
                            //{
                            sentItem.Fee = inputsAmount - sentItem.Amount - (changeAddress == null ? 0 : changeAddress.Transaction.Amount);
                            //}

                            // Mined coins add more coins to the total out.
                            // That makes the fee negative. If that's the case ignore the fee.
                            if ((sentItem.Fee == null) || (sentItem.Fee < 0))
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
                        CoinType = (CoinType)this.Network.Consensus.CoinType,
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
        /// <returns>(bool) Return True or False.</returns>
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
        /// <param name="startHeight">The start height.</param>
        /// <param name="stopHeight">The last block height that should be scanned.</param>
        /// <returns>(RescanBlockChainModel) Start height and stopped height.</returns>
        [ActionName("rescanblockchain")]
        [ActionDescription("Rescan the local blockchain for wallet related transactions.")]
        public IActionResult RescanBlockChain(int? startHeight = null, int? stopHeight = null)
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
                //bug genesis does not have transctions and can't be scanned start from 1 if below 1
                if (startHeight.Value < 1)
                {
                    startHeight  = 1;
                }

                var result = new RescanBlockChainModel();
                result.StartHeight = startHeight.Value;
                inRescan = true;

                Console.WriteLine(string.Format("Start rescan at {0}", result.StartHeight));

                lock (this.walletManager.GetLock())
                {
                    var walletUpdated = false;

                    for (int i = startHeight.Value; i <= stopHeight; i++)
                    {
                        if (!inRescan) break;

                        Console.WriteLine(string.Format("Scanning {0}", i));

                        var chainedHeader = chainRepository.GetBlock(i);
                        var block = blockStoreManager.BlockRepository.GetAsync(chainedHeader.HashBlock).Result;

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
                        foreach (var wallet in this.walletManager.Wallets.Values)
                        {
                            wallet.BlockLocator = chainedHeader.GetLocator().Blocks;

                            foreach (AccountRoot accountRoot in wallet.AccountsRoot.Where(a => a.CoinType == (CoinType)this.Network.Consensus.CoinType))
                            {
                                if ((accountRoot.LastBlockSyncedHeight == null) || (accountRoot.LastBlockSyncedHeight < i))
                                {
                                    accountRoot.LastBlockSyncedHeight = chainedHeader.Height;
                                    accountRoot.LastBlockSyncedHash = chainedHeader.HashBlock;
                                }
                            }
                        }

                        result.StopHeight = i;
                    }

                    if (walletUpdated)
                    {
                        this.walletManager.SaveWallets();
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
        /// The destination directory or file.
        /// </summary>
        /// <param name="walletName">Wallet Name to backup.</param>
        /// <param name="destination">The destination file.</param>
        /// <returns>(bool) Return true if it is done.</returns>
        [ActionName("backupwallet")]
        [ActionDescription("The destination directory or file.")]
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
        /// <param name="walletName">Wallet Name to backup.</param>
        /// <param name="filename">The filename with path relative to config folder.</param>
        /// <returns>(string) The filename with full absolute path.</returns>
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
                var fullFileName = ((WalletManager)this.walletManager).DBreezeStorage.FolderPath + filename;

                Directory.CreateDirectory(fullFileName);

                var chainRepository = this.FullNode.NodeService<ConcurrentChain>();
                var chainedHeader = chainRepository.GetBlock(chainRepository.Height);

                var fileContent = new StringBuilder();
                fileContent.AppendLine("# Wallet dump created by xRhodium" + Assembly.GetEntryAssembly().GetName().Version.ToString());
                fileContent.AppendLine("# * Created on " + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssK"));
                fileContent.AppendLine("# * Best block at time of backup was " + chainRepository.Height + " ," + chainedHeader.HashBlock);
                fileContent.AppendLine("# * mined on" + Utils.UnixTimeToDateTime(chainedHeader.Header.Time).DateTime.ToString("yyyy-MM-ddTHH:mm:ssK"));
                fileContent.AppendLine(string.Empty);

                var addresses = wallet.GetAllAddressesByCoinType((CoinType)this.Network.Consensus.CoinType);

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

        /// <summary>
        /// Returns a new Bitcoin address for receiving payments.
        /// </summary>
        /// <param name="walletName">The Wallet name.</param>
        /// <returns>(string) The new bitcoin address.</returns>
        [ActionName("getnewaddress")]
        [ActionDescription("Returns a new Bitcoin address for receiving payments.")]
        public IActionResult GetNewAddress(string walletName)
        {
            try
            {
                var result = string.Empty;
                walletName = string.IsNullOrEmpty(walletName) ? WalletRPCUtil.DEFAULT_WALLET : walletName;

                var wallet = this.walletManager.GetWalletByName(walletName);
                var address = this.walletManager.GetNewAddresses(wallet, 1);

                if (address.Any())
                {
                    var firstAddress = address.Last();
                    result = firstAddress.Address;
                }

                return this.Json(ResultHelper.BuildResultResponse(result));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Returns a new Bitcoin address, for receiving change. This is for use with raw transactions, NOT normal use.
        /// </summary>
        /// <returns>(string) The address.</returns>
        [ActionName("getrawchangeaddress")]
        [ActionDescription("Returns a new Bitcoin address, for receiving change. This is for use with raw transactions, NOT normal use.")]
        public IActionResult GetRawChangeAddress()
        {
            try
            {
                var address = this.walletKeyPool.GetUnunsedKey();

                return this.Json(ResultHelper.BuildResultResponse(address.ToString()));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Returns the total amount received by the given address in transactions with at least minconf confirmations.
        /// </summary>
        /// <param name="address">The bitcoin address for transactions.</param>
        /// <param name="walletName">Limit calculation for some wallet only.</param>
        /// <returns>(decimal) The total amount in XRC received at this address for one or all wallets.</returns>
        [ActionName("getreceivedbyaddress")]
        [ActionDescription("Returns the total amount received by the given address in transactions with at least minconf confirmations.")]
        public IActionResult GetReceivedByAddress(string address, string walletName = null)
        {
            try
            {
                if (string.IsNullOrEmpty(address))
                {
                    throw new ArgumentNullException("address");
                }

                var result = this.walletManager.GetAddressBalance(address);

                return this.Json(ResultHelper.BuildResultResponse(result.AmountConfirmed));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// DEPRECATED RPC. This will only show the balance of the first derivation path.
        /// </summary>
        /// <param name="account"></param>
        /// <returns></returns>
        [ActionName("getreceivedbyamount")]
        [ActionDescription("DEPRECATED.")]
        public IActionResult GetReceivedByAccount(string account, int minconf = 1)
        {
            try
            {
                var hdAccount = WalletRPCUtil.GetAccountFromWalletForDeprecatedRpcs(
                    (WalletManager) walletManager,
                    this.Network,
                    account
                );

                var confirmedAmount = hdAccount.GetSpendableAmount(this.chain).ConfirmedAmount.ToUnit(MoneyUnit.XRC);

                return this.Json(
                    ResultHelper.BuildResultResponse(confirmedAmount)
                );
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Returns the server's total unconfirmed balance.
        /// </summary>
        /// <returns>(decimal) XRC unconfirmed amount.</returns>
        [ActionName("getunconfirmedbalance")]
        [ActionDescription("Returns the server's total unconfirmed balance.")]
        public IActionResult GetUnconfirmedBalance()
        {
            try
            {
                long unspendAmountSatoshi = 0;

                foreach (var name in this.walletManager.Wallets.Keys)
                {
                    var balances = this.walletManager.GetBalances(name);
                    var accountBalances = balances.Where(a => a.Account.GetCoinType() == (CoinType)this.Network.Consensus.CoinType).ToList();

                    foreach (var itemAccount in accountBalances)
                    {
                        unspendAmountSatoshi += itemAccount.AmountUnconfirmed.Satoshi;
                    }
                }

                var money = new Money(unspendAmountSatoshi);
                return this.Json(ResultHelper.BuildResultResponse(money.ToUnit(MoneyUnit.XRC)));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Returns an object containing various wallet state info.
        /// </summary>
        /// <param name="walletName">The wallet name.</param>
        /// <returns>(GetWalletInfoModel) Return info object.</returns>
        [ActionName("getwalletinfo")]
        [ActionDescription("Returns an object containing various wallet state info.")]
        public IActionResult GetWalletInfo(string walletName)
        {
            if (this.useDeprecatedWalletRPC && string.IsNullOrEmpty(walletName)) walletName = WalletRPCUtil.DEFAULT_WALLET;
            return GetWalletInfoBase(walletName);
        }

        /// <summary>
        /// Returns the state of a wallet.
        /// </summary>
        /// <param name="walletName"></param>
        /// <returns></returns>
        public IActionResult GetWalletInfoBase(string walletName)
        {
            try
            {
                if (string.IsNullOrEmpty(walletName))
                {
                    throw new ArgumentNullException("walletName");
                }

                var result = new GetWalletInfoModel();

                var wallet = this.walletManager.GetWalletByName(walletName);
                var balances = this.walletManager.GetBalances(walletName);

                var accountBalances = balances.Where(a => a.Account.GetCoinType() == (CoinType)this.Network.Consensus.CoinType).ToList();

                result.Balance = new Money(accountBalances.Sum(a => a.AmountConfirmed.Satoshi)).ToUnit(MoneyUnit.XRC);
                result.WalletVersion = this.FullNode?.Version?.ToString() ?? string.Empty;
                result.WalletName = walletName;
                result.UnconfirmedBalance = new Money(accountBalances.Sum(a => a.AmountUnconfirmed.Satoshi)).ToUnit(MoneyUnit.XRC);
                result.Immaturebalance = new Money(accountBalances.Sum(a => a.AmountImmature.Satoshi)).ToUnit(MoneyUnit.XRC);
                result.PayTxFee = this.walletFeePolicy.GetPayTxFee().FeePerK.ToUnit(MoneyUnit.XRC);

                var txCount = wallet.GetAllTransactionsByCoinType((CoinType)this.Network.Consensus.CoinType);
                result.TxCount = txCount == null ? 0 : txCount.Count();

                var passwordExpiration = this.walletManager.WalletSecrets.GetWalletPasswordExpiration(wallet.Name);
                if (passwordExpiration == null)
                {
                    throw new ArgumentNullException("password");
                }
                else
                {
                    if (passwordExpiration < DateTime.Now)
                    {
                        result.UnlockedUntil = 0;
                    }
                    else
                    {
                        result.UnlockedUntil = Utils.DateTimeToUnixTime(passwordExpiration);
                    }
                }

                result.KeyPoolSize = this.walletKeyPool.GetKeyPoolSize();
                result.KeyPoolSizeHdInternal = 0;
                result.KeyPoolOldest = this.walletKeyPool.GetKeyPoolTimeStamp();

                return this.Json(ResultHelper.BuildResultResponse(result));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Fills the keypool.
        /// </summary>
        /// <param name="newsize">The new keypool size.</param>
        /// <returns>(bool) True or false.</returns>
        [ActionName("keypoolrefill")]
        [ActionDescription("Fills the keypool.")]
        public IActionResult KeyPoolReFill(int newsize = 100)
        {
            try
            {
                this.walletKeyPool.ReFill(newsize);
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Adds address that can be watched as if it were in your wallet but cannot be used to spend. Requires a new wallet backup.
        /// </summary>
        /// <param name="walletName">The wallet name.</param>
        /// <param name="address">Base58 valid address for import.</param>
        /// <param name="rescan">Rescan blockchain. It need a long time. Default True.</param>
        /// <returns>(hdAddress) New hdAddress object, null or error.</returns>
        [ActionName("importaddress")]
        [ActionDescription("")]
        public IActionResult ImportAddress(string walletName, string address, bool rescan = true)
        {
            try
            {
                if (string.IsNullOrEmpty(walletName))
                {
                    throw new ArgumentNullException("walletName");
                }
                if (string.IsNullOrEmpty(address))
                {
                    throw new ArgumentNullException("address");
                }

                var isP2PKH = false;
                var isP2SH = false;

                // P2PKH
                if (BitcoinPubKeyAddress.IsValid(address, ref this.Network))
                {
                    isP2PKH = true;
                }
                //P2SH
                else if (BitcoinScriptAddress.IsValid(address, ref this.Network))
                {
                    isP2SH = true;
                }

                if ((!isP2PKH) && (!isP2SH))
                {
                    throw new ArgumentNullException("address");
                }

                HdAddress hdAddress = null;
                var wallet = this.walletManager.GetWalletByName(walletName);
                var account = wallet.AccountsRoot.FirstOrDefault(a => a.CoinType == (CoinType)this.Network.Consensus.CoinType);
                if (account != null)
                {
                    var hdAccount = account.GetAccountByName(DEFAULT_ACCOUNT_NAME);

                    if (isP2PKH)
                    {
                        hdAddress = hdAccount.ImportBase58Address(this.Network, address);
                    }
                    else
                    {
                        hdAddress = hdAccount.ImportScriptAddress(this.Network, address);
                    }

                    if (hdAddress != null)
                    {
                        this.walletManager.UpdateKeysLookupLock(new[] { hdAddress }, walletName);
                        this.walletManager.SaveWallet(wallet);

                        if (rescan) this.RescanBlockChain(null,null);
                    }
                }

                return this.Json(ResultHelper.BuildResultResponse(hdAddress));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Adds a public key (in hex) that can be watched as if it were in your wallet but cannot be used to spend. Requires a new wallet backup.
        /// <p>This call can take minutes to complete if rescan is true, during that time, other rpc calls may report that the imported pubkey<br/>
        /// exists but related transactions are still missing, leading to temporarily incorrect/bogus balances and unspent outputs until rescan completes.</p>
        /// </summary>
        /// <param name="walletName">The wallet name.</param>
        /// <param name="pubKey">You new pubkey hex.</param>
        /// <param name="rescan">Rescan blockchain. It need a long time. Default True.</param>
        /// <returns>(hdAddress) New hdAddress object, null or error.</returns>
        [ActionName("importpubkey")]
        [ActionDescription("Adds a public key (in hex) that can be watched as if it were in your wallet but cannot be used to spend. Requires a new wallet backup.")]
        public IActionResult ImportPubKey(string walletName, string pubKey, bool rescan = true)
        {
            try
            {
                if (string.IsNullOrEmpty(walletName))
                {
                    throw new ArgumentNullException("walletName");
                }
                if (string.IsNullOrEmpty(pubKey))
                {
                    throw new ArgumentNullException("pubKey");
                }

                HdAddress hdAddress = null;
                var wallet = this.walletManager.GetWalletByName(walletName);
                var account = wallet.AccountsRoot.FirstOrDefault(a => a.CoinType == (CoinType)this.Network.Consensus.CoinType);
                if (account != null)
                {
                    var hdAccount = account.GetAccountByName(DEFAULT_ACCOUNT_NAME);
                    hdAddress = hdAccount.CreateAddresses(this.Network, pubKey);
                    if (hdAddress != null)
                    {
                        this.walletManager.UpdateKeysLookupLock(new[] { hdAddress }, walletName);
                        this.walletManager.SaveWallet(wallet);

                        if (rescan) this.RescanBlockChain();
                    }
                }

                return this.Json(ResultHelper.BuildResultResponse(hdAddress));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Lists groups of addresses which have had their common ownership made public by common use as inputs or as the resulting change in past transactions.
        /// </summary>
        /// <param name="walletName">The wallet name.</param>
        /// <returns>(List, ListReceivedByAddressModel) Object with informations.</returns>
        [ActionName("listaddressgroupings")]
        [ActionDescription("Lists groups of addresses which have had their common ownership made public by common use as inputs or as the resulting change in past transactions.")]
        public IActionResult ListAddressGroupings(string walletName)
        {
            try
            {
                var result = new List<ListReceivedByAddressModel>();

                if (string.IsNullOrEmpty(walletName))
                {
                    throw new ArgumentNullException("walletName");
                }

                var wallet = this.walletManager.GetWalletByName(walletName);
                var accountsRoot = wallet.AccountsRoot.Where(a => a.CoinType == (CoinType)this.Network.Consensus.CoinType).ToList();
                var chainRepository = this.FullNode.NodeService<ConcurrentChain>();

                foreach (var itemAccount in accountsRoot)
                {
                    foreach (var itemHdAccount in itemAccount.Accounts)
                    {
                        foreach (var itemAddress in itemHdAccount.ExternalAddresses)
                        {
                            var newItemResult = GetNewReceivedByAddressModel(chainRepository, walletName, itemAddress.Address, false);
                            if (newItemResult != null) result.Add(newItemResult);
                        }

                        foreach (var itemAddress in itemHdAccount.InternalAddresses)
                        {
                            var newItemResult = GetNewReceivedByAddressModel(chainRepository, walletName, itemAddress.Address, false);
                            if (newItemResult != null) result.Add(newItemResult);
                        }
                    }
                }

                return this.Json(ResultHelper.BuildResultResponse(result));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Helper for filling ListReceivedByAddressModel model.
        /// </summary>
        /// <param name="chainRepository">Chain repository object.</param>
        /// <param name="walletName">The wallet name.</param>
        /// <param name="address">The address.</param>
        /// <param name="include_empty">Whether to include addresses that haven't received any payments.</param>
        /// <returns>(ListReceivedByAddressModel) Return filled model or null.</returns>
        private ListReceivedByAddressModel GetNewReceivedByAddressModel(ConcurrentChain chainRepository, string walletName, string address, bool include_empty = false)
        {
            var newItemResult = new ListReceivedByAddressModel();
            var balance = this.walletManager.GetAddressBalance(address, walletName);

            if ((balance.GetTotalAmount() == Money.Zero) && (!include_empty))
            {
                return null;
            }

            newItemResult.Address = address;
            newItemResult.Amount = balance.GetTotalAmount().ToUnit(MoneyUnit.XRC);

            if (balance.Transactions != null)
            {
                newItemResult.TxIds = balance.Transactions.Select(a => a.Id).ToList();

                var lastTx = balance.Transactions.Last();
                if (lastTx.BlockHash != null)
                {
                    var chainedHeader = this.ConsensusLoop.Chain.GetBlock(lastTx.BlockHash);
                    newItemResult.Confirmations = chainRepository.Tip.Height - chainedHeader.Height + 1;
                }
                else
                {
                    newItemResult.Confirmations = 0;
                }
            }

            return newItemResult;
        }

        /// <summary>
        /// Returns list of temporarily unspendable outputs. See the lockunspent call to lock and unlock transactions for spending.
        /// </summary>
        /// <returns>(List, TxOutLock) Object with locked transactions.</returns>
        [ActionName("listlockunspent")]
        [ActionDescription("Returns list of temporarily unspendable outputs. See the lockunspent call to lock and unlock transactions for spending.")]
        public IActionResult ListLockUnspent()
        {
            try
            {
                var txLocks = new List<TxOutLock>();

                foreach (var itemMemTxLock in this.walletManager.LockedTxOut)
                {
                    var newLock = new TxOutLock();
                    newLock.TxId = itemMemTxLock.Key;
                    newLock.Vout = itemMemTxLock.Value;

                    txLocks.Add(newLock);
                }

                return this.Json(ResultHelper.BuildResultResponse(txLocks));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// List balances by receiving address.
        /// </summary>
        /// <param name="walletName">The wallet name.</param>
        /// <param name="minconf">The minimum number of confirmations before payments are included.</param>
        /// <param name="include_empty">Whether to include addresses that haven't received any payments.</param>
        /// <returns>(List, ListReceivedByAddressModel) Object with informations.</returns>
        [ActionName("listreceivedbyaddress")]
        [ActionDescription("List balances by receiving address.")]
        public IActionResult ListReceivedByAddress(string walletName, int minconf = 1, bool include_empty = false)
        {
            try
            {
                var result = new List<ListReceivedByAddressModel>();

                if (string.IsNullOrEmpty(walletName))
                {
                    throw new ArgumentNullException("walletName");
                }

                var wallet = this.walletManager.GetWalletByName(walletName);
                var accountsRoot = wallet.AccountsRoot.Where(a => a.CoinType == (CoinType)this.Network.Consensus.CoinType).ToList();
                var chainRepository = this.FullNode.NodeService<ConcurrentChain>();

                foreach (var itemAccount in accountsRoot)
                {
                    foreach (var itemHdAccount in itemAccount.Accounts)
                    {
                        foreach (var itemAddress in itemHdAccount.ExternalAddresses)
                        {
                            var newItemResult = GetNewReceivedByAddressModel(chainRepository, walletName, itemAddress.Address, include_empty);
                            if (newItemResult != null) result.Add(newItemResult);
                        }
                    }
                }

                return this.Json(ResultHelper.BuildResultResponse(result));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Get all transactions in blocks since block [blockhash], or all transactions if omitted.
        /// </summary>
        /// <param name="param1">The wallet name or block hash if you use compatibility mode.</param>
        /// <param name="param2">The block hash to list transactions since or nth block hash from main chain if you use compatibility mode.</param>
        /// <param name="param3">Return the nth block hash from the main chain. e.g. 1 would mean the best block hash or null if you use compatibility mode.</param>
        /// <returns></returns>
        [ActionName("listsinceblock")]
        [ActionDescription("Get all transactions in blocks since block [blockhash], or all transactions if omitted.")]
        public IActionResult ListSinceBlock(string param1 = null, string param2 = null, string param3 = null)
        {
            int targetConfirmations = 0;
            if (this.useDeprecatedWalletRPC)
            {
                var blockhash = param1;
                                
                int.TryParse(param2, out targetConfirmations);

                return ListSinceBlockResponse(WalletRPCUtil.DEFAULT_WALLET, blockhash, targetConfirmations);
            }

            int.TryParse(param3, out targetConfirmations);
            return ListSinceBlockResponse(param1, param2, targetConfirmations);
        }

        /// <summary>
        /// Get all transactions in blocks since block [blockhash], or all transactions if omitted.
        /// </summary>
        /// <param name="walletName">The wallet name.</param>
        /// <param name="blockhash">The block hash to list transactions since.</param>
        /// <param name="target_confirmations">Return the nth block hash from the main chain. e.g. 1 would mean the best block hash.</param>
        /// <returns></returns>
        public IActionResult ListSinceBlockResponse(string walletName, string blockhash = null, int target_confirmations = 0)
        {
            try
            {
                if (string.IsNullOrEmpty(walletName))
                {
                    throw new ArgumentNullException("walletName");
                }

                var result = new List<TransactionVerboseModel>();
                var wallet = this.walletManager.GetWalletByName(walletName);
                var txList = wallet.GetAllTransactionsByCoinType((CoinType)this.Network.Consensus.CoinType);
                var chainRepository = this.FullNode.NodeService<ConcurrentChain>();

                if (!string.IsNullOrEmpty(blockhash) && target_confirmations > 0)
                {
                    throw new ArgumentException("blockhash  and target_confirmations can't be specified at once. choose either hash or confirmations");
                }

                ChainedHeader startChainedHeader = null;
                if (!string.IsNullOrEmpty(blockhash))
                {

                    var uintBlockHash = new uint256(blockhash);
                    startChainedHeader = this.ConsensusLoop.Chain.GetBlock(uintBlockHash);
                }

                if (target_confirmations > 0)
                {
                    int target_block = (chainRepository.Tip.Height - target_confirmations);
                    startChainedHeader = chainRepository.GetBlock(target_block);
                }

                var chainedTip = chainRepository.Tip;

                if ((txList != null) && (txList.Count() > 0))
                {
                    txList = txList
                        .Where(t => t.BlockHash != null)
                        .OrderBy(t => t.BlockHeight)
                        .GroupBy(tx => tx.Id)
                        .Select(txs => txs.First())
                        .ToList();

                    foreach (var txItem in txList)
                    {
                        var addTx = false;
                        var addSpendingTx = false;
                        if (startChainedHeader != null)
                        {
                            if (txItem.BlockHeight < startChainedHeader.Height)
                            {
                                if ((txItem.SpendingDetails != null) && (txItem.SpendingDetails.BlockHeight >= startChainedHeader.Height))
                                {
                                    addSpendingTx = true;
                                }
                                else
                                {
                                    continue;
                                }
                            }
                            else
                            {
                                addTx = true;
                                if (txItem.SpendingDetails != null) addSpendingTx = true;
                            }
                        }
                        else
                        {
                            addTx = true;
                            if (txItem.SpendingDetails != null) addSpendingTx = true;
                        }

                        if (addTx)
                        {
                            result = result.Concat(DescribeTransaction(txItem, walletName, chainedTip)).ToList();
                        }

                        if (addSpendingTx)
                        {
                            var spendingDataTx = new TransactionData();
                            ChainedHeader chainedHeader = null;
                            //unconfirmed transactions have nullable BlockHeight that throws error if access .value unchecked
                            if (txItem.SpendingDetails.BlockHeight.HasValue)
                            {
                                chainedHeader = this.ConsensusLoop.Chain.GetBlock(txItem.SpendingDetails.BlockHeight.Value);
                            }
                            if (chainedHeader != null)
                            {
                                spendingDataTx.BlockHash = chainedHeader.HashBlock;
                                spendingDataTx.Id = txItem.SpendingDetails.TransactionId;

                                result = result.Concat(DescribeTransaction(spendingDataTx, walletName, chainedTip)).ToList();
                            }
                        }
                    }
                }

                //filter duplicates & sort it
                var clearResult = new List<TransactionVerboseModel>();
                foreach (var item in result)
                {
                    var exist = clearResult.Exists(e =>
                                       e.TxId == item.TxId &&
                                       e.Amount == item.Amount &&
                                       e.BlockHash == item.BlockHash &&
                                       e.Category == item.Category &&
                                       e.Time == item.Time);

                    if (!exist)
                    {
                        clearResult.Add(item);
                    }
                }
                clearResult = clearResult.OrderBy(o => o.BlockHeight)
                    .ThenBy(o => o.TxId)
                    .ThenBy(o => o.Category)
                    .ToList();

                if (this.useDeprecatedWalletRPC)
                {
                    var resultObj = new Dictionary<string, List<TransactionVerboseModel>>();
                    resultObj.Add("transactions", clearResult);
                    return this.Json(ResultHelper.BuildResultResponse(resultObj));
                }
                else
                {
                    return this.Json(ResultHelper.BuildResultResponse(clearResult));
                }
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Returns up to 'count' most recent transactions skipping the first 'from' transactions for account 'account'.
        /// </summary>
        /// <param name="walletName">The wallet name.</param>
        /// <param name="count">The number of transactions to return.</param>
        /// <param name="from">The number of transactions to skip.</param>
        /// <returns>(List, TransactionVerboseModel) Object with information about transaction.</returns>
        [ActionName("listtransactions")]
        [ActionDescription("Returns up to 'count' most recent transactions skipping the first 'from' transactions for account 'account'.")]
        public IActionResult ListTransactions(string walletName, int count = 10, int from = 0)
        {
            try
            {
                var result = new List<TransactionVerboseModel>();

                if (string.IsNullOrEmpty(walletName)) walletName = WalletRPCUtil.DEFAULT_WALLET;

                if (walletName == "*")
                {
                    var walletNames = this.walletManager.GetWalletsNames();
                    foreach(var w in walletNames)
                    {
                        var wallet = this.walletManager.GetWalletByName(w);
                        result = result.Concat(ReportForListTransactions(wallet, count, from)).ToList();
                    }
                }
                else
                {
                    var wallet = this.walletManager.GetWalletByName(walletName);
                    result = ReportForListTransactions(wallet, count, from);
                }

                return this.Json(ResultHelper.BuildResultResponse(result));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        private List<TransactionVerboseModel> ReportForListTransactions(Wallet wallet, int count = 10, int from = 0)
        {
            var result = new List<TransactionVerboseModel>();
            var walletName = wallet.Name;

            var txList = wallet.GetAllTransactionsByCoinType((CoinType)this.Network.Consensus.CoinType);
            var chainRepository = this.FullNode.NodeService<ConcurrentChain>();

            var i = 0;
            var chainedTip = chainRepository.Tip;

            if ((txList != null) && (txList.Count() > 0))
            {
                txList = txList
                        .Where(t => t.BlockHash != null)
                        .OrderByDescending(t => t.BlockHeight)
                        .GroupBy(tx => tx.Id)
                        .Select(txs => txs.First())
                        .ToList();

                foreach (var txItem in txList)
                {
                    if (i >= from)
                    {
                        if (i > from + count - 1) break;
                        result = result.Concat(DescribeTransaction(txItem, walletName, chainedTip)).ToList();
                    }

                    i++;
                }
            }

            return result;
        }

        private List<TransactionVerboseModel> DescribeTransaction(TransactionData txItem,
                                                                  string walletName,
                                                                  ChainedHeader chainedTip)
        {
            var tx = this.blockRepository.GetTrxAsync(txItem.Id).GetAwaiter().GetResult();
            var block = this.blockRepository.GetAsync(txItem.BlockHash).GetAwaiter().GetResult();
            var chainedHeader = this.ConsensusLoop.Chain.GetBlock(txItem.BlockHash);

            if ((tx != null) && (block != null) && (chainedHeader != null))
            {
                chainedHeader.Block = block;

                //read prevTx from blockchain
                var prevTxList = new List<IndexedTxOut>();
                foreach (var itemInput in tx.Inputs)
                {
                    var prevTx = this.blockRepository.GetTrxAsync(itemInput.PrevOut.Hash).GetAwaiter().GetResult();
                    if (prevTx != null)
                    {
                        if (prevTx.Outputs.Count() > itemInput.PrevOut.N)
                        {
                            var indexed = prevTx.Outputs.AsIndexedOutputs();
                            prevTxList.Add(indexed.First(t => t.N == itemInput.PrevOut.N));
                        }
                    }
                }

                return TransactionVerboseModel.GenerateList(
                        tx, prevTxList,
                        block, chainedHeader,
                        chainedTip, walletName,
                        this.Network, this.walletManager as WalletManager
                    ).ToList();
            }
            else {
                return new List<TransactionVerboseModel>();
            }
        }

        /// <summary>
        /// Returns array of unspent transaction outputs. With between minconf confirmations.
        /// </summary>
        /// <param name="walletName">The wallet name.</param>
        /// <param name="minconf">The minimum confirmations to filter.</param>
        /// <returns>(List, TransactionVerboseModel) Object with transaction information.</returns>
        [ActionName("listunspent")]
        [ActionDescription("Returns array of unspent transaction outputs. With between minconf confirmations.")]
        public IActionResult ListUnspent(string walletName, int minconf = 1)
        {
            try
            {
                var result = new List<TransactionVerboseModel>();

                if (string.IsNullOrEmpty(walletName))
                {
                    throw new ArgumentNullException("walletName");
                }

                var chainRepository = this.FullNode.NodeService<ConcurrentChain>();
                var wallet = this.walletManager.GetWalletByName(walletName);

                var unspendTx = wallet.GetAllSpendableTransactions((CoinType)this.Network.Consensus.CoinType, this.Network, chainRepository.Height, minconf);

                if (unspendTx != null)
                {
                    foreach (var itemUnspendTx in unspendTx)
                    {
                        var txItem = itemUnspendTx.Transaction;
                        if (txItem == null)
                        {
                            continue;
                        }
                        if (txItem.BlockHash == null) {
                            continue; // Most likely a mempool tx
                        }                       

                        var tx = this.blockRepository.GetTrxAsync(txItem.Id).GetAwaiter().GetResult();
                        var block = this.blockRepository.GetAsync(txItem.BlockHash).GetAwaiter().GetResult();
                        var chainedHeader = this.ConsensusLoop.Chain.GetBlock(txItem.BlockHash);
                        var chainedTip = this.ConsensusLoop.Chain.Tip;

                        if ((tx != null) && (block != null) && (chainedHeader != null))
                        {
                            var outputModel = new TransactionVerboseModel
                            {
                                Amount = txItem.Amount.ToDecimal(MoneyUnit.XRC),
                                Address = txItem.ScriptPubKey.GetDestinationAddress(this.Network)?.ToString(),
                                Category = "receive",
                                VOut = (uint)itemUnspendTx.ToOutPoint().N,
                                TxId = tx.GetHash().ToString(),
                                Size = tx.GetSerializedSize(),
                                Version = tx.Version,
                                LockTime = tx.LockTime,
                                TimeReceived = tx.Time,
                                BlockHeight = chainedHeader.Height,
                                BlockHash = chainedHeader.HashBlock.ToString(),
                                Time = (uint)block.Header.BlockTime.ToUnixTimeSeconds(),
                                BlockTime = (uint)block.Header.BlockTime.ToUnixTimeSeconds(),
                                Fee = 0,
                                Confirmations = chainedTip.Height - chainedHeader.Height + 1
                            };

                            if (tx.IsCoinBase)
                            {
                                if (outputModel.Confirmations < 10)
                                {
                                    outputModel.Category = "immature";
                                }
                                else
                                {
                                    outputModel.Category = "generate";
                                }
                            }

                            result.Add(outputModel);

                        }
                    }
                }

                return this.Json(ResultHelper.BuildResultResponse(result));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Updates list of temporarily unspendable outputs.
        /// <p>Temporarily lock (unlock=false) or unlock(unlock=true) specified transaction outputs.<br/>
        /// If no transaction outputs are specified when unlocking then all current locked transaction outputs are unlocked.<br/>
        /// A locked transaction output will not be chosen by automatic coin selection, when spending bitcoins.<br/>
        /// Locks are stored in memory only. Nodes start with zero locked outputs, and the locked output list<br/>
        /// is always cleared (by virtue of process exit) when a node stops or fails.<br/>
        /// Also see the listunspent call.</p>
        /// </summary>
        /// <param name="unlock">Whether to unlock (true) or lock (false) the specified transactions.</param>
        /// <param name="jsonTransactions">A json array of objects. Each object the txid (string) and vout (int).</param>
        /// <returns>(bool) Whether the command was successful or not.</returns>
        [ActionName("lockunspend")]
        [ActionDescription("Updates list of temporarily unspendable outputs. ")]
        public IActionResult LockUnspend(bool unlock, string jsonTransactions)
        {
            try
            {
                var txLocks = new List<TxOutLock>();
                int value;

                if (!string.IsNullOrEmpty(jsonTransactions))
                {
                    txLocks = JsonConvert.DeserializeObject<List<TxOutLock>>(jsonTransactions);
                }

                foreach (var itemMemTxLock in this.walletManager.LockedTxOut)
                {
                    if (txLocks.Count > 0)
                    {
                        foreach (var itemLock in txLocks)
                        {
                            if (itemLock.TxId == itemMemTxLock.Key)
                            {
                                if (unlock == true)
                                {
                                    this.walletManager.LockedTxOut.TryRemove(itemMemTxLock.Key, out value);
                                }
                            }
                            else
                            {
                                if (unlock == false)
                                {
                                    this.walletManager.LockedTxOut.AddOrReplace(itemLock.TxId, itemLock.Vout);
                                }
                            }
                        }
                    }
                    else
                    {
                        if (unlock == true) //remove all
                        {
                            this.walletManager.LockedTxOut.Clear();
                            break;
                        }
                    }
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
        /// Restores wallet locally from seed.
        /// </summary>
        /// <param name="passwordBase64">Transaction password in Base64 format.</param>
        /// <param name="walletName">Wallet name</param>
        /// <param name="mnemonic">Mnemonic seed (English)</param>
        /// <param name="creationDate">Wallet creation date in UnixEpoch. If unknown then 1483228800 would ensure that wallet properly synchronize. </param>
        /// <returns>(Wallet) Return object or error.</returns>
        [ActionName("restorefromseedbase64")]
        [ActionDescription("Updates list of temporarily unspendable outputs. ")]
        public IActionResult RestoreBase64(string passwordBase64, string walletName, string mnemonic, long creationDate = 1539810400)
        {
            var password = Encoding.UTF8.GetString(Convert.FromBase64String(passwordBase64));
            return Restore(password, walletName, mnemonic, creationDate);
        }

        /// <summary>
        /// Restores wallet locally from seed.
        /// </summary>
        /// <param name="password">Transaction password.</param>
        /// <param name="walletName">Wallet name</param>
        /// <param name="mnemonic">Mnemonic seed (English)</param>
        /// <param name="creationDate">Wallet creation date in UnixEpoch. If unknown then 1483228800 would ensure that wallet properly synchronize. </param>
        /// <returns>(Wallet) Return object or error.</returns>
        [ActionName("restorefromseed")]
        [ActionDescription("Updates list of temporarily unspendable outputs. ")]
        public IActionResult Restore(string password,string walletName, string mnemonic, long creationDate = 1539810400)
        {
            var date = DateTimeOffset.FromUnixTimeSeconds(creationDate).DateTime;
            Wallet wallet = this.walletManager.RecoverWallet(password, walletName, mnemonic, date);

            // start syncing the wallet from the creation date
            this.walletSyncManager.SyncFromDate(date);
            return this.Json(ResultHelper.BuildResultResponse(wallet));
        }

        /// <summary>
        /// Remove transaction from wallet and sync it again.
        /// </summary>
        /// <param name="walletName">Wallet name.</param>
        /// <param name="tx">Transaction id.</param>
        /// <returns>(bool) Whether the command was successful or not.</returns>
        [ActionName("removetransaction")]
        [ActionDescription("Remove transaction from wallet.")]
        public IActionResult RemoveTransaction(string walletName, string tx)
        {
            try
            {
                var wallet = this.walletManager.GetWalletByName(walletName);
                var result = this.walletManager.RemoveTransactionsByIds(walletName, new uint256[] { new uint256(tx) });

                if (result.Any())
                {
                    DateTimeOffset earliestDate = result.Min(r => r.Item2);
                    ChainedHeader chainedHeader = this.chain.GetBlock(this.chain.GetHeightAtTime(earliestDate.DateTime));

                    // Update the wallet and save it to the file system.
                    wallet.SetLastBlockDetailsByCoinType((CoinType)this.Network.Consensus.CoinType, chainedHeader);
                    this.walletManager.SaveWallet(wallet);

                    // Start the syncing process from the block before the earliest transaction was seen.
                    this.walletSyncManager.SyncFromHeight(chainedHeader.Height - 1);

                    IEnumerable<RemovedTransactionModel> model = result.Select(r => new RemovedTransactionModel
                    {
                        TransactionId = r.Item1,
                        CreationTime = r.Item2
                    });

                    return this.Json(ResultHelper.BuildResultResponse(true));
                }
                else
                {
                    return this.Json(ResultHelper.BuildResultResponse(false));
                }
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Stores the wallet decryption key in memory for 'timeout' seconds.
        /// </summary>
        /// <param name="password"></param>
        /// <param name="timeout"></param>
        [ActionName("walletpassphrase")]
        [ActionDescription("Stores the wallet decryption key in memory for 'timeout' seconds.")]
        public IActionResult WalletPassphrase(string password, int timeout = 1073741824)
        {
            return Walletpassword(WalletRPCUtil.DEFAULT_WALLET, password, timeout);
        }

        /// <summary>
        /// DEPRECATED RPC.
        /// </summary>
        /// <returns></returns>
        [ActionName("listaccounts")]
        [ActionDescription("DEPRECATED.")]
        public IActionResult ListAccounts()
        {
            try
            {
                var result = new Dictionary<string, decimal>();
                foreach(var walletName in walletManager.GetWalletsNames())
                {
                    var hdAccount = WalletRPCUtil.GetAccountFromWalletForDeprecatedRpcs(
                        (WalletManager)walletManager,
                        this.Network,
                        walletName
                    );

                    result.Add(walletName,
                            hdAccount.GetSpendableAmount(this.chain).ConfirmedAmount.ToUnit(MoneyUnit.XRC));
                }

                return this.Json(
                    ResultHelper.BuildResultResponse(result)
                );
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }
    }
}
