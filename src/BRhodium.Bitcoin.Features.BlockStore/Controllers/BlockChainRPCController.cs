using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using BRhodium.Node;
using BRhodium.Node.Base;
using BRhodium.Node.Configuration;
using BRhodium.Node.Controllers;
using BRhodium.Node.Utilities.JsonContract;
using BRhodium.Node.Utilities.JsonErrors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace BRhodium.Bitcoin.Features.BlockStore.Controllers
{
    /// <summary>
    /// BlockChain RPCs method
    /// </summary>
    /// <seealso cref="BRhodium.Node.Controllers.FeatureController" />
    [Controller]
    public class BlockChainRPCController : FeatureController
    {
        /// <summary>
        /// Instance logger
        /// </summary>
        private readonly ILogger logger;

        public BlockChainRPCController(
            ILoggerFactory loggerFactory,
            IFullNode fullNode = null,
            NodeSettings nodeSettings = null,
            Network network = null,
            ConcurrentChain chain = null,
            IChainState chainState = null,
            Node.Connection.IConnectionManager connectionManager = null)
            : base(
                  fullNode: fullNode,
                  nodeSettings: nodeSettings,
                  network: network,
                  chain: chain,
                  chainState: chainState,
                  connectionManager: connectionManager)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

        }

        /// <summary>
        /// Gets the block.
        /// If verbosity is 0, returns a string that is serialized, hex-encoded data for block 'hash'.
        /// If verbosity is 1, returns an Object with information about block<hash>.
        /// If verbosity is 2, returns an Object with information about block<hash> and information about each transaction. 
        /// </summary>
        /// <param name="verbosity">The verbosity.</param>
        /// <returns>Return data based on verbosity</returns>
        [ActionName("getblock")]
        [ActionDescription("Gets the block.")]
        public IActionResult GetBlock(int verbosity)
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
        /// Gets the best blockhash.
        /// </summary>
        /// <param name="hash">The hash.</param>
        /// <param name="verbosity">The verbosity.</param>
        /// <returns></returns>
        [ActionName("getbestblockhash")]
        [ActionDescription("Gets the block.")]
        public IActionResult GetBestBlockhash(string hash, int verbosity)
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
