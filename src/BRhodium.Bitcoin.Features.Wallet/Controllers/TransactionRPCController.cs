using BRhodium.Bitcoin.Features.Consensus.Interfaces;
using BRhodium.Node;
using BRhodium.Node.Base;
using BRhodium.Node.Configuration;
using BRhodium.Node.Connection;
using BRhodium.Node.Controllers;
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
        private readonly ILoggerFactory loggerFactory;

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
            this.loggerFactory = loggerFactory;
        }
    }
}
