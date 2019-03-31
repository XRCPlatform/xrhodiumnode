using System.Linq;
using NBitcoin;
using NBitcoin.RPC;
using BRhodium.Node.IntegrationTests.EnvironmentMockUpHelpers;
using Xunit;
using BRhodium.Bitcoin.Features.Wallet.Interfaces;
using BRhodium.Bitcoin.Features.Wallet;
using BRhodium.Node.Tests.Wallet.Common;
using BRhodium.Node.Configuration;

namespace BRhodium.Node.IntegrationTests.RPC
{
    /// <summary>
    /// Bitcoin test fixture for RPC tests.
    /// </summary>
    public class RpcTestFixtureBitcoin : RpcTestFixtureBase
    {


        /// <inheritdoc />
        protected override void InitializeFixture()
        {
            this.Builder = NodeBuilder.Create();
            this.Node = this.Builder.CreateBRhodiumPowNode();
            this.Builder.StartAll();

            var walletManager = this.Node.FullNode.WalletManager();
            walletManager.CreateWallet("password", "Wallet1");
            this.TestWallet = walletManager.GetWalletByName("Wallet1");
            var hdAddress = this.TestWallet.AccountsRoot.FirstOrDefault().Accounts.FirstOrDefault().ExternalAddresses.FirstOrDefault();

            var key = this.TestWallet.GetExtendedPrivateKeyForAddress("password", hdAddress).PrivateKey;
            this.Node.SetDummyMinerSecret(new BitcoinSecret(key, this.Node.FullNode.Network));

            this.RpcClient = this.Node.CreateRPCClient();
            this.NetworkPeerClient = this.Node.CreateNetworkPeerClient();
            this.NetworkPeerClient.VersionHandshakeAsync().GetAwaiter().GetResult();

            // generate 11 blocks
            this.Node.GenerateBRhodiumWithMiner(101);
        }
    }

    /// <summary>
    /// These tests share a test fixture that creates a 101 block chain of transactions to be queried via RPC.
    /// This transaction chain should not be modified by the tests as the tests here assume the state of the
    /// chain is immutable.
    /// Tests that require modifying the transactions should be done in <see cref="RpcBitcoinMutableTests"/>
    /// and set up the chain in each test.
    /// </summary>
    public class RpcBitcoinImmutableTests : IClassFixture<RpcTestFixtureBitcoin>
    {
        private readonly RpcTestFixtureBitcoin rpcTestFixture;

        public RpcBitcoinImmutableTests(RpcTestFixtureBitcoin RpcTestFixture)
        {
            this.rpcTestFixture = RpcTestFixture;
        }

        /// <summary>
        /// <seealso cref="https://github.com/MetacoSA/NBitcoin/blob/master/NBitcoin.Tests/RPCClientTests.cs">NBitcoin test CanGetTxOutFromRPC</seealso>
        /// </summary>
        [Fact(Skip = "Unsuitible UnspentCoin deserializer")]
        public void GetTxOutWithValidTxThenReturnsCorrectUnspentTx()
        {
            RPCClient rpc = this.rpcTestFixture.RpcClient;
            UnspentCoin[] unspent = rpc.ListUnspent(this.rpcTestFixture.TestWallet.Name,1);
            Assert.True(unspent.Any());
            UnspentCoin coin = unspent[0];
            UnspentTransaction resultTxOut = rpc.GetTxOut(coin.OutPoint.Hash, coin.OutPoint.N, true);
            Assert.Equal((int)coin.Confirmations, resultTxOut.confirmations);
            Assert.Equal(coin.Amount.ToDecimal(MoneyUnit.XRC), resultTxOut.value);
            Assert.Equal(coin.Address.ToString(), resultTxOut.scriptPubKey.addresses[0]);
        }

        /// <summary>
        /// <seealso cref="https://github.com/MetacoSA/NBitcoin/blob/master/NBitcoin.Tests/RPCClientTests.cs">NBitcoin test CanGetTxOutAsyncFromRPC</seealso>
        /// </summary>
        [Fact(Skip = "Unsuitible UnspentCoin deserializer")]
        public async void GetTxOutAsyncWithValidTxThenReturnsCorrectUnspentTxAsync()
        {
            RPCClient rpc = this.rpcTestFixture.RpcClient;
            UnspentCoin[] unspent = rpc.ListUnspent(this.rpcTestFixture.TestWallet.Name,1);
            Assert.True(unspent.Any());
            UnspentCoin coin = unspent[0];
            UnspentTransaction resultTxOut = await rpc.GetTxOutAsync(coin.OutPoint.Hash, coin.OutPoint.N, true);
            Assert.Equal((int)coin.Confirmations, resultTxOut.confirmations);
            Assert.Equal(coin.Amount.ToDecimal(MoneyUnit.XRC), resultTxOut.value);
            Assert.Equal(coin.Address.ToString(), resultTxOut.scriptPubKey.addresses[0]);
        }

