using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using BRhodium.Bitcoin.Features.BlockStore.Controllers;
using BRhodium.Bitcoin.Features.BlockStore.Models;
using BRhodium.Node.Tests.Common;
using BRhodium.Node.Utilities.JsonErrors;
using Xunit;

namespace BRhodium.Bitcoin.Features.BlockStore.Tests
{
    public class BlockStoreControllerTests
    {
        private const string ValidHash = "df57b0765146219d95187b04b1f146be4b804ee4715fb8a245f3cd0c8ad87c5f";

        // Mainnet block 100
        private const string BlockAsHex =
            "0000002000a216ff547fc10f6e835f55cb899b92bd8c928c1646dac8327184a1a457bd945d3ff2cff9ff3d78ca057fd9" +
            "cadd25fd2db71c9c3572a6e4f37193b2bdc439a83926d25bffff0f1ee2c5001703000000200100000000000000000000" +
            "00000000000000000000000000000000000000000000ffffffff240164062f503253482f03938117082f190198000004" +
            "a70d2f42545220436f696e69756d2f00000000020837c10e000000001976a914f80f823d37d7186b3304ca2e35f1e250" +
            "cec896dc88ac58272600000000001976a9142a5d5c1ad8df19871d4fec3c4f034c4e8bfd3dd788ac0000000001000000" +
            "05ec0f2683f0d416481a173b10802f01b0b22eba0ff230f5375d79a9ad5793448b000000006a47304402204977afe039" +
            "4462b50fe73e2d95a9d4609a9f1671f3883ce55a7a863fcff39510022040090df63a233c4662e504889f3aff5e28a025" +
            "cf431d810dd4daf76f968f3e27012102c13dd76b601edb660d5ff322aeb243b193cb27ad615881b9c58d11f0575889a5" +
            "ffffffffd2bd895305122d04639379fbc263e8e2189378ce8da99407b9fd627cbb236a05010000006b483045022100c6" +
            "d34cb864914960e712ba11a7d19eb09d69a821f3ace82d9da811b7adb780d5022064adf73d46f0c230ccb9d4b03018df" +
            "654f63202c3506cecdd90b586d82fd00b401210283afa030a245b60af7dfa3224ba08bd9431a071a041bd398f21df0e4" +
            "169c4ee3ffffffffecc43d1bdf2905e0f8a811d467b9c596ed40114db9cb442f3875e66d5618a1ef010000006a473044" +
            "02202f87e9abd31c78953ff1a233b857503088483bece5126251abc64d253fee5d7102202ab0c999542be83483b34ff6" +
            "bac672d07eb8f76469b946c416d16368f9ad789e01210283afa030a245b60af7dfa3224ba08bd9431a071a041bd398f2" +
            "1df0e4169c4ee3ffffffff08e42f79534d94d180d77f0004bfac7319ddfd75c380a4a53a26787f641c4f34000000006a" +
            "47304402201dee9331fa8a19864bb7cb499418f0aa9b8e1e5935709999d727a669b8bf49830220065624495cacbe0b2f" +
            "fdf3b280d1d5d42d1ea421cc8cb8f36710126a938fce82012103f421862673ae3d0429b2139a5fb6d384fc06a46e5df8" +
            "1f74bf6ef84e99d6f341ffffffffb749861d208509bf7f054e139094c106bb29a2a0758a71952eea99bf809fe30f0000" +
            "00006a47304402207749b6c4d2c12d87c059afbb077510547b0aaeec9b703567ae144fdb59edb1b702202adda410d009" +
            "d249ad214b26feb42e9e4912b6fb5142e9ceef819523e9822923012103ca460a3ea5c73e355520bc086fb3ccb91b83a1" +
            "08a4a964c18633f71d98075669ffffffff248c7a4f01000000001976a914b71824007f3409df288ec462be2b2b1464f2" +
            "840588ac387e2100000000001976a914e26b62f78d4891947c27f9a7f7f9997ad36e6df288acd0121300000000001976" +
            "a9148436a6e842123b4ce0c2c2f75a54ff98817c1b5a88ac60781300000000001976a914847e0e8227a128feeed55343" +
            "aa98601fa119af6a88acd87c0f00000000001976a91427488a46822a02f025b1c970edc9cdd7c00dc06588ac38de1100" +
            "000000001976a914e80c8d13f74424cc6d6efb3376d8c3754bece43088acf8ed1e00000000001976a914221e8a155094" +
            "4d36138e7e2891fe885c6db3397b88ac9884c400000000001976a914f8fb167d7e45645bc0c0921cd5b0435e9d5d35b3" +
            "88ac10f4c300000000001976a9145546a402145c0306909add69741e80f30bcd1c6d88acb0fdc400000000001976a914" +
            "7a8c8aade401df617ede6b3019aea1fb013bc28c88aca8f1e200000000001976a9141c38de9f3c188a36822caef26707" +
            "61741498e35f88ac80242e00000000001976a914b6fc410c516b5b1362f4b954741c3854f0c9de4088ac1016e8000000" +
            "00001976a914cc48555d4dbf9fd051c1cc2a000e4f54a7a8053588ac18c3c400000000001976a914d4fa79b72b4009a0" +
            "134fe300af8fa4ae4d16feb788acf0a11701000000001976a914b82271a2bf1450905e131b308c384098be92c1b188ac" +
            "70221300000000001976a91466e962c08467adf5b6be5ff4e8c81a42898153c288acd8324500000000001976a914f241" +
            "e5f2454a42e828272740d046afeda34e987388ac20fc6000000000001976a9148b94df3aca4b125214065bea1f70da08" +
            "eaded8db88ac78501900000000001976a9148e22379512626d354f70f6f953b9bd53e25becb788ac7836c40000000000" +
            "1976a91464e11644712aaa89e3a3b1cd189f6843190dbcbc88ac80551100000000001976a91420c75d72fc9bacef3fe6" +
            "461163ab849940ce5f5d88ac08e17a00000000001976a9142d9470dc4e8f6d670b769e530665f742b8bad32f88acb0c9" +
            "2600000000001976a914c537623dc7b63b6b5cfef6ca46c248a4811fdd0988aca8f42600000000001976a914b5117ceb" +
            "5145d59a15a4cf1a9001eca5de916dc388acb0753400000000001976a914d923286931c64eb411d7d6b8da782135ea78" +
            "ff5688ac10902d00000000001976a91421342919eeb6ef9eae19f3e2f32f52cd9b3cf23388ac58321100000000001976" +
            "a914b3ddbc7c63ca4f61794e2434dfd07d39ec56589888ac5019c300000000001976a9141e6633ca1d6c251829e6acb0" +
            "8ef330268a13535688ac20a61b00000000001976a914397fed5debc7f0c8be2837f9535fcffa6e1e886f88aca0a31100" +
            "000000001976a914a7136b11fbc52e1a393779889aef846e6998bd1c88acb0162f00000000001976a91445f5a6c732d5" +
            "d5888890eca274ecaa24180bf5bd88acc8f51e00000000001976a9145261448b506d5b5694f7cea6b02246f287b8bd9e" +
            "88ac0013d003000000001976a91449e47752b21177cc2e0c06a13678dd32d5b0568388ac702e1100000000001976a914" +
            "0e69b333092f0a0b0f6ae66163d63dff4064232888acb0531000000000001976a9141f80b5dc6f0b2356956b4aec9e7b" +
            "57686040322188ace8fc1500000000001976a914062a7d37e69abaddcd8cd34d451aefbece05d78488ac000000000100" +
            "000001ee8a753251c04f8510fbd549030ad76c53f317c6c09f22b08db67c822c02d280000000006b483045022100d172" +
            "1dccb39ea415936fbf4f91a0153167b5b2fd0492b19e5b2730abf55933f002207dbb03c7d370b1cf06b3850049f4c364" +
            "7fda24b4419c49f9793dae3d0074b195012103072034db8408f0d616252ef44081a3a0e0def833af9241a832a7207938" +
            "a780b0ffffffff021cd3ed4e600000001976a914891226b6f5ab1a9c32add631dc8958f3d9eb17c388ac803770eb0000" +
            "00001976a914fb7fc71d86c83e7997d0f4ffca129ac263c8f12088ac00000000";

