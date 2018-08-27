using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Linq;
using BRhodium.Node;
using BRhodium.Node.Base;
using BRhodium.Node.Configuration;
using BRhodium.Node.Connection;
using BRhodium.Node.Controllers;
using BRhodium.Node.P2P.Peer;
using BRhodium.Node.Utilities.JsonContract;
using BRhodium.Node.Utilities.JsonErrors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using BRhodium.Node.P2P;
using BRhodium.Node.Utilities;

namespace BRhodium.Bitcoin.Features.RPC.Controllers
{
    /// <summary>
    /// Network RPCs method
    /// </summary>
    /// <seealso cref="BRhodium.Node.Controllers.FeatureController" />
    public class NetworkController : FeatureController
    {

        /// <summary>
        /// Instance logger
        /// </summary>
        private readonly ILogger logger;

        /// <summary>Functionality of date and time.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>
        /// The peer address manager
        /// </summary>
        private readonly IPeerAddressManager peerAddressManager;

        public NetworkController(
            ILoggerFactory loggerFactory,
            IPeerAddressManager peerAddressManager,
            IDateTimeProvider dateTimeProvider,
            IFullNode fullNode = null,
            NodeSettings nodeSettings = null,
            Network network = null,
            ConcurrentChain chain = null,
            IChainState chainState = null,
            IConnectionManager connectionManager = null)
            : base(
                  fullNode: fullNode,
                  nodeSettings: nodeSettings,
                  network: network,
                  chain: chain,
                  chainState: chainState,
                  connectionManager: connectionManager)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.peerAddressManager = peerAddressManager;
            this.dateTimeProvider = dateTimeProvider;
        }

