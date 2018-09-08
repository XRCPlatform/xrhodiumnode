using System;
using System.Net;
using BRhodium.Bitcoin.Features.Consensus.Interfaces;
using BRhodium.Node;
using BRhodium.Node.Base;
using BRhodium.Node.Configuration;
using BRhodium.Node.Connection;
using BRhodium.Node.Controllers;
using BRhodium.Node.Utilities.JsonContract;
using BRhodium.Node.Utilities.JsonErrors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace BRhodium.Bitcoin.Features.Wallet.Controllers
{
    /// <summary>
    /// Class offering method working with Raw Transactions
    /// </summary>
    /// <seealso cref="BRhodium.Node.Controllers.FeatureController" />
    [Controller]
    public class TransactionRPCController : FeatureController
    {
        private readonly ILogger logger;

        public TransactionRPCController(
            ILoggerFactory loggerFactory,
            IFullNode fullNode,
            NodeSettings nodeSettings,
            Network network,
            ConcurrentChain chain,
            IConnectionManager connectionManager,
            IChainState chainState = null,
            IConsensusLoop consensusLoop = null) : base(fullNode, nodeSettings, network, chain, chainState, connectionManager)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <summary>
        /// Combine multiple partially signed transactions into one transaction. The combined transaction may be another partially signed transaction or a fully signed transaction.
        /// </summary>
        /// <param name="txs">A json array of hex strings of partially signed transactions.</param>
        /// <returns>The hex-encoded raw transaction with signature(s)</returns>
        [ActionName("combinerawtransaction")]
        [ActionDescription("Combine multiple partially signed transactions into one transaction. The combined transaction may be another partially signed transaction or a fully signed transaction.")]
        public IActionResult CombineRawTransaction(string txs)
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


        /// <summary>
        /// Create a transaction spending the given inputs and creating new outputs. Outputs can be addresses or data. Returns hex - encoded raw transaction. Note that the transaction's inputs are not signed, and it is not stored in the wallet or transmitted to the network.
        /// </summary>
        /// <param name="inputs">A json array of json objects</param>
        /// <param name="outputs">A json object with outputs</param>
        /// <returns>Hex string of the transaction</returns>
        [ActionName("createrawtransaction")]
        [ActionDescription("Create a transaction spending the given inputs and creating new outputs. Outputs can be addresses or data. Returns hex - encoded raw transaction. Note that the transaction's inputs are not signed, and it is not stored in the wallet or transmitted to the network.")]
        public IActionResult CreateRawTransaction(string inputs, string outputs)
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

        /// <summary>
        ///  Return a JSON object representing the serialized, hex-encoded transaction.
        /// </summary>
        /// <param name="hex">The transaction hex string</param>
        /// <returns>Result transaction object</returns>
        [ActionName("decoderawtransaction")]
        [ActionDescription("Return a JSON object representing the serialized, hex-encoded transaction.")]
        public IActionResult DecodeRawTransaction(string hex)
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

        /// <summary>
        /// Decode a hex-encoded script.
        /// </summary>
        /// <param name="hex">The hex encoded script.</param>
        /// <returns>Result is decoded hex</returns>
        [ActionName("decodescript")]
        [ActionDescription("Decode a hex-encoded script.")]
        public IActionResult DecodeScript(string hex)
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

        /// <summary>
        /// Add inputs to a transaction until it has enough in value to meet its out value. This will not modify existing inputs, and will add at most one change output to the outputs. No existing outputs will be modified unless \"subtractFeeFromOutputs\" is specified. Note that inputs which were signed may need to be resigned after completion since in/ outputs have been added. The inputs added will not be signed, use signrawtransaction for that. Note that all existing inputs must have their previous output transaction be in the wallet. Note that all inputs selected must be of standard form and P2SH scripts must be in the wallet using importaddress or addmultisigaddress(to calculate fees). You can see whether this is the case by checking the \"solvable\" field in the listunspent output. Only pay-to-pubkey, multisig, and P2SH versions thereof are currently supported for watch-only
        /// </summary>
        /// <param name="hex">The hex string of the raw transaction</param>
        /// <param name="options">The options</param>
        /// <returns></returns>
        [ActionName("fundrawtransaction")]
        [ActionDescription("Add inputs to a transaction until it has enough in value to meet its out value. This will not modify existing inputs, and will add at most one change output to the outputs. No existing outputs will be modified unless \"subtractFeeFromOutputs\" is specified. Note that inputs which were signed may need to be resigned after completion since in/ outputs have been added. The inputs added will not be signed, use signrawtransaction for that. Note that all existing inputs must have their previous output transaction be in the wallet. Note that all inputs selected must be of standard form and P2SH scripts must be in the wallet using importaddress or addmultisigaddress(to calculate fees). You can see whether this is the case by checking the \"solvable\" field in the listunspent output. Only pay-to-pubkey, multisig, and P2SH versions thereof are currently supported for watch-only")]
        public IActionResult FundRawTransaction(string hex, string options)
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

        /// <summary>
        /// Return the raw transaction data. If verbose is 'true', returns an Object with information about 'txid'. If verbose is 'false' or omitted, returns a string that is serialized, hex - encoded data for 'txid'.
        /// </summary>
        /// <param name="txid">The transaction id</param>
        /// <param name="verbose">If false, return a string, otherwise return a json object</param>
        /// <param name="blockhash">The block in which to look for the transaction</param>
        /// <returns>The serialized, hex-encoded data for 'txid'</returns>
        [ActionName("getrawtransaction")]
        [ActionDescription("Return the raw transaction data. If verbose is 'true', returns an Object with information about 'txid'. If verbose is 'false' or omitted, returns a string that is serialized, hex - encoded data for 'txid'.")]
        public IActionResult GetRawTransaction(string txid, bool verbose, string blockhash)
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

        /// <summary>
        /// Submits raw transaction (serialized, hex-encoded) to local node and network. Also see createrawtransaction and signrawtransaction calls.
        /// </summary>
        /// <param name="hex">The hex string of the raw transaction</param>
        /// <param name="allowhighfees">Allow high fees</param>
        /// <returns>The transaction hash in hex</returns>
        [ActionName("sendrawtransaction")]
        [ActionDescription("Submits raw transaction (serialized, hex-encoded) to local node and network. Also see createrawtransaction and signrawtransaction calls.")]
        public IActionResult SendRawTransaction(string hex, bool allowhighfees)
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

        /// <summary>
        /// Sign inputs for raw transaction (serialized, hex-encoded). The second optional argument(may be null) is an array of previous transaction outputs 
        /// that this transaction depends on but may not yet be in the block chain. The third optional argument(may be null) is an array of base58 - encoded 
        /// private keys that, if given, will be the only keys used to sign the transaction.
        /// </summary>
        /// <param name="hexstring">The transaction hex string</param>
        /// <param name="prevtxs">An json array of previous dependent transaction outputs</param>
        /// <param name="privkeys">A json array of base58-encoded private keys for signing</param>
        /// <param name="sighashtype">The signature hash type. Must be one of "ALL", "NONE", "SINGLE", "ALL|ANYONECANPAY", "NONE|ANYONECANPAY", "SINGLE|ANYONECANPAY"</param>
        /// <returns>Result is sign object of transaction</returns>
        [ActionName("signrawtransaction")]
        [ActionDescription("Sign inputs for raw transaction (serialized, hex-encoded). The second optional argument(may be null) is an array of previous transaction outputs that this transaction depends on but may not yet be in the block chain. The third optional argument(may be null) is an array of base58 - encoded private keys that, if given, will be the only keys used to sign the transaction.")]
        public IActionResult SignRawTransaction(string hexstring, string prevtxs, string privkeys, string sighashtype)
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