        private const string InvalidHash = "This hash is no good";

        [Fact]
        public void GetBlock_With_null_Hash_IsInvalid()
        {
            var requestWithNoHash = new SearchByHashRequest()
            {
                Hash = null,
                OutputJson = true
            };
            var validationContext = new ValidationContext(requestWithNoHash);
            Validator.TryValidateObject(requestWithNoHash, validationContext, null, true).Should().BeFalse();
        }

        [Fact]
        public void GetBlock_With_empty_Hash_IsInvalid()
        {
            var requestWithNoHash = new SearchByHashRequest()
            {
                Hash = "",
                OutputJson = false
            };
            var validationContext = new ValidationContext(requestWithNoHash);
            Validator.TryValidateObject(requestWithNoHash, validationContext, null, true).Should().BeFalse();
        }

        [Fact]
        public void GetBlock_With_good_Hash_IsValid()
        {
            var requestWithNoHash = new SearchByHashRequest()
            {
                Hash = "some good hash",
                OutputJson = true
            };
            var validationContext = new ValidationContext(requestWithNoHash);
            Validator.TryValidateObject(requestWithNoHash, validationContext, null, true).Should().BeTrue();
        }

        [Fact]
        public void Get_Block_When_Hash_Is_Not_Found_Should_Return_Not_Found_Object_Result()
        {
            var (cache, controller) = GetControllerAndCache();

            cache.Setup(c => c.GetBlockAsync(It.IsAny<uint256>()))
                .Returns(Task.FromResult((Block)null));

            var response = controller.GetBlockAsync(new SearchByHashRequest()
            { Hash = ValidHash, OutputJson = true });

            response.Result.Should().BeOfType<NotFoundObjectResult>();
            var notFoundObjectResult = (NotFoundObjectResult)response.Result;
            notFoundObjectResult.StatusCode.Should().Be(404);
            notFoundObjectResult.Value.Should().Be("Block not found");
        }

