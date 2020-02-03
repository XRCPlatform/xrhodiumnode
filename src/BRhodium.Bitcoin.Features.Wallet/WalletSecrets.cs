using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System;
using System.Collections.Concurrent;
using BRhodium.Node.Utilities;
using Microsoft.AspNetCore.DataProtection;
using NBitcoin;

namespace BRhodium.Bitcoin.Features.Wallet
{
    /// <summary>
    /// Store wallet secrets without leaving passwords left unencrypted in memory.
    /// Can timeout passwords.
    /// </summary>
    public class WalletSecrets
    {
        private ConcurrentDictionary<string, string> walletPasswords;

        private ConcurrentDictionary<string, DateTime> walletPasswordsExpiration;

        private IDataProtectionProvider provider;
        private IDataProtector protector;

        /// <summary>
        /// Generate a WalletSecrets storage with a data protection provider.
        /// </summary>
        /// <param name="provider"></param>
        public WalletSecrets(IDataProtectionProvider provider)
        {
            this.provider = provider;
            this.protector = provider.CreateProtector("BRhodium.Bitcoin.Features.Wallet.WalletSecrets");
            this.walletPasswords = new ConcurrentDictionary<string, string>();
            this.walletPasswordsExpiration = new ConcurrentDictionary<string, DateTime>();
        }

        /// <summary>
        /// Stora and cache a reference to the wallet password in memory for [timeout] seconds. 
        /// </summary>
        /// <param name="walletName"></param>
        /// <param name="password"></param>
        /// <param name="timeout"></param>
        public async void UnlockWallet(string walletName, string password, DateTime timeout)
        {
            Guard.NotEmpty(walletName, nameof(walletName));
            Guard.NotEmpty(password, nameof(password));
            var protectedPassword = this.protector.Protect(password);
            this.walletPasswords.TryAdd(walletName, protectedPassword);
            this.walletPasswordsExpiration.TryAdd(walletName, timeout);

            DateTime value;
            this.walletPasswordsExpiration.TryGetValue(walletName, out value);

            await Task.Run(async delegate
            {
                await Task.Delay((int)(Utils.DateTimeToUnixTime(timeout) - Utils.DateTimeToUnixTime(DateTime.Now)));
                this.LockWallet(walletName);
            });

            this.walletPasswordsExpiration.TryGetValue(walletName, out value);
        }

        /// <summary>
        /// Lock the wallet by removing password information.
        /// </summary>
        /// <param name="walletName"></param>
        public void LockWallet(string walletName)
        {
            string password;
            DateTime passwordExpiration;
            this.walletPasswords.TryRemove(walletName, out password);
            this.walletPasswordsExpiration.TryRemove(walletName, out passwordExpiration);
        }

        /// <summary>
        /// Gets wallet password only if it has not expired already.
        /// </summary>
        /// <param name="walletName"></param>
        /// <returns></returns>
        public string GetWalletPassword(string walletName)
        {
            DateTime passwordExpiration;
            this.walletPasswordsExpiration.TryGetValue(walletName, out passwordExpiration);
            if (passwordExpiration < DateTime.Now)
            {
                this.LockWallet(walletName);
                return null;
            }

            string password;
            this.walletPasswords.TryGetValue(walletName, out password);
            return this.protector.Unprotect(password);
        }

        /// <summary>
        /// Get the wallet password expiration.
        /// </summary>
        /// <param name="walletName"></param>
        /// <returns></returns>
        public DateTime GetWalletPasswordExpiration(string walletName)
        {
            DateTime expiration;
            this.walletPasswordsExpiration.TryGetValue(walletName, out expiration);
            return expiration;
        }

    }
}