using NBitcoin;
using NBitcoin.RPC;
using BRhodium.Node.IntegrationTests.EnvironmentMockUpHelpers;
using Xunit;
using System.Linq;

namespace BRhodium.Node.IntegrationTests.RPC
{
    /// <summary>
    /// These tests are for RPC tests that require modifying the chain/nodes. 
    /// Setup of the chain or nodes can be done in each test.
    /// </summary>
    public class RpcBitcoinMutableTests
    {
        /// <summary>
        /// <seealso cref="https://github.com/MetacoSA/NBitcoin/blob/master/NBitcoin.Tests/RPCClientTests.cs">NBitcoin test CanGetRawMemPool</seealso>
        /// </summary>
        [Fact]
        public void GetRawMemPoolWithValidTxThenReturnsSameTx()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                CoreNode node = builder.CreateBRhodiumPowNode();
                CoreNode nodeB = builder.CreateBRhodiumPowNode();
                builder.StartAll();

                var walletManager = node.FullNode.WalletManager();
                walletManager.CreateWallet("test", "wallet1");
                RPCClient rpcClient = node.CreateRPCClient();
                rpcClient.AddNode(nodeB.Endpoint);

                var wallet = walletManager.GetWalletByName("wallet1");
                var hdAddress = wallet.AccountsRoot.FirstOrDefault().Accounts.FirstOrDefault().ExternalAddresses.FirstOrDefault();

                var key = wallet.GetExtendedPrivateKeyForAddress("test", hdAddress).PrivateKey;
                node.SetDummyMinerSecret(new BitcoinSecret(key, node.FullNode.Network));
                node.GenerateBRhodiumWithMiner(7);

                uint256 txid = rpcClient.SendToAddress("wallet1","test",new Key().PubKey.GetAddress(rpcClient.Network).ToString(), Money.Coins(1.0m).ToDecimal(MoneyUnit.XRC));
                uint256[] ids = rpcClient.GetRawMempool();
                Assert.Single(ids);
                Assert.Equal(txid, ids[0]);
            }
        }

        /// <summary>
        /// <seealso cref="https://github.com/MetacoSA/NBitcoin/blob/master/NBitcoin.Tests/RPCClientTests.cs">NBitcoin test CanAddNodes</seealso>
        /// </summary>
        [Fact]
        public void AddNodeWithValidNodeThenExecutesSuccessfully()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                CoreNode nodeA = builder.CreateBRhodiumPowNode();
                CoreNode nodeB = builder.CreateBRhodiumPowNode();
                builder.StartAll();
                RPCClient rpc = nodeA.CreateRPCClient();
                rpc.RemoveNode(nodeA.Endpoint);
                rpc.AddNode(nodeB.Endpoint);

                AddedNodeInfo[] info = null;
                TestHelper.WaitLoop(() =>
                {
                    info = rpc.GetAddedNodeInfo();
                    return info != null && info.Length > 0;
                });
                Assert.NotNull(info);
                Assert.NotEmpty(info);

                //For some reason this one does not pass anymore in 0.13.1
                //Assert.Equal(nodeB.Endpoint, info.First().Addresses.First().Address);
                AddedNodeInfo oneInfo = rpc.GetAddedNodeInfo(nodeB.Endpoint);
                Assert.NotNull(oneInfo);
                Assert.Equal(nodeB.Endpoint.ToString(), oneInfo.AddedNode.ToString());
                oneInfo = rpc.GetAddedNodeInfo(nodeA.Endpoint);
                Assert.Null(oneInfo);
                rpc.RemoveNode(nodeB.Endpoint);

                TestHelper.WaitLoop(() =>
                {
                    info = rpc.GetAddedNodeInfo();
                    return info.Length == 0;
                });

                Assert.Empty(info);
            }
        }
    }
}