        [Fact]
        public void Get_Block_When_Hash_Is_Invalid_Should_Error_With_Explanation()
        {
            var (cache, controller) = GetControllerAndCache();

            var response = controller.GetBlockAsync(new SearchByHashRequest()
            { Hash = InvalidHash, OutputJson = true });

            response.Result.Should().BeOfType<ErrorResult>();
            var notFoundObjectResult = (ErrorResult)response.Result;
            notFoundObjectResult.StatusCode.Should().Be(400);
            ((ErrorResponse)notFoundObjectResult.Value).Errors[0]
                .Description.Should().Contain("Invalid Hex String");
        }

        [Fact]
        public void Get_Block_When_Block_Is_Found_And_Requesting_JsonOuput()
        {
            var (cache, controller) = GetControllerAndCache();

            cache.Setup(c => c.GetBlockAsync(It.IsAny<uint256>()))
                .Returns(Task.FromResult(Block.Parse(BlockAsHex, Network.TestNet)));

            var response = controller.GetBlockAsync(new SearchByHashRequest()
                {Hash = ValidHash, OutputJson = true});

            response.Result.Should().BeOfType<JsonResult>();
            var result = (JsonResult) response.Result;

            result.Value.Should().BeOfType<Models.BlockModel>();
            ((BlockModel) result.Value).Hash.Should().Be(ValidHash);
            ((BlockModel) result.Value).MerkleRoot.Should()
                .Be("a839c4bdb29371f3e4a672359c1cb72dfd25ddcad97f05ca783dfff9cff23f5d");
        }

        [Fact]
        public void Get_Block_When_Block_Is_Found_And_Requesting_RawOutput()
        {
                var (cache, controller) = GetControllerAndCache();

                cache.Setup(c => c.GetBlockAsync(It.IsAny<uint256>()))
                    .Returns(Task.FromResult(Block.Parse(BlockAsHex, Network.TestNet)));

                var response = controller.GetBlockAsync(new SearchByHashRequest()
                { Hash = ValidHash, OutputJson = false });

                response.Result.Should().BeOfType<JsonResult>();
                var result = (JsonResult)response.Result;
                ((Block)(result.Value)).ToHex(Network.TestNet).Should().Be(BlockAsHex);
        }

        private static (Mock<IBlockStoreCache> cache, BlockStoreController controller) GetControllerAndCache()
        {
            var logger = new Mock<ILoggerFactory>();
            var cache = new Mock<IBlockStoreCache>();

            logger.Setup(l => l.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>);

            var controller = new BlockStoreController(logger.Object, cache.Object);

            return (cache, controller);
        }
    }
}