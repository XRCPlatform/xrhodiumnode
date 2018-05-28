using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using BRhodium.Bitcoin.Controllers;
using BRhodium.Bitcoin.Features.RPC;
using BRhodium.Bitcoin.Features.Wallet.Interfaces;
using BRhodium.Bitcoin.Features.Wallet.Models;
using BRhodium.Bitcoin.Utilities.JsonContract;
using BRhodium.Bitcoin.Utilities.JsonErrors;

namespace BRhodium.Bitcoin.Features.Wallet
{
    public class WalletRPCController : FeatureController
    {
        public WalletRPCController(IServiceProvider serviceProvider, IWalletManager walletManager, ILoggerFactory loggerFactory, IFullNode fullNode)
        {
            this.walletManager = walletManager;
            this.serviceProvider = serviceProvider;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.Network = fullNode.Network;
        }

        internal IServiceProvider serviceProvider;

        public IWalletManager walletManager { get; set; }

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        [ActionName("sendtoaddress")]
        [ActionDescription("Sends money to a bitcoin address.")]
        public uint256 SendToAddress(BitcoinAddress bitcoinAddress, Money amount)
        {
            var account = this.GetAccount();
            return uint256.Zero;
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


        [ActionName("dumpprivkey")]
        [ActionDescription("Gets the current consensus tip height.")]
        public IActionResult DumpPrivKey(string address)
        {
            try
            {
                var mywallet = this.walletManager.GetWallet("rhodium.genesis");

                Key privateKey = HdOperations.DecryptSeed(mywallet.EncryptedSeed, "thisisourrootwallet", this.Network);
                var secret = new BitcoinSecret(privateKey, this.Network);
                var stringPrivateKey = secret.ToString();
                return this.Json(ResultHelper.BuildResultResponse(stringPrivateKey));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("getbalance")]
        [ActionDescription("Gets the current consensus tip height.")]
        public IActionResult GetBalance(string address)
        {
            try
            {
                WalletBalanceModel model = new WalletBalanceModel();

                var accounts = this.walletManager.GetAccounts("rhodium.genesis").ToList();
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

                //P2WPKH
                //if (BitcoinWitPubKeyAddress.IsValid(address, ref this.Network))
                //{
                //    res.IsValid = true;
                //}
                // P2WSH
                //if (BitcoinWitScriptAddress.IsValid(address, ref this.Network))
                //{
                //    res.IsValid = true;
                //}
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

        [ActionName("getaccount")]
        [ActionDescription("")]
        public IActionResult GetAccount(string address)
        {
            try
            {
                string walletName = this.walletManager.GetWalletsNames().FirstOrDefault();

                return this.Json(ResultHelper.BuildResultResponse(walletName));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

    }
}
