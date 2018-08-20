using System;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using BRhodium.Node.Base;
using BRhodium.Node.Builder;
using BRhodium.Node.Builder.Feature;
using BRhodium.Node.Configuration;
using BRhodium.Node.Configuration.Logging;
using BRhodium.Bitcoin.Features.BlockStore;
using BRhodium.Bitcoin.Features.MemoryPool;
using BRhodium.Bitcoin.Features.Miner.Controllers;
using BRhodium.Bitcoin.Features.Miner.Interfaces;
using BRhodium.Bitcoin.Features.RPC;
using BRhodium.Bitcoin.Features.Wallet;
using BRhodium.Node.Mining;

namespace BRhodium.Bitcoin.Features.Miner
{
    /// <summary>
    /// Provides an ability to mine.
    /// </summary>
    public class MiningFeature : FullNodeFeature
    {
        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        private readonly Network network;

        /// <summary>Settings relevant to mining or staking.</summary>
        private readonly MinerSettings minerSettings;

        /// <summary>Settings relevant to node.</summary>
        private readonly NodeSettings nodeSettings;

        /// <summary>POW miner.</summary>
        private readonly IPowMining powMining;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>State of time synchronization feature that stores collected data samples.</summary>
        private readonly ITimeSyncBehaviorState timeSyncBehaviorState;

        /// <summary>
        /// Initializes the instance of the object.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="minerSettings">Settings relevant to mining or staking.</param>
        /// <param name="nodeSettings">The node's configuration settings.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the node.</param>
        /// <param name="timeSyncBehaviorState">State of time synchronization feature that stores collected data samples.</param>
        /// <param name="powMining">POW miner.</param>
        public MiningFeature(
            Network network,
            MinerSettings minerSettings,
            NodeSettings nodeSettings,
            ILoggerFactory loggerFactory,
            ITimeSyncBehaviorState timeSyncBehaviorState,
            IPowMining powMining)
        {
            this.network = network;
            this.minerSettings = minerSettings;
            this.nodeSettings = nodeSettings;
            this.powMining = powMining;
            this.timeSyncBehaviorState = timeSyncBehaviorState;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc />
        public override void LoadConfiguration()
        {
            this.minerSettings.Load(this.nodeSettings);
        }

        /// <summary>
        /// Prints command-line help.
        /// </summary>
        /// <param name="network">The network to extract values from.</param>
        public static void PrintHelp(Network network)
        {
            MinerSettings.PrintHelp(network);
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            MinerSettings.BuildDefaultConfigurationFile(builder, network);
        }

        /// <summary>
        /// Stop a Proof of Work miner.
        /// </summary>
        public void StopMining()
        {
            this.powMining?.StopMining();
            this.logger.LogInformation("Mining stopped.");
        }

        /// <inheritdoc />
        public override void Initialize()
        {
            if (this.minerSettings.Mine)
            {
                string mineToAddress = this.minerSettings.MineAddress;
                // if (string.IsNullOrEmpty(mineToAddress)) ;
                //    TODO: get an address from the wallet.

                if (!string.IsNullOrEmpty(mineToAddress))
                {
                    this.logger.LogInformation("Mining enabled.");

                    this.powMining.Mine(BitcoinAddress.Create(mineToAddress, this.network).ScriptPubKey);
                }
            }
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            this.StopMining();
        }

        /// <inheritdoc />
        public override void ValidateDependencies(IFullNodeServiceProvider services)
        {
            // Mining and staking require block store feature.
            if (this.minerSettings.Mine)
            {
                services.Features.EnsureFeature<BlockStoreFeature>();
                StoreSettings blockSettings = services.ServiceProvider.GetService<StoreSettings>();
                if (blockSettings.Prune)
                    throw new ConfigurationException("BlockStore prune mode is incompatible with mining and staking.");
            }
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderMiningExtension
    {
        /// <summary>
        /// Adds a mining feature to the node being initialized.
        /// </summary>
        /// <param name="fullNodeBuilder">The object used to build the current node.</param>
        /// <param name="setup">Callback routine to be called when miner settings are loaded.</param>
        /// <returns>The full node builder, enriched with the new component.</returns>
        public static IFullNodeBuilder AddMining(this IFullNodeBuilder fullNodeBuilder, Action<MinerSettings> setup = null)
        {
            LoggingConfiguration.RegisterFeatureNamespace<MiningFeature>("mining");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<MiningFeature>()
                    .DependOn<MempoolFeature>()
                    .DependOn<RPCFeature>()
                    .DependOn<WalletFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<IPowMining, PowMining>();
                        services.AddSingleton<IBlockProvider, BlockProvider>();
                        services.AddSingleton<BlockDefinition, PowBlockDefinition>();
                        services.AddSingleton<MinerController>();
                        services.AddSingleton<MiningRPCController>();
                        services.AddSingleton<MinerSettings>(new MinerSettings(setup));
                    });
            });

            return fullNodeBuilder;
        }

        /// <summary>
        /// Adds POW components to the node, so that it can mine.
        /// </summary>
        /// <param name="fullNodeBuilder">The object used to build the current node.</param>
        /// <param name="setup">Callback routine to be called when miner settings are loaded.</param>
        /// <returns>The full node builder, enriched with the new component.</returns>
        public static IFullNodeBuilder AddPowMining(this IFullNodeBuilder fullNodeBuilder, Action<MinerSettings> setup = null)
        {
            LoggingConfiguration.RegisterFeatureNamespace<MiningFeature>("mining");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<MiningFeature>()
                    .DependOn<MempoolFeature>()
                    .DependOn<RPCFeature>()
                    .DependOn<WalletFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<IPowMining, PowMining>();
                        services.AddSingleton<IBlockProvider, BlockProvider>();
                        services.AddSingleton<BlockDefinition, PowBlockDefinition>();
                        services.AddSingleton<MinerController>();
                        services.AddSingleton<MiningRPCController>();
                        services.AddSingleton<MinerSettings>(new MinerSettings(setup));
                    });
            });

            return fullNodeBuilder;
        }
    }
}