using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;
using BRhodium.Node.Configuration.Settings;
using BRhodium.Node.Utilities;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace BRhodium.Node.Configuration.Logging
{
    /// <summary>
    /// An extension of the <see cref="LoggerFactory"/> that allows access to some internal components.
    /// </summary>
    public class ExtendedLoggerFactory : LoggerFactory
    {
        /// <summary>Configuration of console logger.</summary>
        public ILoggingBuilder ConsoleSettings { get; set; }

        /// <summary>Provider of console logger.</summary>
        public ConsoleLoggerProvider ConsoleLoggerProvider { get; set; }

        public static ILoggerFactory Create()
        {
            return ExtendedLoggerFactory.Create(builder =>
            {
                builder.AddFilter("Default", LogLevel.Information)
                    .AddFilter("System", LogLevel.Warning)
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("Microsoft.AspNetCore", LogLevel.Error)
                    .AddConsole();
            }
            );
        }

        /// <summary>Loads the NLog.config file from the <see cref="DataFolder"/>, if it exists.</summary>
        public static ILoggerFactory Create(LogSettings settings)
        {
            return ExtendedLoggerFactory.Create(builder =>
            {
                LoggingConfiguration.ConfigureConsoleFilters(builder, settings);

                builder.AddFilter("Default", LogLevel.Information)
                    .AddFilter("System", LogLevel.Warning)
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("Microsoft.AspNetCore", LogLevel.Error)
                    .AddFilter<ConsoleLoggerProvider>($"{nameof(BRhodium)}.*", LogLevel.Information)
                    .AddConsole();

                builder.SetMinimumLevel(LogLevel.Debug);
            }
            );
        }
    }

    /// <summary>
    /// Integration of NLog with Microsoft.Extensions.Logging interfaces.
    /// </summary>
    public static class LoggingConfiguration
    {
        /// <summary>Width of a column for pretty console/log outputs.</summary>
        public const int ColumnLength = 20;

        /// <summary>Currently used node's log settings.</summary>
        private static LogSettings logSettings;

        /// <summary>Currently used data folder to determine path to logs.</summary>
        private static DataFolder folder;

        /// <summary>Mappings of keys to class name spaces to be used when filtering log categories.</summary>
        private static readonly Dictionary<string, string> keyCategories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            //{ "addrman", "" },
            //{ "alert", "" },
            //{ "cmpctblock", "" }
            //{ "coindb", "" },
            //{ "http", "" },
            //{ "libevent", "" },
            //{ "lock", "" },
            //{ "mempoolrej", "" },
            { "net", $"{nameof(BRhodium)}.{nameof(Node)}.{nameof(Connection)}.*" },
            //{ "proxy", "" },
            //{ "prune", "" },
            //{ "rand", "" },
            //{ "reindex", "" },
            //{ "qt", "" },
            //{ "selectcoins", "" },
            //{ "tor", "" },
            //{ "zmq", "" },

            // Short Names
            { "configuration", $"{nameof(BRhodium)}.{nameof(Node)}.{nameof(Configuration)}.*" },
            { "fullnode", $"{nameof(BRhodium)}.{nameof(Node)}.{nameof(FullNode)}" }
        };

        public static void RegisterFeatureNamespace<T>(string key)
        {
            keyCategories[key] = typeof(T).Namespace + ".*";
        }

        public static void RegisterFeatureClass<T>(string key)
        {
            keyCategories[key] = typeof(T).Namespace + "." + typeof(T).Name;
        }

        /// <summary>
        /// Initializes application logging.
        /// </summary>
        static LoggingConfiguration()
        {
            // If there is no NLog.config file, we need to initialize the configuration ourselves.
            if (LogManager.Configuration == null) LogManager.Configuration = new NLog.Config.LoggingConfiguration();

            // Installs handler to be called when NLog's configuration file is changed on disk.
            LogManager.ConfigurationReloaded += NLogConfigurationReloaded;
        }

        /// <summary>
        /// Event handler to be called when logging <see cref="NLog.LogManager.Configuration"/> gets reloaded.
        /// </summary>
        /// <param name="sender">Not used.</param>
        /// <param name="e">Not used.</param>
        public static void NLogConfigurationReloaded(object sender, LoggingConfigurationReloadedEventArgs e)
        {
            AddFilters(logSettings, folder);
        }

        /// <summary>
        /// Extends the logging rules in the "NLog.config" with node log settings rules.
        /// </summary>
        /// <param name="settings">Node log settings to extend the rules from the configuration file, or null if no extension is required.</param>
        /// <param name="dataFolder">Data folder to determine path to log files.</param>
        private static void AddFilters(LogSettings settings = null, DataFolder dataFolder = null)
        {
            if (settings == null) return;

            logSettings = settings;
            folder = dataFolder;

            // If we use "debug*" targets, which are defined in "NLog.config", make sure they log into the correct log folder in data directory.
            List<Target> debugTargets = LogManager.Configuration.AllTargets.Where(t => (t.Name != null) && t.Name.StartsWith("debug")).ToList();
            foreach (Target debugTarget in debugTargets)
            {
                FileTarget debugFileTarget = debugTarget is AsyncTargetWrapper ? (FileTarget)((debugTarget as AsyncTargetWrapper).WrappedTarget) : (FileTarget)debugTarget;
                string currentFile = debugFileTarget.FileName.Render(new LogEventInfo { TimeStamp = DateTime.UtcNow });
                debugFileTarget.FileName = Path.Combine(folder.LogPath, Path.GetFileName(currentFile));
            }

            // Remove rule that forbids logging before the logging is initialized.
            LoggingRule nullPreInitRule = null;
            foreach (LoggingRule rule in LogManager.Configuration.LoggingRules)
            {
                if (rule.Final && rule.NameMatches("*") && (rule.Targets.Count > 0) && (rule.Targets[0].Name == "null"))
                {
                    nullPreInitRule = rule;
                    break;
                }
            }
            LogManager.Configuration.LoggingRules.Remove(nullPreInitRule);

            // Configure main file target, configured using command line or node configuration file settings.
            var mainTarget = new FileTarget
            {
                Name = "main",
                FileName = Path.Combine(folder.LogPath, "node.txt"),
                ArchiveFileName = Path.Combine(folder.LogPath, "node-${date:universalTime=true:format=yyyy-MM-dd}.txt"),
                ArchiveNumbering = ArchiveNumberingMode.Sequence,
                ArchiveEvery = FileArchivePeriod.Day,
                MaxArchiveFiles = 7,
                Layout = "[${longdate:universalTime=true} ${threadid}${mdlc:item=id}] ${level:uppercase=true}: ${callsite} ${message}",
                Encoding = Encoding.UTF8
            };

            LogManager.Configuration.AddTarget(mainTarget);

            // Default logging level is Info for all components.
            var defaultRule = new LoggingRule($"{nameof(BRhodium)}.{nameof(Node)}.*", settings.NLogLevel, mainTarget);
            var defaultFeatureRule = new LoggingRule($"BRhodium.Bitcoin.Features.*", settings.NLogLevel, mainTarget);

            if (settings.DebugArgs.Any())
            {
                if (settings.DebugArgs[0] == "1")
                {
                    // Increase all logging to Debug level.
                    defaultRule = new LoggingRule($"{nameof(BRhodium)}.{nameof(Node)}.*", NLog.LogLevel.Debug, mainTarget);
                    defaultFeatureRule = new LoggingRule($"BRhodium.Bitcoin.Features.*", NLog.LogLevel.Debug, mainTarget);
                }
                else
                {
                    HashSet<string> usedCategories = new HashSet<string>(StringComparer.Ordinal);

                    // Increase selected categories to Debug.
                    foreach (string key in settings.DebugArgs)
                    {
                        if (!keyCategories.TryGetValue(key.Trim(), out var category))
                        {
                            // Allow direct specification - e.g. "-debug=BRhodium.Node.Miner".
                            category = key.Trim();
                        }

                        if (!usedCategories.Contains(category))
                        {
                            usedCategories.Add(category);
                            var rule = new LoggingRule(category, NLog.LogLevel.Debug, mainTarget);
                            LogManager.Configuration.LoggingRules.Add(rule);
                        }
                    }
                }
            }

            LogManager.Configuration.LoggingRules.Add(defaultRule);
            LogManager.Configuration.LoggingRules.Add(defaultFeatureRule);

            // Apply new rules.
            LogManager.ReconfigExistingLoggers();
        }

        /// <summary>
        /// Extends the logging rules in the "NLog.config" with node log settings rules.
        /// </summary>
        /// <param name="loggerFactory">Not used.</param>
        /// <param name="settings">Node log settings to extend the rules from the configuration file, or null if no extension is required.</param>
        /// <param name="dataFolder">Data folder to determine path to log files.</param>
        public static void AddFilters(this ILoggerFactory loggerFactory, LogSettings settings, DataFolder dataFolder)
        {
            AddFilters(settings, dataFolder);
        }

        /// <summary>
        /// Configure the console logger and set it to filter logs not related to the fullnode.
        /// </summary>
        /// <param name="loggerFactory">Not used.</param>
        /// <param name="consoleLoggerSettings">Console settings to filter.</param>
        /// <param name="settings">Settings that hold potential debug arguments, if null no debug arguments will be loaded."/></param>
        public static void ConfigureConsoleFilters(ILoggingBuilder builder, LogSettings settings)
        {
            if (settings != null)
            {
                if (settings.DebugArgs.Any())
                {
                    if (settings.DebugArgs[0] == "1")
                    {
                        // Increase all logging to Debug.
                        builder.AddFilter<ConsoleLoggerProvider>($"{nameof(BRhodium)}", Microsoft.Extensions.Logging.LogLevel.Debug);
                    }
                    else
                    {
                        lock (keyCategories)
                        {
                            var usedCategories = new HashSet<string>(StringComparer.Ordinal);

                            // Increase selected categories to Debug.
                            foreach (string key in settings.DebugArgs)
                            {
                                if (!keyCategories.TryGetValue(key.Trim(), out string category))
                                {
                                    // Allow direct specification - e.g. "-debug=Blockcore.Miner".
                                    category = key.Trim();
                                }

                                if (!usedCategories.Contains(category))
                                {
                                    usedCategories.Add(category);
                                    builder.AddFilter<ConsoleLoggerProvider>(category.TrimEnd('*').TrimEnd('.'), Microsoft.Extensions.Logging.LogLevel.Debug);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
