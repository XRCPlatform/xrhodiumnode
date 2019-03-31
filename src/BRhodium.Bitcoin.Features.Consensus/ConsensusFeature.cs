using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using BRhodium.Node.Base;
using BRhodium.Node.Base.Deployments;
using BRhodium.Node.BlockPulling;
using BRhodium.Node.Builder;
using BRhodium.Node.Builder.Feature;
using BRhodium.Node.Configuration;
using BRhodium.Node.Configuration.Logging;
using BRhodium.Node.Connection;
using BRhodium.Bitcoin.Features.Consensus.CoinViews;
using BRhodium.Bitcoin.Features.Consensus.Interfaces;
using BRhodium.Bitcoin.Features.Consensus.Rules;
using BRhodium.Bitcoin.Features.Consensus.Rules.CommonRules;
using BRhodium.Node.Interfaces;
using BRhodium.Node.P2P.Protocol.Payloads;
using BRhodium.Node.Utilities;
using BRhodium.Node.Signals;

[assembly: InternalsVisibleTo("BRhodium.Bitcoin.Features.Consensus.Tests")]

namespace BRhodium.Bitcoin.Features.Consensus
{
    public class ConsensusFeature : FullNodeFeature, INodeStats
    {
        private readonly DBreezeCoinView dBreezeCoinView;

        private readonly LookaheadBlockPuller blockPuller;

        private readonly CoinView coinView;

        private readonly IChainState chainState;

        private readonly IConnectionManager connectionManager;

        private readonly Signals signals;

        /// <summary>Manager of the longest fully validated chain of blocks.</summary>
        private readonly IConsensusLoop consensusLoop;

        private readonly NodeDeployments nodeDeployments;

        private readonly IRuleRegistration ruleRegistration;

        private readonly NodeSettings nodeSettings;

        private readonly ConsensusSettings consensusSettings;

        private readonly IConsensusRules consensusRules;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Logger factory to create loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Consensus statistics logger.</summary>
        private readonly ConsensusStats consensusStats;

        public ConsensusFeature(
            DBreezeCoinView dBreezeCoinView,
            Network network,
            LookaheadBlockPuller blockPuller,
            CoinView coinView,
            IChainState chainState,
            IConnectionManager connectionManager,
            Signals signals,
            IConsensusLoop consensusLoop,
            NodeDeployments nodeDeployments,
            ILoggerFactory loggerFactory,
            ConsensusStats consensusStats,
            IRuleRegistration ruleRegistration,
            IConsensusRules consensusRules,
            NodeSettings nodeSettings,
            ConsensusSettings consensusSettings)
        {
            this.dBreezeCoinView = dBreezeCoinView;
            this.blockPuller = blockPuller;
            this.coinView = coinView;
            this.chainState = chainState;
            this.connectionManager = connectionManager;
            this.signals = signals;
            this.consensusLoop = consensusLoop;
            this.nodeDeployments = nodeDeployments;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.loggerFactory = loggerFactory;
            this.consensusStats = consensusStats;
            this.ruleRegistration = ruleRegistration;
            this.nodeSettings = nodeSettings;
            this.consensusSettings = consensusSettings;
            this.consensusRules = consensusRules;

            this.chainState.MaxReorgLength = network.Consensus.Option<PowConsensusOptions>().MaxReorgLength;
        }

        /// <inheritdoc />
        public void AddNodeStats(StringBuilder benchLogs)
        {
            if (this.chainState?.ConsensusTip != null)
            {
                benchLogs.AppendLine("Consensus.Height: ".PadRight(LoggingConfiguration.ColumnLength + 1) +
                                     this.chainState.ConsensusTip.Height.ToString().PadRight(8) +
                                     " Consensus.Hash: ".PadRight(LoggingConfiguration.ColumnLength - 1) +
                                     this.chainState.ConsensusTip.HashBlock);
            }
        }

        /// <inheritdoc />
        public override void LoadConfiguration()
        {
            this.consensusSettings.Load(nodeSettings);
        }

        /// <inheritdoc />
        public override void Initialize()
        {
            this.dBreezeCoinView.InitializeAsync().GetAwaiter().GetResult();
            this.consensusLoop.StartAsync().GetAwaiter().GetResult();

            this.chainState.ConsensusTip = this.consensusLoop.Tip;
            this.connectionManager.Parameters.TemplateBehaviors.Add(new BlockPullerBehavior(this.blockPuller, this.loggerFactory));

            var flags = this.nodeDeployments.GetFlags(this.consensusLoop.Tip);
            if (flags.ScriptFlags.HasFlag(ScriptVerify.Witness))
                this.connectionManager.AddDiscoveredNodesRequirement(NetworkPeerServices.NODE_WITNESS);

            this.signals.SubscribeForBlocks(this.consensusStats);

            this.consensusRules.Register(this.ruleRegistration);
        }