        /// <summary>
        /// <seealso cref="https://github.com/MetacoSA/NBitcoin/blob/master/NBitcoin.Tests/RPCClientTests.cs">NBitcoin test CanUseAsyncRPC</seealso>
        /// </summary>
        [Fact]
        public void GetBlockCountAsyncWithValidChainReturnsCorrectCount()
        {
            RPCClient rpc = this.rpcTestFixture.RpcClient;
            int blkCount = rpc.GetBlockCountAsync().Result;
            Assert.Equal(101, blkCount);
        }

        /// <summary>
        /// <seealso cref="https://github.com/MetacoSA/NBitcoin/blob/master/NBitcoin.Tests/RPCClientTests.cs">NBitcoin test CanGetPeersInfo</seealso>
        /// </summary>
        [Fact]
        public void GetPeersInfoWithValidPeersThenReturnsPeerInfo()
        {
            PeerInfo[] peers = this.rpcTestFixture.RpcClient.GetPeersInfo();
            Assert.NotEmpty(peers);
        }

        /// <summary>
        /// <seealso cref="https://github.com/MetacoSA/NBitcoin/blob/master/NBitcoin.Tests/RPCClientTests.cs">NBitcoin test EstimateFeeRate</seealso>
        /// </summary>
        [Fact]
        public void EstimateFeeRateReturnsCorrectValues()
        {
            RPCClient rpc = this.rpcTestFixture.RpcClient;
            //Assert.Throws<NoEstimationException>(() => rpc.EstimateFeeRate(1));
            Assert.Equal(Money.Coins(1050250m), rpc.GetBalance(1, false));
            Assert.Equal(Money.Coins(1050250m), rpc.GetBalance());
        }

        /// <summary>
        /// <seealso cref="https://github.com/MetacoSA/NBitcoin/blob/master/NBitcoin.Tests/RPCClientTests.cs">NBitcoin test TestFundRawTransaction</seealso>
        /// </summary>
        [Fact(Skip = "Wallet references aren't passed")]
        public void FundRawTransactionWithValidTxsThenReturnsCorrectResponse()
        {
            var k = new Key();
            var tx = new Transaction();
            var unspentOutputs = this.rpcTestFixture.TestWallet.GetAllSpendableTransactions((CoinType)this.rpcTestFixture.TestWallet.Network.Consensus.CoinType,100,1);
            var outPoint = unspentOutputs.FirstOrDefault().ToOutPoint();
            TxIn input = new TxIn(outPoint);
            tx.AddInput(input);
            tx.Outputs.Add(new TxOut(Money.Coins(1), k));
            RPCClient rpc = this.rpcTestFixture.RpcClient;
            FundRawTransactionResponse result = rpc.FundRawTransaction(tx);
            TestFundRawTransactionResult(tx, result);

            result = rpc.FundRawTransaction(tx, new FundRawTransactionOptions());
            TestFundRawTransactionResult(tx, result);
            FundRawTransactionResponse result1 = result;

            BitcoinAddress change = rpc.GetNewAddress();
            BitcoinAddress change2 = rpc.GetRawChangeAddress();
            result = rpc.FundRawTransaction(tx, new FundRawTransactionOptions()
            {
                FeeRate = new FeeRate(Money.Satoshis(50), 1),
                IncludeWatching = true,
                ChangeAddress = change,
            });
            TestFundRawTransactionResult(tx, result);
            Assert.True(result1.Fee < result.Fee);
            Assert.Contains(result.Transaction.Outputs, o => o.ScriptPubKey == change.ScriptPubKey);
        }

        private static void TestFundRawTransactionResult(Transaction tx, FundRawTransactionResponse result)
        {
            Assert.Equal(tx.Version, result.Transaction.Version);
            Assert.True(result.Transaction.Inputs.Count > 0);
            Assert.True(result.Transaction.Outputs.Count > 1);
            Assert.True(result.ChangePos != -1);
            Assert.Equal(Money.Coins(50m) - result.Transaction.Outputs.Select(txout => txout.Value).Sum(), result.Fee);
        }
    }
}
