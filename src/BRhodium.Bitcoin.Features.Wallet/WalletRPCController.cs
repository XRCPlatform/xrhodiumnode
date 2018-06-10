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
using BRhodium.Bitcoin.Features.Wallet.Controllers;
using BRhodium.Bitcoin.Features.Consensus.Models;
using BRhodium.Bitcoin.Features.BlockStore;
using BRhodium.Bitcoin.Configuration;
using BRhodium.Bitcoin.Utilities;
using BRhodium.Bitcoin.Features.Consensus.Interfaces;
using NBitcoin.RPC;
using Newtonsoft.Json;

namespace BRhodium.Bitcoin.Features.Wallet
{
    public class WalletRPCController : FeatureController
    {
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

        public WalletRPCController(
            IServiceProvider serviceProvider, 
            IWalletManager walletManager, 
            ILoggerFactory loggerFactory, 
            IFullNode fullNode, 
            IBlockRepository blockRepository,
            NodeSettings nodeSettings,
            Network network,
            IConsensusLoop consensusLoop = null)
        {
            this.walletManager = walletManager;
            this.serviceProvider = serviceProvider;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.Network = fullNode.Network;
            this.FullNode = fullNode;
            this.ConsensusLoop = consensusLoop;

            this.loggerFactory = loggerFactory;
            this.nodeSettings = nodeSettings;
            this.blockRepository = blockRepository;
            this.network = network;
            this.blockStoreCache = new BlockStoreCache(this.blockRepository, DateTimeProvider.Default, this.loggerFactory, this.nodeSettings);
        }

        


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
        [ActionDescription("Gets private key of given wallet address")]
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
                string accountName = null;
                if (this.walletManager.ContainsWallets)
                {
                    string walletName = this.walletManager.GetWalletsNames().FirstOrDefault();
                    if (walletName != null)
                    {
                        var account = this.walletManager.GetAccounts(walletName).FirstOrDefault();
                        accountName = account.Name;
                    }
                }
                else {
                    throw new RPCException(RPCErrorCode.RPC_INVALID_ADDRESS_OR_KEY, "Wallet not initialized", null, false);
                }
            
                return this.Json(ResultHelper.BuildResultResponse(accountName));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }
        [ActionName("generatenewwallet")]
        public Mnemonic GenerateNewWallet(string walletName, string password)
        {
            var w = this.walletManager as WalletManager;
            return w.CreateWallet(password, walletName);
        }

        [ActionName("getwallet")]
        public HdAccount GetWallet(string walletName)
        {
            var w = this.walletManager;
            var wallet = w.GetWalletByName(walletName);
            return wallet.GetAccountsByCoinType(CoinType.BRhodium).ToArray().First();
        }

        [ActionName("sendmoney")]
        public Transaction SendMoney(string hdAcccountName, string walletName, string targetAddress, string password, decimal satoshi)
        {
            var transaction = this.FullNode.NodeService<IWalletTransactionHandler>() as WalletTransactionHandler;
            var w = this.walletManager as WalletManager;
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

        [ActionName("sendmany")]
        public uint256 Sendmany(string hdAcccountName, string toBitcoinAddresses,  int minconf, string password )
        {
            string walletName = this.walletManager.GetWalletsNames().FirstOrDefault();//get default account wallet name
            var transaction = this.FullNode.NodeService<IWalletTransactionHandler>() as WalletTransactionHandler;
            var w = this.walletManager as WalletManager;
            var walletReference = new WalletAccountReference(walletName, hdAcccountName);
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

        [ActionName("gettransaction")]
        [ActionDescription("Returns a wallet (only local transactions) transaction detail.")]
        public IActionResult GetTransaction(string[] args)
        {
            try
            {
                var reqTransactionId = uint256.Parse(args[0]);
                if (reqTransactionId == null)
                {
                    var response = new Utilities.JsonContract.ErrorModel();
                    response.Code = "-5";
                    response.Message = "Invalid or non-wallet transaction id";
                    return this.Json(ResultHelper.BuildResultResponse(response));
                }
                //UnspentOutputReference currentTransaction = null;
                //string walletName = this.walletManager.GetWalletsNames().FirstOrDefault();

                //foreach (var item in this.walletManager.GetSpendableTransactionsInWallet(walletName,0))
                //{
                //    if (item.Transaction.Id.Equals(reqTransactionId)) {
                //        currentTransaction = item;
                //        break;
                //    }
                //}
                //var x = this.ConsensusLoop.UTXOSet.FetchCoinsAsync(new uint256[1] { reqTransactionId }).GetAwaiter().GetResult();        
                Block block = null;
                ChainedHeader chainedHeader = null;
                var blockHash = this.blockRepository.GetTrxBlockIdAsync(reqTransactionId).GetAwaiter().GetResult(); //this brings block hash for given transaction
                if (blockHash != null) {
                    block = this.blockRepository.GetAsync(blockHash).GetAwaiter().GetResult();
                    chainedHeader = this.ConsensusLoop.Chain.GetBlock(blockHash);
                }
                
                var currentTransaction = this.blockRepository.GetTrxAsync(reqTransactionId).GetAwaiter().GetResult();
                if (currentTransaction == null)
                {
                    var response = new Utilities.JsonContract.ErrorModel();
                    response.Code = "-5";
                    response.Message = "Invalid or non-wallet transaction id";
                    return this.Json(ResultHelper.BuildResultResponse(response));
                }                

                var transactionResponse = new TransactionModel();
                var transactionHash = currentTransaction.GetHash();
                transactionResponse.NormTxId = string.Format("{0:x8}", transactionHash);
                transactionResponse.TxId = string.Format("{0:x8}", transactionHash);
                if (block != null && chainedHeader != null)
                {
                    transactionResponse.Confirmations = this.ConsensusLoop.Chain.Tip.Height - chainedHeader.Height; // ExtractBlockHeight(block.Transactions.First().Inputs.First().ScriptSig);
                    transactionResponse.BlockTime = block.Header.BlockTime.ToUnixTimeSeconds();
                }
                
                transactionResponse.BlockHash = string.Format("{0:x8}", blockHash);
               
                
                transactionResponse.Time = currentTransaction.Time;
                transactionResponse.TimeReceived = currentTransaction.Time;
                //transactionResponse.BlockIndex = currentTransaction.Inputs.Transaction.;//The index of the transaction in the block that includes it
  
                transactionResponse.Details = new List<TransactionDetail>();
                foreach (var item in currentTransaction.Outputs)
                {
                    var detail = new TransactionDetail();
                    var address = this.walletManager.GetAddressByPubKeyHash(item.ScriptPubKey);

                    if (address == null)
                    {
                        var response = new Utilities.JsonContract.ErrorModel();
                        response.Code = "-5";
                        response.Message = "Invalid or non-wallet transaction id";
                        return this.Json(ResultHelper.BuildResultResponse(response));
                    }

                    detail.Account = address.Address;
                    detail.Address = address.Address;

                    if (transactionResponse.Confirmations < 10)
                    {
                        detail.Category = "receive";
                    }
                    else {
                        detail.Category = "generate";
                    }
                    
                    
                    detail.Amount = (double)item.Value.Satoshi / 100000000;
                    transactionResponse.Details.Add(detail);
                }

                
                transactionResponse.Hex = currentTransaction.ToHex();


                var json = ResultHelper.BuildResultResponse(transactionResponse);
                return this.Json(json);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        private int ExtractBlockHeight(Script scriptSig)
        {
            int retval = 0;
            foreach (var item in scriptSig.ToOps())
            {
                //item.PushData.
                break;
            }
            return retval;
        }
    }
}