        /// <summary>
        /// Prints command-line help.
        /// </summary>
        /// <param name="network">The network to extract values from.</param>
        public static void PrintHelp(Network network)
        {
            ConsensusSettings.PrintHelp(network);
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            ConsensusSettings.BuildDefaultConfigurationFile(builder, network);
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            // First, we need to wait for the consensus loop to finish.
            // Only then we can flush our coinview safely.
            // Otherwise there is a race condition and a new block
            // may come from the consensus at wrong time.
            this.consensusLoop.Stop();

            var cache = this.coinView as CachedCoinView;
            if (cache != null)
            {
                this.logger.LogInformation("Flushing Cache CoinView...");
                cache.FlushAsync().GetAwaiter().GetResult();
                cache.Dispose();
            }
           
            this.dBreezeCoinView.Dispose();
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderConsensusExtension
    {
        public static IFullNodeBuilder UsePowConsensus(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<ConsensusFeature>("consensus");
            LoggingConfiguration.RegisterFeatureClass<ConsensusStats>("bench");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<ConsensusFeature>()
                .FeatureServices(services =>
                {
                    
                    fullNodeBuilder.Network.Consensus.Options = new PowConsensusOptions();
                    if (fullNodeBuilder.Network.Name == "BRhodiumRegTest")
                    {
                        fullNodeBuilder.Network.Consensus.Options = new PowConsensusOptions().RegTestPowConsensusOptions();
                    }
                    else if (fullNodeBuilder.Network.Name != Network.BRhodiumBaseName)
                    {
                        fullNodeBuilder.Network.Consensus.Options = new PowConsensusOptions().TestPowConsensusOptions();
                    }
                    services.AddSingleton<ICheckpoints, Checkpoints>();
                    services.AddSingleton<NBitcoin.Consensus.ConsensusOptions, PowConsensusOptions>();
                    services.AddSingleton<DBreezeCoinView>();
                    services.AddSingleton<CoinView, CachedCoinView>();
                    services.AddSingleton<LookaheadBlockPuller>().AddSingleton<ILookaheadBlockPuller, LookaheadBlockPuller>(provider => provider.GetService<LookaheadBlockPuller>()); ;
                    services.AddSingleton<IConsensusLoop, ConsensusLoop>();
                    services.AddSingleton<ConsensusManager>().AddSingleton<INetworkDifficulty, ConsensusManager>();
                    services.AddSingleton<IInitialBlockDownloadState, InitialBlockDownloadState>();
                    services.AddSingleton<IGetUnspentTransaction, ConsensusManager>();
                    services.AddSingleton<ConsensusController>();
                    services.AddSingleton<ConsensusStats>();
                    services.AddSingleton<ConsensusSettings>();
                    services.AddSingleton<IConsensusRules, PowConsensusRules>();
                    services.AddSingleton<IRuleRegistration, PowConsensusRulesRegistration>();
                });
            });

            return fullNodeBuilder;
        }

        public class PowConsensusRulesRegistration : IRuleRegistration
        {
            public IEnumerable<ConsensusRule> GetRules()
            {
                return new List<ConsensusRule>
                {
                    new BlockHeaderRule(),

                    // rules that are inside the method CheckBlockHeader
                    new CalculateWorkRule(),

                    // rules that are inside the method ContextualCheckBlockHeader
                    new CheckpointsRule(),
                    new AssumeValidRule(),
                    new BlockHeaderPowContextualRule(),

                    // rules that are inside the method ContextualCheckBlock
                    new TransactionLocktimeActivationRule(), // implements BIP113
                    new CoinbaseHeightActivationRule(), // implements BIP34
                    new WitnessCommitmentsRule(), // BIP141, BIP144
                    new BlockSizeRule(),

                    // rules that are inside the method CheckBlock
                    new BlockMerkleRootRule(),
                    new EnsureCoinbaseRule(),
                    new CheckPowTransactionRule(),
                    new CheckSigOpsRule(),

                    // rules that require the store to be loaded (coinview)
                    new LoadCoinviewRule(),
                    new TransactionDuplicationActivationRule(), // implements BIP30
                    new PowCoinViewRule() // implements BIP68, MaxSigOps and BlockReward calculation
                };
            }
        }
    }
}
