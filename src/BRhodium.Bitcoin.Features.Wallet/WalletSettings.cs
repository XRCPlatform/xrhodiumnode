using System;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using BRhodium.Node.Configuration;
using BRhodium.Node.Utilities;
using System.Collections.Generic;

namespace BRhodium.Bitcoin.Features.Wallet
{
    /// <summary>
    /// Configuration related to the wallet.
    /// </summary>
    public class WalletSettings
    {
        /// <summary>
        /// A value indicating whether the transactions hex representations should be saved in the wallet file.
        /// </summary>
        public bool SaveTransactionHex { get; set; }

        /// <summary>
        /// A value indicating whether the wallet being run is the light wallet or the full wallet.
        /// </summary>
        public bool IsLightWallet { get; set; }
        /// <summary>
        /// Wallet notification subscriptions.
        /// </summary>
        public List<WalletNotification> WalletNotify
        {
            get
            {
                return _walletNotify;
            }
        }
        /// <summary>
        /// A callback allow changing the default settings.
        /// </summary>
        private readonly Action<WalletSettings> callback;

        private List<WalletNotification> _walletNotify = new List<WalletNotification>();

        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        public WalletSettings()
        {
        }

        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        /// <param name="callback">Callback routine to be called once the wallet settings are loaded.</param>
        public WalletSettings(Action<WalletSettings> callback = null)
        {
            this.callback = callback;
        }

        /// <summary>
        /// Loads the wallet settings from the application configuration.
        /// </summary>
        /// <param name="nodeSettings">Application configuration.</param>
        public void Load(NodeSettings nodeSettings)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));

            TextFileConfiguration config = nodeSettings.ConfigReader;
            this.SaveTransactionHex = config.GetOrDefault<bool>("savetrxhex", false);
            var walletNotifications = config.GetAll("walletnotify");

            foreach (var subcription in walletNotifications)
            {
                this._walletNotify.Add(new WalletNotification(subcription));
            }

            this.callback?.Invoke(this);
        }

        /// <summary>
        /// Displays wallet configuration help information on the console.
        /// </summary>
        /// <param name="mainNet">Not used.</param>
        public static void PrintHelp(Network mainNet)
        {
            var defaults = NodeSettings.Default();
            var builder = new StringBuilder();

            builder.AppendLine("-savetrxhex=<0 or 1>            Save the hex of transactions in the wallet file. Default: false.");
            defaults.Logger.LogInformation(builder.ToString());
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            builder.AppendLine("####Wallet Settings####");
            builder.AppendLine("#Save the hex of transactions in the wallet file. Default: 0.");
            builder.AppendLine("#savetrxhex=0");
            builder.AppendLine("#walletnotify=recieved:curl.exe -s http://localhost:62602/walletnotify?%s");
            builder.AppendLine("#walletnotify=recieved:/home/myuser/transaction.sh %s");
        }
    }
}
