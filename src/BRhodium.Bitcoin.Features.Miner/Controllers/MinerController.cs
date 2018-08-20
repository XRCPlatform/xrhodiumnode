using System;
using System.Linq;
using System.Net;
using System.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using BRhodium.Bitcoin.Features.Miner.Interfaces;
using BRhodium.Bitcoin.Features.Miner.Models;
using BRhodium.Bitcoin.Features.Wallet.Interfaces;
using BRhodium.Node.Utilities;
using BRhodium.Node.Utilities.JsonErrors;
using BRhodium.Node;

namespace BRhodium.Bitcoin.Features.Miner.Controllers
{
    /// <summary>
    /// Controller providing operations on mining feature.
    /// </summary>
    [Route("api/[controller]")]
    public class MinerController : Controller
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Full Node.</summary>
        private readonly IFullNode fullNode;

        /// <summary>The wallet manager.</summary>
        private readonly IWalletManager walletManager;

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="fullNode">Full Node.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the node.</param>
        /// <param name="walletManager">The wallet manager.</param>
        public MinerController(IFullNode fullNode, ILoggerFactory loggerFactory, IWalletManager walletManager)
        {
            Guard.NotNull(fullNode, nameof(fullNode));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));

            this.fullNode = fullNode;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.walletManager = walletManager;
        }
    }
}
