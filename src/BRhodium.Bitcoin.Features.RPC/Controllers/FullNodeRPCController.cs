using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Newtonsoft.Json.Linq;
using BRhodium.Node.Base;
using BRhodium.Node.Configuration;
using BRhodium.Node.Controllers;
using BRhodium.Bitcoin.Features.Consensus.Interfaces;
using BRhodium.Bitcoin.Features.RPC.Models;
using BRhodium.Node.Interfaces;
using BRhodium.Node.Utilities;
using BRhodium.Node.Utilities.Extensions;
using BRhodium.Node.Utilities.JsonContract;
using BRhodium.Node.Utilities.JsonErrors;
using System.Net;
using BRhodium.Bitcoin.Features.BlockStore;
using System.Collections.Generic;
using BRhodium.Node;

namespace BRhodium.Bitcoin.Features.RPC.Controllers
{
    /// <summary>
    /// Full Node Controller RPCs method
    /// </summary>
    /// <seealso cref="BRhodium.Node.Controllers.FeatureController" />
    [Controller]
    public class FullNodeRPCController : FeatureController
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private readonly INetworkDifficulty networkDifficulty;

        /// <summary>Manager of the longest fully validated chain of blocks.</summary>
        private readonly IConsensusLoop consensusLoop;

        public FullNodeRPCController(
            ILoggerFactory loggerFactory,
            INetworkDifficulty networkDifficulty = null,
            IConsensusLoop consensusLoop = null,
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
            this.networkDifficulty = networkDifficulty;
            this.consensusLoop = consensusLoop;
        }

        /// <summary>
        /// Gets the information.
        /// </summary>
        /// <returns>GetInfoModel rpc format</returns>
        [ActionName("getinfo")]
        [ActionDescription("Gets general information about the full node.")]
        public IActionResult GetInfo()
        {
            try
            {
                var model = new GetInfoModel
                {
                    Version = this.FullNode?.Version?.ToUint() ?? 0,
                    ProtocolVersion = (uint)(this.Settings?.ProtocolVersion ?? NodeSettings.SupportedProtocolVersion),
                    Blocks = this.ChainState?.ConsensusTip?.Height ?? 0,
                    TimeOffset = this.ConnectionManager?.ConnectedPeers?.GetMedianTimeOffset() ?? 0,
                    Connections = this.ConnectionManager?.ConnectedPeers?.Count(),
                    Proxy = string.Empty,
                    Difficulty = this.GetNetworkDifficulty()?.Difficulty ?? 0,
                    Testnet = this.Network.IsTest(),
                    RelayFee = this.Settings?.MinRelayTxFeeRate?.FeePerK?.ToUnit(MoneyUnit.BTR) ?? 0,
                    Errors = string.Empty,

                    //TODO: Wallet related infos: walletversion, balance, keypNetwoololdest, keypoolsize, unlocked_until, paytxfee
                    WalletVersion = null,
                    Balance = null,
                    KeypoolOldest = null,
                    KeypoolSize = null,
                    UnlockedUntil = null,
                    PayTxFee = null
                };

                return this.Json(ResultHelper.BuildResultResponse(model));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        private Target GetNetworkDifficulty()
        {
            return this.networkDifficulty?.GetNetworkDifficulty();
        }
    }
}
