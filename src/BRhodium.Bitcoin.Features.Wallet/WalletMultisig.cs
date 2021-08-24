using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin;

namespace BRhodium.Bitcoin.Features.Wallet
{
    public class WalletMultisig : Wallet
    {
        public static readonly bool IsMultisig = true;
        private readonly MultisigScheme multisigScheme;

        public WalletMultisig(MultisigScheme multisigScheme)
        {
            this.multisigScheme = multisigScheme;
        }

        public MultisigScheme MultisigScheme
        {
            get
            {
                return this.multisigScheme;
            }
        }

        /// Generates an HD public key derived from an extended public key.
        /// </summary>
        /// <param name="accountExtPubKey">The extended public key used to generate child keys.</param>
        /// <param name="index">The index of the child key to generate.</param>
        /// <param name="isChange">A value indicating whether the public key to generate corresponds to a change address.</param>
        /// <returns>
        /// An HD public key derived from an extended public key.
        /// </returns>

        public Script GeneratePublicKey(int hdPathIndex, bool isChange = false)
        {
            List<PubKey> derivedPubKeys = new List<PubKey>();
            foreach (var xpub in this.MultisigScheme.XPubs)
            {
                derivedPubKeys.Add(HdOperations.GeneratePublicKey(xpub, hdPathIndex, isChange));
            }
            var sortedkeys = LexographicalSort(derivedPubKeys);

            Script redeemScript = PayToMultiSigTemplate.Instance.GenerateScriptPubKey(this.MultisigScheme.Threashold, sortedkeys.ToArray());
            return redeemScript;
        }

        static IEnumerable<PubKey> LexographicalSort(IEnumerable<PubKey> pubKeys)
        {
            List<PubKey> list = new List<PubKey>();
            var e = pubKeys.Select(s => s.ToHex());
            var sorted = e.OrderByDescending(s => s.Length).ThenBy(r => r);
            foreach (var item in sorted)
            {
                list.Add(new PubKey(item));
            }
            return list;
        }        
    }
}
