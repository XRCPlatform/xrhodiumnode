using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace BRhodium.Node.Configuration.Settings
{
    /// <summary>
    /// Configuration related to logging.
    /// </summary>
    public class LogSettings
    {
        /// <summary>
        /// Initializes an instance of the object with default values.
        /// </summary>
        public LogSettings()
        {
            this.DebugArgs = new List<string>();
            this.LogLevel = LogLevel.Information;
            this.NLogLevel = NLog.LogLevel.Info;
        }

        /// <summary>List of categories to enable debugging information for.</summary>
        /// <remarks>A special value of "1" of the first category enables trace level debugging information for everything.</remarks>
        public List<string> DebugArgs { get; private set; }

        /// <summary>Level of logging details.</summary>
        public LogLevel LogLevel { get; private set; }

        /// <summary>Level of logging for NLog</summary>
        public NLog.LogLevel NLogLevel { get; private set; }

        private void MapLogLevels()
        {
            switch(this.LogLevel)
            {
                case LogLevel.Critical:
                    this.NLogLevel = NLog.LogLevel.Fatal;
                    break;
                case LogLevel.Debug:
                    this.NLogLevel = NLog.LogLevel.Debug;
                    break;
                case LogLevel.Error:
                    this.NLogLevel = NLog.LogLevel.Error;
                    break;
                case LogLevel.Information:
                    this.NLogLevel = NLog.LogLevel.Info;
                    break;
                case LogLevel.None:
                    this.NLogLevel = NLog.LogLevel.Off;
                    break;
                case LogLevel.Trace:
                    this.NLogLevel = NLog.LogLevel.Trace;
                    break;
                case LogLevel.Warning:
                    this.NLogLevel = NLog.LogLevel.Warn;
                    break;
            }
        }

        /// <summary>
        /// Loads the logging settings from the application configuration.
        /// </summary>
        /// <param name="config">Application configuration.</param>
        public void Load(TextFileConfiguration config)
        {
            this.DebugArgs = config.GetOrDefault("debug", string.Empty).Split(',').Where(s => !string.IsNullOrEmpty(s)).ToList();

            // Get the minimum log level. The default is Information.
            LogLevel minLogLevel = LogLevel.Information;
            string logLevelArg = config.GetOrDefault("loglevel", string.Empty);
            if (!string.IsNullOrEmpty(logLevelArg))
            {
                if (!Enum.TryParse(logLevelArg, true, out minLogLevel))
                {
                    minLogLevel = LogLevel.Information;
                }
            }

            this.LogLevel = minLogLevel;
            MapLogLevels();
        }
    }
}
