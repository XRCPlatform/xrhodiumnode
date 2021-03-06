using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using BRhodium.Node.Base;
using BRhodium.Node.BlockPulling;
using BRhodium.Node.Builder;
using BRhodium.Node.Builder.Feature;
using BRhodium.Node.Connection;
using BRhodium.Bitcoin.Features.Notifications.Controllers;
using BRhodium.Bitcoin.Features.Notifications.Interfaces;

[assembly: InternalsVisibleTo("BRhodium.Bitcoin.Features.Notifications.Tests")]

namespace BRhodium.Bitcoin.Features.Notifications
{
    /// <summary>
    /// Feature enabling the broadcasting of blocks.
    /// </summary>
    public class BlockNotificationFeature : FullNodeFeature
    {
        private readonly IBlockNotification blockNotification;

        private readonly IConnectionManager connectionManager;

        private readonly LookaheadBlockPuller blockPuller;

        private readonly IChainState chainState;

        private readonly ConcurrentChain chain;

        private readonly ILoggerFactory loggerFactory;

        public BlockNotificationFeature(IBlockNotification blockNotification, IConnectionManager connectionManager,
            LookaheadBlockPuller blockPuller, IChainState chainState, ConcurrentChain chain, ILoggerFactory loggerFactory)
        {
            this.blockNotification = blockNotification;
            this.connectionManager = connectionManager;
            this.blockPuller = blockPuller;
            this.chainState = chainState;
            this.chain = chain;
            this.loggerFactory = loggerFactory;
        }

        public override void Initialize()
        {
            var connectionParameters = this.connectionManager.Parameters;
            connectionParameters.TemplateBehaviors.Add(new BlockPullerBehavior(this.blockPuller, this.loggerFactory));

            this.blockNotification.Start();
            this.chainState.ConsensusTip = this.chain.Tip;
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            this.blockNotification.Stop();
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderBlockNotificationExtension
    {
        public static IFullNodeBuilder UseBlockNotification(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<BlockNotificationFeature>()
                .FeatureServices(services =>
                {
                    services.AddSingleton<IBlockNotification, BlockNotification>();
                    services.AddSingleton<LookaheadBlockPuller>().AddSingleton<ILookaheadBlockPuller, LookaheadBlockPuller>(provider => provider.GetService<LookaheadBlockPuller>());
                    services.AddSingleton<NotificationsController>();
                });
            });

            return fullNodeBuilder;
        }
    }
}