        /// <summary>
        /// Attempts to add or remove a node from the addnode list. Or try a connection to a node once.
        /// </summary>
        /// <param name="node">The node ip:port</param>
        /// <param name="command">'add' to add a node to the list, 'remove' to remove a node from the list, 'onetry' to try a connection to the node once</param>
        /// <returns>True/Error</returns>
        [ActionName("addnode")]
        [ActionDescription("Attempts to add or remove a node from the addnode list. Or try a connection to a node once.")]
        public IActionResult AddNode(string node, string command)
        {
            try
            {
                if (string.IsNullOrEmpty(node))
                {
                    throw new ArgumentNullException("node");
                }
                if (string.IsNullOrEmpty(command))
                {
                    throw new ArgumentNullException("command");
                }

                var nodeParam = node.Split(':');
                if (nodeParam.Length < 2)
                {
                    throw new ArgumentNullException("node");
                }

                var endPoint = new IPEndPoint(IPAddress.Parse(nodeParam[0]), int.Parse(nodeParam[1]));

                switch (command)
                {
                    case "add":
                        this.ConnectionManager.AddNodeAddress(endPoint);
                        break;
                    case "remove":
                        this.ConnectionManager.RemoveNodeAddress(endPoint);
                        break;
                    case "onetry":
                        this.ConnectionManager.ConnectAsync(endPoint).GetAwaiter().GetResult();
                        break;
                    default:
                        return this.Json(ResultHelper.BuildResultResponse(false));
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
        /// Returns data about each connected network node as a json array of objects.
        /// </summary>
        /// <returns>List of connected peer nodes as <see cref="PeerNodeModel"/>.</returns>
        [ActionName("getpeerinfo")]
        [ActionDescription("Returns data about each connected network node as a json array of objects.")]
        public IActionResult GetPeerInfo()
        {
            try
            {
                var peerList = new List<PeerNodeModel>();

                var connectedPeers = this.ConnectionManager.ConnectedPeers;
                List<INetworkPeer> peers = connectedPeers.ToList();

                foreach (INetworkPeer peer in peers)
                {
                    if ((peer != null) && (peer.RemoteSocketAddress != null))
                    {
                        var peerNode = new PeerNodeModel
                        {
                            Id = peers.IndexOf(peer),
                            Address = peer.RemoteSocketEndpoint.ToString()
                        };

                        if (peer.MyVersion != null)
                        {
                            peerNode.LocalAddress = peer.MyVersion.AddressReceiver?.ToString();
                            peerNode.Services = ((ulong)peer.MyVersion.Services).ToString("X");
                            peerNode.Version = (uint)peer.MyVersion.Version;
                            peerNode.SubVersion = peer.MyVersion.UserAgent;
                            peerNode.StartingHeight = peer.MyVersion.StartHeight;
                        }

                        var connectionManagerBehavior = peer.Behavior<ConnectionManagerBehavior>();
                        if (connectionManagerBehavior != null)
                        {
                            peerNode.Inbound = connectionManagerBehavior.Inbound;
                            peerNode.IsWhiteListed = connectionManagerBehavior.Whitelisted;
                        }

                        if (peer.TimeOffset != null)
                        {
                            peerNode.TimeOffset = peer.TimeOffset.Value.Seconds;
                        }

                        peerList.Add(peerNode);
                    }
                }

                return this.Json(ResultHelper.BuildResultResponse(peerList));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Clear all banned IPs.
        /// </summary>
        /// <returns>True/Error</returns>
        [ActionName("clearbanned")]
        [ActionDescription("Result—the current difficulty.")]
        public IActionResult ClearBanned()
        {
            try
            {
                foreach (var peer in this.peerAddressManager.Peers)
                {
                    peer.BanUntil = null;
                    peer.BanReason = null;
                    peer.BanTimeStamp = null;
                    peer.BanScore = null;
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
        /// Immediately disconnects from the specified peer node.
        /// 
        /// Strictly one out of 'address' and 'nodeid' can be provided to identify the node.
        ///
        /// To disconnect by nodeid, either set 'address' to the empty string, or call using the named 'nodeid' argument only.
        /// </summary>
        /// <param name="address">The IP address:port of the node</param>
        /// <param name="nodeId">The node ID (see getpeerinfo for node IDs)</param>
        /// <returns>True/Error</returns>
        [ActionName("disconnectnode")]
        [ActionDescription("Immediately disconnects from the specified peer node.")]
        public IActionResult DisconnectNode(string address, int? nodeId)
        {
            try
            {
                if ((string.IsNullOrEmpty(address)) && (!nodeId.HasValue))
                {
                    throw new ArgumentNullException("address");
                }

                if (!string.IsNullOrEmpty(address))
                {
                    var nodeParam = address.Split(':');
                    if (nodeParam.Length < 2)
                    {
                        throw new ArgumentNullException("node");
                    }

                    var endPoint = new IPEndPoint(IPAddress.Parse(nodeParam[0]), int.Parse(nodeParam[1]));

                    var node = this.ConnectionManager.FindNodeByEndpoint(endPoint);

                    if (node != null)
                    {
                        node.Disconnect("RPC DisconnectNode call");
                    }
                }
                else
                {
                    var peer = this.ConnectionManager.ConnectedPeers.ToList();
                    
                    if ((peer.Count > 0) && (peer.Count < nodeId.Value))
                    {
                        peer[nodeId.Value].Disconnect("RPC DisconnectNode call");
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
        /// List all banned IPs/Subnets.
        /// </summary>
        /// <returns>True/Error</returns>
        [ActionName("listbanned")]
        [ActionDescription("List all banned IPs/Subnets.")]
        public IActionResult ListBanned()
        {
            try
            {
                var bannedList = new List<string>();

                var banned = this.peerAddressManager.Peers.Where(a => a.BanUntil.HasValue && a.BanUntil.Value > this.dateTimeProvider.GetUtcNow());

                foreach (var item in banned)
                {
                    bannedList.Add(item.Endpoint.ToString());
                }

                return this.Json(ResultHelper.BuildResultResponse(bannedList));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        //getaddednodeinfo 
        //getconnectioncount
        //getnettotals
        //getnetworkinfo
        //ping
        //setban
        //setnetworkactive 

    }
}
