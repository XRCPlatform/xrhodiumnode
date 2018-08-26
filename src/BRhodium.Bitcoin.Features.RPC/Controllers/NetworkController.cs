﻿using System;
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

        public NetworkController(
            ILoggerFactory loggerFactory,
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
        }

        /// <summary>
        /// Attempts to add or remove a node from the addnode list. Or try a connection to a node once.
        /// </summary>
        /// <param name="node">The node ip:port</param>
        /// <param name="command">'add' to add a node to the list, 'remove' to remove a node from the list, 'onetry' to try a connection to the node once</param>
        /// <returns>True/False</returns>
        [ActionName("getdifficulty")]
        [ActionDescription("Result—the current difficulty.")]
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
    }
}
