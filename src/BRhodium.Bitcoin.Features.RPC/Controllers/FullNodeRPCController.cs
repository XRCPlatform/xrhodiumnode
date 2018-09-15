using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
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
using System.Collections.Generic;
using BRhodium.Node;
using System.Net.NetworkInformation;

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
        /// (DEPRECATED) Returns an object containing various state info regarding P2P networking. Please to use getblockchaininfo or getnetworkinfo or getwalletinfo or getmininginfo.
        /// </summary>
        /// <returns>(GetInfoModel) RPC format.</returns>
        [ActionName("getinfo")]
        [ActionDescription("Returns an object containing various state info regarding P2P networking. Please to use getblockchaininfo or getnetworkinfo or getwalletinfo or getmininginfo.")]
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
                    Errors = string.Empty
                };

                return this.Json(ResultHelper.BuildResultResponse(model));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Returns an object containing various state info regarding P2P networking.
        /// </summary>
        /// <returns>(GetNetworkInfoModel) Return object model with informations.</returns>
        [ActionName("getnetworkinfo")]
        [ActionDescription("Returns an object containing various state info regarding P2P networking.")]
        public IActionResult GetnetworkInfo()
        {
            try
            {
                var model = new GetNetworkInfoModel
                {
                    Version = this.FullNode?.Version?.ToUint() ?? 0,
                    SubVersion = this.FullNode?.Version?.ToString() ?? string.Empty,
                    ProtocolVersion = (uint)(this.Settings?.ProtocolVersion ?? NodeSettings.SupportedProtocolVersion),
                    LocalServices = "000000000000040d",
                    LocalRelay = true,
                    TimeOffset = this.ConnectionManager?.ConnectedPeers?.GetMedianTimeOffset() ?? 0,
                    Connections = this.ConnectionManager?.ConnectedPeers?.Count(),
                    NetworkActive = this.ConnectionManager?.IsActive,
                    Networks = new List<GetNetworkInfoNetworkModel>(),
                    IncrementalFee = 0,
                    RelayFee = this.Settings?.MinRelayTxFeeRate?.FeePerK?.ToUnit(MoneyUnit.BTR) ?? 0,
                    LocalAddresses = new List<GetNetworkInfoAddressModel>(),
                    Warning = string.Empty
                };

                var localEntry = Dns.GetHostEntry(Dns.GetHostName());
                if (localEntry != null && localEntry.AddressList.Count() > 0)
                {
                    foreach (IPAddress adr in localEntry.AddressList)
                    {
                        if (adr != null && adr.IsIPv4() == true)
                        {
                            var address = new GetNetworkInfoAddressModel();
                            address.Address = adr.ToString();
                            address.Port = this.FullNode.Network.DefaultPort;
                            model.LocalAddresses.Add(address);
                        }
                    }
                }

                var networkInterface = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var adapter in networkInterface)
                {
                    var network = new GetNetworkInfoNetworkModel();

                    if (adapter.Supports(NetworkInterfaceComponent.IPv4))
                    {
                        network.Name = "ipv4";
                        network.Reachable = adapter.IsReceiveOnly ? false : true;
                        model.Networks.Add(network);
                    }
                    if (adapter.Supports(NetworkInterfaceComponent.IPv6))
                    {
                        network.Name = "ipv6";
                        network.Reachable = adapter.IsReceiveOnly ? false : true;
                        model.Networks.Add(network);
                    }

                    model.Networks.Add(network);
                }

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
