using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BRhodium.Node.Utilities;
using NBitcoin;
using Xunit;

namespace BRhodium.Bitcoin.Features.Wallet.Tests
{
    public class MultisigWalletTest
    {
        private readonly Network network;
        private readonly MultisigScheme multisigScheme;

        public MultisigWalletTest()
        {
            this.network = Network.Main;
            this.multisigScheme = new MultisigScheme()
            {
                Threashold = 2,
                XPubs = new string[]
                {
                    "xpub661MyMwAqRbcFK4g9bHwLNYLmy4JxSFkKNRURL3hJqVYLoZ318eHmEKjMbFxbgDiaMycDk4oixtRkHKgbopRzukzimmyzAb2aEVfAEeJPxt",
                    "xpub661MyMwAqRbcFVViGz3WGiaYgzsKWKXYduJZ8oR4tnnaRLp2QXMUf5P9Yq6CpJ8zuddutUFyrTkMECqqa1UyjnegVpoiYbcqpJfSCP9GcmG",
                    "xpub661MyMwAqRbcFYCYnw23jLqPAHMvCfmLeKkmDSLGy6nGpVtJvN9EDoR2qdz8fmvkV8sSa1ZT7j8oyfBgjHKX2nGnESLqSndrM2gJ6TGcXrU",
                }
            };
        }

        [Fact]
        public void Generate1stMultisigAddress() {

            WalletMultisig wallet = new WalletMultisig(this.multisigScheme);
            Script redeemScript = wallet.GeneratePublicKey(0);
            Assert.Equal("rbAxG3vTMCuVMWppaobvTajBtUHSiFtkr5", redeemScript.Hash.GetAddress(this.network).ToString()); 
        }

        [Fact]
        public void Generate4SequnetialMultisigRecievingAndChangeAddress()
        {
            string[] receiving = { "rbAxG3vTMCuVMWppaobvTajBtUHSiFtkr5", "roLKXofxDFrkZqW7kU9aD7j7E6pTajEe16", "rjmcR79w6MatNK3KeHR3FvaEEHngdAKa9h", "reD6MyXPFUaJpyBtJVDgdYf7iN4qHbhzBz" };
            string[] change = { "rXh3PVYpn462fDTAmLiUyKZ1aTWdzr4W9J", "rjNVcwuXKW1d8j8CQbxK4Kim5Hkg7Z4ZhL", "rginUKQEG9XjQVfhJ5ho7P4v9ENrtvF8Uk", "rgeVtHemGFGQzRU2Dhj3gYTgsWcjg4wgG5" };

            WalletMultisig wallet = new WalletMultisig(this.multisigScheme);

            for (int i = 0; i < receiving.Length; i++)
            {
                Script redeemScript = wallet.GeneratePublicKey(i);
                Assert.Equal(receiving[i], redeemScript.Hash.GetAddress(this.network).ToString());
            }

            for (int i = 0; i < change.Length; i++)
            {
                Script redeemScript = wallet.GeneratePublicKey(i, true);
                Assert.Equal(change[i], redeemScript.Hash.GetAddress(this.network).ToString());
            }
        }
    }
}
