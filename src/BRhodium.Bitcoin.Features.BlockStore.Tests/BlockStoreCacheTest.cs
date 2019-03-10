﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using BRhodium.Node.Configuration;
using BRhodium.Node.Utilities;
using Xunit;

namespace BRhodium.Bitcoin.Features.BlockStore.Tests
{
    public class BlockStoreCacheTest
    {
        private BlockStoreCache blockStoreCache;
        private readonly Mock<IBlockRepository> blockRepository;
        private readonly ILoggerFactory loggerFactory;
        private readonly NodeSettings nodeSettings;

        public BlockStoreCacheTest()
        {
            this.loggerFactory = new LoggerFactory();
            this.blockRepository = new Mock<IBlockRepository>();

            this.nodeSettings = new NodeSettings();

            this.blockStoreCache = new BlockStoreCache(this.blockRepository.Object, DateTimeProvider.Default, this.loggerFactory, this.nodeSettings);
        }

        [Fact]
        public void GetBlockAsyncBlockInCacheReturnsBlock()
        {
            var powBlockHeader = new BlockHeader();
            powBlockHeader.Version = 1513;
            var block = new Block(powBlockHeader);
            this.blockStoreCache.AddToCache(new Block(block.Header));

            uint256 hash = block.GetHash();
            Block blockFromCache = this.blockStoreCache.GetBlockAsync(hash).GetAwaiter().GetResult();

            Assert.Equal(1513, blockFromCache.Header.Version);
        }

        [Fact]
        public void GetBlockAsyncBlockNotInCacheQueriesRepositoryStoresBlockInCacheAndReturnsBlock()
        {
            uint256 blockId = new uint256(2389704);
            Block repositoryBlock = new Block();
            repositoryBlock.Header.Version = 1451;
            this.blockRepository.Setup(b => b.GetAsync(blockId))
                .Returns(Task.FromResult(repositoryBlock));

            this.blockStoreCache = new BlockStoreCache(this.blockRepository.Object, DateTimeProvider.Default, this.loggerFactory, this.nodeSettings);

            var result = this.blockStoreCache.GetBlockAsync(blockId);
            result.Wait();

            Assert.Equal(1451, result.Result.Header.Version);
        }
    }
}
