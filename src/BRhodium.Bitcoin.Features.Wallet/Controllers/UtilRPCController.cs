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
                if (string.IsNullOrEmpty(privKey))
                {
                    throw new ArgumentNullException("privKey");
                }
                if (string.IsNullOrEmpty(message))
                {
                    throw new ArgumentNullException("message");
                }

                var secret = this.Network.CreateBitcoinSecret(privKey);
                var signature = secret.PrivateKey.SignMessage(message);

                return this.Json(ResultHelper.BuildResultResponse(signature));
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
                if (string.IsNullOrEmpty(address))
                {
                    throw new ArgumentNullException("address");
                }
                if (string.IsNullOrEmpty(signature))
                {
                    throw new ArgumentNullException("signature");
                }
                if (string.IsNullOrEmpty(message))
                {
                    throw new ArgumentNullException("message");
                }

                var bAddress = (BitcoinPubKeyAddress)Network.Main.CreateBitcoinAddress(address);
                var result = bAddress.VerifyMessage(message, signature);

                return this.Json(ResultHelper.BuildResultResponse(result));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }
    }
}
