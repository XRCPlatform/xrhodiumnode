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
    /// RPC methods which support transaction functionality
    /// </summary>
    /// <seealso cref="BRhodium.Node.Controllers.FeatureController" />
    [Controller]
    public class UtilRPCController : FeatureController
    {
        private readonly ILogger logger;

        public UtilRPCController(
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
        /// Estimates the approximate fee per kilobyte needed for a transaction to begin confirmation within conf_target blocks if possible and return the number of blocks for which the estimate is valid.Uses virtual transaction size as defined in BIP 141 (witness data is discounted).
        /// </summary>
        /// <param name="nblocks">Confirmation target in blocks (1 - 1008)</param>
        /// <param name="estimate_mode">The fee estimate mode. Whether to return a more conservative estimate which also satisfies a longer history.A conservative estimate potentially returns a higher feerate and is more likely to be sufficient for the desired target, but is not as responsive to short term drops in the prevailing fee market.  Must be one of: "UNSET" (defaults to CONSERVATIVE), "ECONOMICAL", "CONSERVATIVE"</param>
        /// <returns>Return model EstimateSmartFee</returns>
        [ActionName("estimatesmartfee")]
        [ActionDescription("Estimates the approximate fee per kilobyte needed for a transaction to begin confirmation within conf_target blocks if possible and return the number of blocks for which the estimate is valid.Uses virtual transaction size as defined in BIP 141 (witness data is discounted).")]
        public IActionResult EstimateSmartFee(int nblocks, string estimate_mode)
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
        /// The private key to sign the message with.
        /// </summary>
        /// <param name="privKey">The private key to sign the message with.</param>
        /// <param name="message">The message to create a signature of.</param>
        /// <returns>The signature of the message encoded in base 64</returns>
        [ActionName("signmessagewithprivkey")]
        [ActionDescription("Sign a message with the private key of an address")]
        public IActionResult SignMessageWithPrivKey(string privKey, string message)
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
        /// Return information about the given bitcoin address.
        /// </summary>
        /// <param name="address">The bitcoin address to validate</param>
        /// <returns>Return Validate Address model</returns>
        [ActionName("validateaddress")]
        [ActionDescription("Return information about the given bitcoin address.")]
        public IActionResult ValidateAddress(string address)
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
        /// Verify a signed message
        /// </summary>
        /// <param name="address">The bitcoin address to use for the signature.</param>
        /// <param name="signature">The signature provided by the signer in base 64 encoding (see signmessage).</param>
        /// <param name="message">The message that was signed.</param>
        /// <returns>Return true/false</returns>
        [ActionName("verifymessage")]
        [ActionDescription("Verify a signed message")]
        public IActionResult VerifyMessage(string address, string signature, string message)
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
