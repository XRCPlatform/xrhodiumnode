﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NBitcoin;
using BRhodium.Node.Connection;
using BRhodium.Bitcoin.Features.Wallet.Controllers;
using BRhodium.Bitcoin.Features.Wallet.Interfaces;
using BRhodium.Bitcoin.Features.Wallet.Models;
using BRhodium.Node.Interfaces;
using BRhodium.Node.P2P.Peer;
using BRhodium.Node.Tests.Common.Logging;
using BRhodium.Node.Tests.Wallet.Common;
using BRhodium.Node.Utilities;
using BRhodium.Node.Utilities.JsonErrors;
using Xunit;

namespace BRhodium.Bitcoin.Features.Wallet.Tests
{
    public class WalletControllerTest : LogsTestBase
    {
        [Fact]
        public void GenerateMnemonicWithoutParametersCreatesMnemonicWithDefaults()
        {
            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GenerateMnemonic();

            JsonResult viewResult = Assert.IsType<JsonResult>(result);

            string[] resultingWords = (viewResult.Value as string).Split(' ');

            Assert.Equal(12, resultingWords.Length);
            foreach (string word in resultingWords)
            {
                var index = -1;
                Assert.True(Wordlist.English.WordExists(word, out index));
            }
        }

        [Fact]
        public void GenerateMnemonicWithDifferentWordCountCreatesMnemonicWithCorrectNumberOfWords()
        {
            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GenerateMnemonic(wordCount: 24);

            JsonResult viewResult = Assert.IsType<JsonResult>(result);

            string[] resultingWords = (viewResult.Value as string).Split(' ');

            Assert.Equal(24, resultingWords.Length);
        }

        [Fact]
        public void GenerateMnemonicWithStrangeLanguageCasingReturnsCorrectMnemonic()
        {
            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GenerateMnemonic("eNgLiSh");

            JsonResult viewResult = Assert.IsType<JsonResult>(result);

            string[] resultingWords = (viewResult.Value as string).Split(' ');

            Assert.Equal(12, resultingWords.Length);
            foreach (string word in resultingWords)
            {
                var index = -1;
                Assert.True(Wordlist.English.WordExists(word, out index));
            }
        }

        [Fact]
        public void GenerateMnemonicWithEnglishWordListCreatesCorrectMnemonic()
        {
            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GenerateMnemonic("english");

            JsonResult viewResult = Assert.IsType<JsonResult>(result);

            string[] resultingWords = (viewResult.Value as string).Split(' ');

            Assert.Equal(12, resultingWords.Length);
            foreach (string word in resultingWords)
            {
                var index = -1;
                Assert.True(Wordlist.English.WordExists(word, out index));
            }
        }

        [Fact]
        public void GenerateMnemonicWithFrenchWordListCreatesCorrectMnemonic()
        {
            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GenerateMnemonic("french");

            JsonResult viewResult = Assert.IsType<JsonResult>(result);

            string[] resultingWords = (viewResult.Value as string).Split(' ');

            Assert.Equal(12, resultingWords.Length);
            foreach (string word in resultingWords)
            {
                var index = -1;
                Assert.True(Wordlist.French.WordExists(word, out index));
            }
        }

        [Fact]
        public void GenerateMnemonicWithSpanishWordListCreatesCorrectMnemonic()
        {
            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GenerateMnemonic("spanish");

            JsonResult viewResult = Assert.IsType<JsonResult>(result);

            string[] resultingWords = (viewResult.Value as string).Split(' ');

            Assert.Equal(12, resultingWords.Length);
            foreach (string word in resultingWords)
            {
                var index = -1;
                Assert.True(Wordlist.Spanish.WordExists(word, out index));
            }
        }

        [Fact]
        public void GenerateMnemonicWithJapaneseWordListCreatesCorrectMnemonic()
        {
            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GenerateMnemonic("japanese");

            JsonResult viewResult = Assert.IsType<JsonResult>(result);

            // japanese uses a JP space symbol.
            string[] resultingWords = (viewResult.Value as string).Split('　');

            Assert.Equal(12, resultingWords.Length);
            foreach (string word in resultingWords)
            {
                var index = -1;
                Assert.True(Wordlist.Japanese.WordExists(word, out index));
            }
        }

        [Fact]
        public void GenerateMnemonicWithChineseTraditionalWordListCreatesCorrectMnemonic()
        {
            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GenerateMnemonic("chinesetraditional");

            JsonResult viewResult = Assert.IsType<JsonResult>(result);

            string[] resultingWords = (viewResult.Value as string).Split(' ');

            Assert.Equal(12, resultingWords.Length);
            foreach (string word in resultingWords)
            {
                var index = -1;
                Assert.True(Wordlist.ChineseTraditional.WordExists(word, out index));
            }
        }

        [Fact]
        public void GenerateMnemonicWithChineseSimplifiedWordListCreatesCorrectMnemonic()
        {
            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GenerateMnemonic("chinesesimplified");

            JsonResult viewResult = Assert.IsType<JsonResult>(result);

            string[] resultingWords = (viewResult.Value as string).Split(' ');

            Assert.Equal(12, resultingWords.Length);
            foreach (string word in resultingWords)
            {
                var index = -1;
                Assert.True(Wordlist.ChineseSimplified.WordExists(word, out index));
            }
        }

        [Fact]
        public void GenerateMnemonicWithUnknownLanguageReturnsBadRequest()
        {
            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GenerateMnemonic("invalidlanguage");

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);

            Assert.Equal(400, errorResponse.Status);
            Assert.StartsWith("System.FormatException", errorResponse.Description);
            Assert.Equal("Invalid language 'invalidlanguage'. Choices are: English, French, Spanish, Japanese, ChineseSimplified and ChineseTraditional.", errorResponse.ErrorCode);
        }

        [Fact]
        public void CreateWalletSuccessfullyReturnsMnemonic()
        {
            Mnemonic mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
            var mockWalletCreate = new Mock<IWalletManager>();
            mockWalletCreate.Setup(wallet => wallet.CreateWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(mnemonic);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletCreate.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.Create(new WalletCreationRequest
            {
                Name = "myName",
                FolderPath = "",
                Password = "",
                Network = ""
            });

            mockWalletCreate.VerifyAll();
            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            Assert.Equal(mnemonic.ToString(), viewResult.Value);
            Assert.NotNull(result);
        }

        [Fact]
        public void CreateWalletWithInvalidModelStateReturnsBadRequest()
        {
            Mnemonic mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
            var mockWalletCreate = new Mock<IWalletManager>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletCreate.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("Name", "Name cannot be empty.");

            IActionResult result = controller.Create(new WalletCreationRequest
            {
                Name = "",
                FolderPath = "",
                Password = "",
                Network = ""
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);

            Assert.Equal(400, errorResponse.Status);
            Assert.Equal("Name cannot be empty.", errorResponse.ErrorCode);
        }

        [Fact]
        public void CreateWalletWithInvalidOperationExceptionReturnsConflict()
        {
            string errorMessage = "An error occurred.";
            Mnemonic mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
            var mockWalletCreate = new Mock<IWalletManager>();
            mockWalletCreate.Setup(wallet => wallet.CreateWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new WalletException(errorMessage));

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletCreate.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.Create(new WalletCreationRequest
            {
                Name = "myName",
                FolderPath = "",
                Password = "",
                Network = ""
            });

            mockWalletCreate.VerifyAll();
            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);

            Assert.Equal(409, errorResponse.Status);
            Assert.Equal(errorMessage, errorResponse.ErrorCode);
        }

        [Fact]
        public void CreateWalletWithNotSupportedExceptionExceptionReturnsBadRequest()
        {
            Mnemonic mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
            var mockWalletCreate = new Mock<IWalletManager>();
            mockWalletCreate.Setup(wallet => wallet.CreateWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new NotSupportedException("Not supported"));

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletCreate.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.Create(new WalletCreationRequest
            {
                Name = "myName",
                FolderPath = "",
                Password = "",
                Network = ""
            });

            mockWalletCreate.VerifyAll();
            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);

            Assert.Equal(400, errorResponse.Status);
            Assert.Equal("There was a problem creating a wallet.", errorResponse.ErrorCode);
        }

        [Fact]
        public void RecoverWalletSuccessfullyReturnsWalletModel()
        {
            Wallet wallet = new Wallet
            {
                Name = "myWallet",
                Network = NetworkHelpers.GetNetwork("BRhodiumMain")
            };

            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.RecoverWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), null)).Returns(wallet);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.Recover(new WalletRecoveryRequest
            {
                Name = "myWallet",
                FolderPath = "",
                Password = "",
                Network = "MainNet",
                Mnemonic = "mnemonic"
            });

            mockWalletWrapper.VerifyAll();
            OkResult viewResult = Assert.IsType<OkResult>(result);
            Assert.Equal(200, viewResult.StatusCode);
        }

        [Fact]
        public void RecoverWalletWithInvalidModelStateReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("Password", "A password is required.");

            IActionResult result = controller.Recover(new WalletRecoveryRequest
            {
                Name = "myWallet",
                FolderPath = "",
                Password = "",
                Network = "MainNet",
                Mnemonic = "mnemonic"
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);

            Assert.Equal(400, errorResponse.Status);
            Assert.Equal("A password is required.", errorResponse.ErrorCode);
        }

        [Fact]
        public void RecoverWalletWithInvalidOperationExceptionReturnsConflict()
        {
            string errorMessage = "An error occurred.";
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.RecoverWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), null))
                .Throws(new WalletException(errorMessage));

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.Recover(new WalletRecoveryRequest
            {
                Name = "myWallet",
                FolderPath = "",
                Password = "",
                Network = "MainNet",
                Mnemonic = "mnemonic"
            });

            mockWalletWrapper.VerifyAll();
            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);

            Assert.Equal(409, errorResponse.Status);
            Assert.Equal(errorMessage, errorResponse.ErrorCode);
        }

        [Fact]
        public void RecoverWalletWithFileNotFoundExceptionReturnsNotFound()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.RecoverWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), null))
                .Throws(new FileNotFoundException("File not found."));

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.Recover(new WalletRecoveryRequest
            {
                Name = "myWallet",
                FolderPath = "",
                Password = "",
                Network = "MainNet",
                Mnemonic = "mnemonic"
            });

            mockWalletWrapper.VerifyAll();
            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);

            Assert.Equal(404, errorResponse.Status);
            Assert.StartsWith("System.IO.FileNotFoundException", errorResponse.Description);
            Assert.Equal("Wallet not found.", errorResponse.ErrorCode);
        }

        [Fact]
        public void RecoverWalletWithExceptionReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.RecoverWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), null))
                .Throws(new FormatException("Formatting failed."));

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.Recover(new WalletRecoveryRequest
            {
                Name = "myWallet",
                FolderPath = "",
                Password = "",
                Network = "MainNet",
                Mnemonic = "mnemonic"
            });

            mockWalletWrapper.VerifyAll();
            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);

            Assert.Equal(400, errorResponse.Status);
            Assert.StartsWith("System.FormatException", errorResponse.Description);
            Assert.Equal("Formatting failed.", errorResponse.ErrorCode);
        }

        [Fact]
        public void LoadWalletSuccessfullyReturnsWalletModel()
        {
            Wallet wallet = new Wallet
            {
                Name = "myWallet",
                Network = NetworkHelpers.GetNetwork("BRhodiumMain")
            };
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.LoadWallet(It.IsAny<string>(), It.IsAny<string>())).Returns(wallet);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.Load(new WalletLoadRequest
            {
                Name = "myWallet",
                FolderPath = "",
                Password = ""
            });

            mockWalletWrapper.VerifyAll();
            OkResult viewResult = Assert.IsType<OkResult>(result);
            Assert.Equal(200, viewResult.StatusCode);
        }

        [Fact]
        public void LoadWalletWithInvalidModelReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("Password", "A password is required.");

            IActionResult result = controller.Load(new WalletLoadRequest
            {
                Name = "myWallet",
                FolderPath = "",
                Password = ""
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);

            Assert.Equal(400, errorResponse.Status);
            Assert.Equal("A password is required.", errorResponse.ErrorCode);
        }

        [Fact]
        public void LoadWalletWithFileNotFoundExceptionandReturnsNotFound()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(wallet => wallet.LoadWallet(It.IsAny<string>(), It.IsAny<string>())).Throws<FileNotFoundException>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.Load(new WalletLoadRequest
            {
                Name = "myName",
                FolderPath = "",
                Password = ""
            });

            mockWalletWrapper.VerifyAll();
            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);

            Assert.Equal(404, errorResponse.Status);
            Assert.StartsWith("System.IO.FileNotFoundException", errorResponse.Description);
            Assert.Equal("This wallet was not found at the specified location.", errorResponse.ErrorCode);
        }

        [Fact]
        public void LoadWalletWithSecurityExceptionandReturnsForbidden()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(wallet => wallet.LoadWallet(It.IsAny<string>(), It.IsAny<string>())).Throws<SecurityException>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.Load(new WalletLoadRequest
            {
                Name = "myName",
                FolderPath = "",
                Password = ""
            });

            mockWalletWrapper.VerifyAll();
            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);

            Assert.Equal(403, errorResponse.Status);
            Assert.StartsWith("System.Security.SecurityException", errorResponse.Description);
            Assert.Equal("Wrong password, please try again.", errorResponse.ErrorCode);
        }

        [Fact]
        public void LoadWalletWithOtherExceptionandReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(wallet => wallet.LoadWallet(It.IsAny<string>(), It.IsAny<string>())).Throws<FormatException>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.Load(new WalletLoadRequest
            {
                Name = "myName",
                FolderPath = "",
                Password = ""
            });

            mockWalletWrapper.VerifyAll();
            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);

            Assert.Equal(400, errorResponse.Status);
            Assert.StartsWith("System.FormatException", errorResponse.Description);
        }

        [Fact]
        public void GetGeneralInfoSuccessfullyReturnsWalletGeneralInfoModel()
        {
            Wallet wallet = new Wallet
            {
                Name = "myWallet",
                Network = this.Network,
                CreationTime = new DateTime(2017, 6, 19, 1, 1, 1),
                AccountsRoot = new List<AccountRoot> {
                    new AccountRoot()
                    {
                        CoinType = (CoinType)this.Network.Consensus.CoinType,
                        LastBlockSyncedHeight = 15
                    }
                }
            };

            var concurrentChain = new ConcurrentChain(this.Network);
            ChainedHeader tip = WalletTestsHelpers.AppendBlock(null, new[] { concurrentChain });

            var connectionManagerMock = new Mock<IConnectionManager>();
            connectionManagerMock.Setup(c => c.ConnectedPeers)
                .Returns(new NetworkPeerCollection());

            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.GetWallet("myWallet")).Returns(wallet);           

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, connectionManagerMock.Object, this.Network, concurrentChain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GetGeneralInfo(new WalletName
            {
                Name = "myWallet"
            });

            mockWalletWrapper.VerifyAll();
            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            WalletGeneralInfoModel resultValue = Assert.IsType<WalletGeneralInfoModel>(viewResult.Value);

            Assert.Equal(wallet.Network, resultValue.Network);
            Assert.Equal(wallet.CreationTime, resultValue.CreationTime);
            Assert.Equal(15, resultValue.LastBlockSyncedHeight);
            Assert.Equal(0, resultValue.ConnectedNodes);
            Assert.Equal(tip.Height, resultValue.ChainTip);
            Assert.True(resultValue.IsDecrypted);

        }

        [Fact]
        public void GetGeneralInfoWithModelStateErrorReturnsBadRequest()
        {
            Wallet wallet = new Wallet
            {
                Name = "myWallet",
            };

            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.GetWallet("myWallet")).Returns(wallet);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("Name", "Invalid name.");

            IActionResult result = controller.GetGeneralInfo(new WalletName
            {
                Name = "myWallet"
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);

            Assert.Equal(400, errorResponse.Status);
            Assert.Equal("Invalid name.", errorResponse.ErrorCode);
        }

        [Fact]
        public void GetGeneralInfoWithExceptionReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.GetWallet("myWallet")).Throws<FormatException>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GetGeneralInfo(new WalletName
            {
                Name = "myWallet"
            });

            mockWalletWrapper.VerifyAll();
            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);

            Assert.Equal(400, errorResponse.Status);
            Assert.StartsWith("System.FormatException", errorResponse.Description);
        }

        [Fact]
        public void GetHistoryWithoutAddressesReturnsEmptyModel()
        {
            var walletName = "myWallet";
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.GetHistory(walletName, null)).Returns(new List<AccountHistory>
            {
                new AccountHistory
                {
                    History = new List<FlatHistory>(),
                    Account = new HdAccount()
                }
            });
            mockWalletWrapper.Setup(w => w.GetWalletByName(walletName)).Returns(new Wallet());

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetHistory(new WalletHistoryRequest
            {
                WalletName = walletName
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletHistoryModel;

            Assert.NotNull(model);
            Assert.NotNull(model.AccountsHistoryModel);
            Assert.NotEmpty(model.AccountsHistoryModel);
            Assert.Single(model.AccountsHistoryModel);
            Assert.Empty(model.AccountsHistoryModel.First().TransactionsHistory);
        }

        [Fact]
        public void GetHistoryWithValidModelWithoutTransactionSpendingDetailsReturnsWalletHistoryModel()
        {
            var walletName = "myWallet";
            HdAddress address = WalletTestsHelpers.CreateAddress();
            TransactionData transaction = WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(500000), 1);
            address.Transactions.Add(transaction);

            var addresses = new List<HdAddress> { address };
            Wallet wallet = WalletTestsHelpers.CreateWallet(walletName, this.Network);
            HdAccount account = new HdAccount { ExternalAddresses = addresses };
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                Accounts = new List<HdAccount> { account }
            });

            List<FlatHistory> flat = addresses.SelectMany(s => s.Transactions.Select(t => new FlatHistory { Address = s, Transaction = t })).ToList();

            var accountsHistory = new List<AccountHistory> { new AccountHistory { History = flat, Account = account } };
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.GetHistory(walletName, null)).Returns(accountsHistory);
            mockWalletWrapper.Setup(w => w.GetWalletByName(walletName)).Returns(wallet);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetHistory(new WalletHistoryRequest
            {
                WalletName = walletName
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletHistoryModel;

            Assert.NotNull(model);
            Assert.Single(model.AccountsHistoryModel);

            var historyModel = model.AccountsHistoryModel.ElementAt(0);
            Assert.Single(historyModel.TransactionsHistory);
            TransactionItemModel resultingTransactionModel = historyModel.TransactionsHistory.ElementAt(0);

            Assert.Equal(TransactionItemType.Received, resultingTransactionModel.Type);
            Assert.Equal(address.Address, resultingTransactionModel.ToAddress);
            Assert.Equal(transaction.Id, resultingTransactionModel.Id);
            Assert.Equal(transaction.Amount, resultingTransactionModel.Amount);
            Assert.Equal(transaction.CreationTime, resultingTransactionModel.Timestamp);
            Assert.Equal(1, resultingTransactionModel.ConfirmedInBlock);
        }

        [Fact]
        public void GetHistoryWithValidModelWithTransactionSpendingDetailsReturnsWalletHistoryModel()
        {
            var walletName = "myWallet";
            HdAddress changeAddress = WalletTestsHelpers.CreateAddress(changeAddress: true);
            HdAddress address = WalletTestsHelpers.CreateAddress();
            HdAddress destinationAddress = WalletTestsHelpers.CreateAddress();

            TransactionData changeTransaction = WalletTestsHelpers.CreateTransaction(new uint256(2), new Money(275000), 1);
            changeAddress.Transactions.Add(changeTransaction);

            PaymentDetails paymentDetails = WalletTestsHelpers.CreatePaymentDetails(new Money(200000), destinationAddress);
            SpendingDetails spendingDetails = WalletTestsHelpers.CreateSpendingDetails(changeTransaction, paymentDetails);

            TransactionData transaction = WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(500000), 1, spendingDetails);
            address.Transactions.Add(transaction);

            var addresses = new List<HdAddress> { address, changeAddress };
            Wallet wallet = WalletTestsHelpers.CreateWallet(walletName, this.Network);
            HdAccount account = new HdAccount { ExternalAddresses = addresses };
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                Accounts = new List<HdAccount> { account }
            });

            List<FlatHistory> flat = addresses.SelectMany(s => s.Transactions.Select(t => new FlatHistory { Address = s, Transaction = t })).ToList();
            var accountsHistory = new List<AccountHistory> { new AccountHistory { History = flat, Account = account } };

            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.GetHistory(walletName, null)).Returns(accountsHistory);
            mockWalletWrapper.Setup(w => w.GetWalletByName(walletName)).Returns(wallet);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetHistory(new WalletHistoryRequest
            {
                WalletName = walletName
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletHistoryModel;

            Assert.NotNull(model);
            Assert.Single(model.AccountsHistoryModel);

            var historyModel = model.AccountsHistoryModel.ElementAt(0);
            Assert.Equal(2, historyModel.TransactionsHistory.Count);
            TransactionItemModel resultingTransactionModel = historyModel.TransactionsHistory.ElementAt(0);

            Assert.Equal(TransactionItemType.Received, resultingTransactionModel.Type);
            Assert.Equal(address.Address, resultingTransactionModel.ToAddress);
            Assert.Equal(transaction.Id, resultingTransactionModel.Id);
            Assert.Equal(transaction.Amount, resultingTransactionModel.Amount);
            Assert.Equal(transaction.CreationTime, resultingTransactionModel.Timestamp);
            Assert.Equal(transaction.BlockHeight, resultingTransactionModel.ConfirmedInBlock);
            Assert.Null(resultingTransactionModel.Fee);
            Assert.Equal(0, resultingTransactionModel.Payments.Count);

            resultingTransactionModel = historyModel.TransactionsHistory.ElementAt(1);

            Assert.Equal(TransactionItemType.Sent, resultingTransactionModel.Type);
            Assert.Null(resultingTransactionModel.ToAddress);
            Assert.Equal(spendingDetails.TransactionId, resultingTransactionModel.Id);
            Assert.Equal(spendingDetails.CreationTime, resultingTransactionModel.Timestamp);
            Assert.Equal(spendingDetails.BlockHeight, resultingTransactionModel.ConfirmedInBlock);
            Assert.Equal(paymentDetails.Amount, resultingTransactionModel.Amount);
            Assert.Equal(new Money(25000), resultingTransactionModel.Fee);

            Assert.Equal(1, resultingTransactionModel.Payments.Count);
            PaymentDetailModel resultingPayment = resultingTransactionModel.Payments.ElementAt(0);
            Assert.Equal(paymentDetails.DestinationAddress, resultingPayment.DestinationAddress);
            Assert.Equal(paymentDetails.Amount, resultingPayment.Amount);
        }

        [Fact]
        public void GetHistoryWithValidModelWithFeeBelowZeroSetsFeeToZero()
        {
            var walletName = "myWallet";

            HdAddress changeAddress = WalletTestsHelpers.CreateAddress(changeAddress: true);
            HdAddress address = WalletTestsHelpers.CreateAddress();
            HdAddress destinationAddress = WalletTestsHelpers.CreateAddress();

            TransactionData changeTransaction = WalletTestsHelpers.CreateTransaction(new uint256(2), new Money(310000), 1);
            changeAddress.Transactions.Add(changeTransaction);

            PaymentDetails paymentDetails = WalletTestsHelpers.CreatePaymentDetails(new Money(200000), destinationAddress);
            SpendingDetails spendingDetails = WalletTestsHelpers.CreateSpendingDetails(changeTransaction, paymentDetails);

            TransactionData transaction = WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(500000), 1, spendingDetails);
            address.Transactions.Add(transaction);

            var addresses = new List<HdAddress> { address, changeAddress };

            Wallet wallet = WalletTestsHelpers.CreateWallet(walletName, this.Network);
            HdAccount account = new HdAccount { ExternalAddresses = addresses };
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                Accounts = new List<HdAccount> { account }
            });

            List<FlatHistory> flat = addresses.SelectMany(s => s.Transactions.Select(t => new FlatHistory { Address = s, Transaction = t })).ToList();
            var accountsHistory = new List<AccountHistory> { new AccountHistory { History = flat, Account = account } };

            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.GetHistory(walletName, null)).Returns(accountsHistory);
            mockWalletWrapper.Setup(w => w.GetWalletByName(walletName)).Returns(wallet);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetHistory(new WalletHistoryRequest
            {
                WalletName = walletName
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletHistoryModel;

            Assert.NotNull(model);
            Assert.Single(model.AccountsHistoryModel);

            var historyModel = model.AccountsHistoryModel.ElementAt(0);
            Assert.Equal(2, historyModel.TransactionsHistory.Count);

            TransactionItemModel resultingTransactionModel = historyModel.TransactionsHistory.ElementAt(1);
            Assert.Equal(0, resultingTransactionModel.Fee);
        }

        /// <summary>
        /// Tests that when a transaction has been sent that has multiple inputs to form the transaction these duplicate spending details do not show up multiple times in the history.
        /// </summary>
        [Fact]
        public void GetHistoryWithDuplicateSpentTransactionsSelectsDistinctsSpentTransactionsForDuplicates()
        {
            var walletName = "myWallet";
            var addresses = new List<HdAddress>
            {
                new HdAddress
                {
                    HdPath = $"m/44'/0'/0'/1/0",
                    Transactions = new List<TransactionData>
                    {
                        new TransactionData
                        {
                            Id = new uint256(13),
                            Amount = new Money(50),
                            BlockHeight = 5,
                            SpendingDetails = new SpendingDetails
                            {
                                TransactionId = new uint256(15),
                                BlockHeight = 10,
                                Payments = new List<PaymentDetails>
                                {
                                    new PaymentDetails
                                    {
                                        Amount = new Money(80),
                                        DestinationAddress = "address1"
                                    }
                                }
                            }
                        }
                    }
                },
                new HdAddress
                {
                    HdPath = $"m/44'/0'/0'/1/1",
                    Transactions = new List<TransactionData>
                    {
                        new TransactionData
                        {
                            Id = new uint256(14),
                            Amount = new Money(30),
                            BlockHeight = 6,
                            SpendingDetails = new SpendingDetails
                            {
                                TransactionId = new uint256(15),
                                BlockHeight = 10,
                                Payments = new List<PaymentDetails>
                                {
                                    new PaymentDetails
                                    {
                                        Amount = new Money(80),
                                        DestinationAddress = "address1"
                                    }
                                }
                            }
                        }
                    }
                }
            };

            Wallet wallet = WalletTestsHelpers.CreateWallet(walletName, this.Network);
            HdAccount account = new HdAccount { ExternalAddresses = addresses };
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                Accounts = new List<HdAccount> { account }
            });

            List<FlatHistory> flat = addresses.SelectMany(s => s.Transactions.Select(t => new FlatHistory { Address = s, Transaction = t })).ToList();
            var accountsHistory = new List<AccountHistory> { new AccountHistory { History = flat, Account = account } };

            var mockWalletManager = new Mock<IWalletManager>();
            mockWalletManager.Setup(w => w.GetWalletByName(walletName)).Returns(wallet);
            mockWalletManager.Setup(w => w.GetHistory(walletName, null)).Returns(accountsHistory);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetHistory(new WalletHistoryRequest
            {
                WalletName = walletName
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletHistoryModel;

            Assert.NotNull(model);
            Assert.Single(model.AccountsHistoryModel);

            var historyModel = model.AccountsHistoryModel.ElementAt(0);
            Assert.Single(historyModel.TransactionsHistory);

            TransactionItemModel resultingTransactionModel = historyModel.TransactionsHistory.ElementAt(0);

            Assert.Equal(TransactionItemType.Sent, resultingTransactionModel.Type);
            Assert.Equal(new uint256(15), resultingTransactionModel.Id);
            Assert.Equal(10, resultingTransactionModel.ConfirmedInBlock);
            Assert.Equal(new Money(80), resultingTransactionModel.Amount);

            Assert.Equal(1, resultingTransactionModel.Payments.Count);
            PaymentDetailModel resultingPayment = resultingTransactionModel.Payments.ElementAt(0);
            Assert.Equal("address1", resultingPayment.DestinationAddress);
            Assert.Equal(new Money(80), resultingPayment.Amount);
        }

        [Fact]
        public void GetHistoryWithExceptionReturnsBadRequest()
        {
            var walletName = "myWallet";
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.GetHistory("myWallet", null)).Throws(new InvalidOperationException("Issue retrieving wallets."));
            mockWalletWrapper.Setup(w => w.GetWalletByName(walletName)).Returns(new Wallet());

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetHistory(new WalletHistoryRequest
            {
                WalletName = walletName
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);

            Assert.Equal(400, errorResponse.Status);
            Assert.StartsWith("System.InvalidOperationException", errorResponse.Description);
            Assert.Equal("Issue retrieving wallets.", errorResponse.ErrorCode);
        }

        [Fact]
        public void GetHistoryWithChangeAddressesShouldIncludeSpentChangeAddesses()
        {
            string walletName = "myWallet";

            // create addresses
            HdAddress changeAddress = WalletTestsHelpers.CreateAddress(changeAddress: true);
            HdAddress changeAddress2 = WalletTestsHelpers.CreateAddress(changeAddress: true);
            HdAddress address = WalletTestsHelpers.CreateAddress();
            HdAddress destinationAddress = WalletTestsHelpers.CreateAddress();
            HdAddress destinationAddress2 = WalletTestsHelpers.CreateAddress();

            // create transaction on change address
            TransactionData changeTransaction = WalletTestsHelpers.CreateTransaction(new uint256(2), new Money(275000), 1);
            changeAddress.Transactions.Add(changeTransaction);

            // create transaction with spending details
            PaymentDetails paymentDetails = WalletTestsHelpers.CreatePaymentDetails(new Money(200000), destinationAddress);
            SpendingDetails spendingDetails = WalletTestsHelpers.CreateSpendingDetails(changeTransaction, paymentDetails);
            TransactionData transaction = WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(500000), 1, spendingDetails);
            address.Transactions.Add(transaction);

            // create transaction on change address
            TransactionData changeTransaction2 = WalletTestsHelpers.CreateTransaction(new uint256(4), new Money(200000), 2);
            changeAddress2.Transactions.Add(changeTransaction2);

            // create transaction with spending details on change address
            PaymentDetails paymentDetails2 = WalletTestsHelpers.CreatePaymentDetails(new Money(50000), destinationAddress2);
            SpendingDetails spendingDetails2 = WalletTestsHelpers.CreateSpendingDetails(changeTransaction2, paymentDetails2);
            TransactionData transaction2 = WalletTestsHelpers.CreateTransaction(new uint256(3), new Money(275000), 2, spendingDetails2);
            changeAddress.Transactions.Add(transaction2);

            var addresses = new List<HdAddress> { address, changeAddress, changeAddress2 };
            Wallet wallet = WalletTestsHelpers.CreateWallet(walletName, this.Network);
            HdAccount account = new HdAccount { ExternalAddresses = addresses };
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                Accounts = new List<HdAccount> { account }
            });

            List<FlatHistory> flat = addresses.SelectMany(s => s.Transactions.Select(t => new FlatHistory { Address = s, Transaction = t })).ToList();

            var mockWalletWrapper = new Mock<IWalletManager>();
            var accountsHistory = new List<AccountHistory> { new AccountHistory { History = flat, Account = account } };
            mockWalletWrapper.Setup(w => w.GetHistory(walletName, null)).Returns(accountsHistory);
            mockWalletWrapper.Setup(w => w.GetWalletByName(walletName)).Returns(wallet);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetHistory(new WalletHistoryRequest
            {
                WalletName = walletName
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletHistoryModel;

            Assert.NotNull(model);
            Assert.Single(model.AccountsHistoryModel);

            var historyModel = model.AccountsHistoryModel.ElementAt(0);
            Assert.Equal(3, historyModel.TransactionsHistory.Count);

            TransactionItemModel resultingTransactionModel = historyModel.TransactionsHistory.ElementAt(0);

            Assert.Equal(TransactionItemType.Received, resultingTransactionModel.Type);
            Assert.Equal(address.Address, resultingTransactionModel.ToAddress);
            Assert.Equal(transaction.Id, resultingTransactionModel.Id);
            Assert.Equal(transaction.Amount, resultingTransactionModel.Amount);
            Assert.Equal(transaction.CreationTime, resultingTransactionModel.Timestamp);
            Assert.Equal(transaction.BlockHeight, resultingTransactionModel.ConfirmedInBlock);
            Assert.Null(resultingTransactionModel.Fee);
            Assert.Equal(0, resultingTransactionModel.Payments.Count);

            resultingTransactionModel = historyModel.TransactionsHistory.ElementAt(1);

            Assert.Equal(TransactionItemType.Sent, resultingTransactionModel.Type);
            Assert.Null(resultingTransactionModel.ToAddress);
            Assert.Equal(spendingDetails.TransactionId, resultingTransactionModel.Id);
            Assert.Equal(spendingDetails.CreationTime, resultingTransactionModel.Timestamp);
            Assert.Equal(spendingDetails.BlockHeight, resultingTransactionModel.ConfirmedInBlock);
            Assert.Equal(paymentDetails.Amount, resultingTransactionModel.Amount);
            Assert.Equal(new Money(25000), resultingTransactionModel.Fee);

            Assert.Equal(1, resultingTransactionModel.Payments.Count);
            PaymentDetailModel resultingPayment = resultingTransactionModel.Payments.ElementAt(0);
            Assert.Equal(paymentDetails.DestinationAddress, resultingPayment.DestinationAddress);
            Assert.Equal(paymentDetails.Amount, resultingPayment.Amount);

            resultingTransactionModel = historyModel.TransactionsHistory.ElementAt(2);

            Assert.Equal(TransactionItemType.Sent, resultingTransactionModel.Type);
            Assert.Null(resultingTransactionModel.ToAddress);
            Assert.Equal(spendingDetails2.TransactionId, resultingTransactionModel.Id);
            Assert.Equal(spendingDetails2.CreationTime, resultingTransactionModel.Timestamp);
            Assert.Equal(spendingDetails2.BlockHeight, resultingTransactionModel.ConfirmedInBlock);
            Assert.Equal(paymentDetails2.Amount, resultingTransactionModel.Amount);
            Assert.Equal(new Money(25000), resultingTransactionModel.Fee);

            Assert.Equal(1, resultingTransactionModel.Payments.Count);
            resultingPayment = resultingTransactionModel.Payments.ElementAt(0);
            Assert.Equal(paymentDetails2.DestinationAddress, resultingPayment.DestinationAddress);
            Assert.Equal(paymentDetails2.Amount, resultingPayment.Amount);
        }

        [Fact]
        public void GetBalanceWithValidModelStateReturnsWalletBalanceModel()
        {
            HdAccount account = WalletTestsHelpers.CreateAccount("account 1");
            HdAddress accountAddress1 = WalletTestsHelpers.CreateAddress();
            accountAddress1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(15000), null));
            accountAddress1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(2), new Money(10000), 1));

            HdAddress accountAddress2 = WalletTestsHelpers.CreateAddress();
            accountAddress2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(3), new Money(20000), null));
            accountAddress2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(4), new Money(120000), 2));

            account.ExternalAddresses.Add(accountAddress1);
            account.InternalAddresses.Add(accountAddress2);

            HdAccount account2 = WalletTestsHelpers.CreateAccount("account 2");
            HdAddress account2Address1 = WalletTestsHelpers.CreateAddress();
            account2Address1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(5), new Money(74000), null));
            account2Address1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(6), new Money(18700), 3));

            HdAddress account2Address2 = WalletTestsHelpers.CreateAddress();
            account2Address2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(7), new Money(65000), null));
            account2Address2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(8), new Money(89300), 4));

            account2.ExternalAddresses.Add(account2Address1);
            account2.InternalAddresses.Add(account2Address2);

            var accountsBalances = new List<AccountBalance>
            {
                new AccountBalance { Account = account, AmountConfirmed = new Money(130000), AmountUnconfirmed = new Money(35000) },
                new AccountBalance { Account = account2, AmountConfirmed = new Money(108000), AmountUnconfirmed = new Money(139000) }
            };

            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.GetBalances("myWallet", null)).Returns(accountsBalances);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetBalance(new WalletBalanceRequest
            {
                WalletName = "myWallet"
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletBalanceModel;

            Assert.NotNull(model);
            Assert.Equal(2, model.AccountsBalances.Count);

            AccountBalanceModel resultingBalance = model.AccountsBalances[0];
            Assert.Equal(this.Network.Consensus.CoinType, (int)resultingBalance.CoinType);
            Assert.Equal(account.Name, resultingBalance.Name);
            Assert.Equal(account.HdPath, resultingBalance.HdPath);
            Assert.Equal(new Money(130000), resultingBalance.AmountConfirmed);
            Assert.Equal(new Money(35000), resultingBalance.AmountUnconfirmed);

            resultingBalance = model.AccountsBalances[1];
            Assert.Equal(this.Network.Consensus.CoinType, (int)resultingBalance.CoinType);
            Assert.Equal(account2.Name, resultingBalance.Name);
            Assert.Equal(account2.HdPath, resultingBalance.HdPath);
            Assert.Equal(new Money(108000), resultingBalance.AmountConfirmed);
            Assert.Equal(new Money(139000), resultingBalance.AmountUnconfirmed);
        }

        [Fact]
        public void GetBalanceWithEmptyListOfAccountsReturnsWalletBalanceModel()
        {
            var accounts = new List<HdAccount>();
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.GetAccounts("myWallet"))
                .Returns(accounts);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetBalance(new WalletBalanceRequest
            {
                WalletName = "myWallet"
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletBalanceModel;

            Assert.NotNull(model);
            Assert.Empty(model.AccountsBalances);
        }

        [Fact]
        public void GetBalanceWithInvalidValidModelStateReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("WalletName", "A walletname is required.");
            IActionResult result = controller.GetBalance(new WalletBalanceRequest
            {
                WalletName = ""
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);

            Assert.Equal(400, errorResponse.Status);
            Assert.Equal("A walletname is required.", errorResponse.ErrorCode);
        }

        [Fact]
        public void GetBalanceWithExceptionReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(m => m.GetBalances("myWallet", null))
                  .Throws(new InvalidOperationException("Issue retrieving accounts."));

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetBalance(new WalletBalanceRequest
            {
                WalletName = "myWallet"
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);

            Assert.Equal(400, errorResponse.Status);
            Assert.StartsWith("System.InvalidOperationException", errorResponse.Description);
            Assert.Equal("Issue retrieving accounts.", errorResponse.ErrorCode);
        }

        [Fact]
        public void GetAddressBalanceWithValidModelStateReturnsAddressBalanceModel()
        {
            HdAccount account = WalletTestsHelpers.CreateAccount("account 1");
            HdAddress accountAddress = WalletTestsHelpers.CreateAddress();
            account.InternalAddresses.Add(accountAddress);

            AddressBalance addressBalance = new AddressBalance { Address = accountAddress.Address, AmountConfirmed = new Money(75000), AmountUnconfirmed = new Money(500000) };

            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.GetAddressBalance(accountAddress.Address)).Returns(addressBalance);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetReceivedByAddress(new ReceivedByAddressRequest
            {
                Address = accountAddress.Address
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as AddressBalanceModel;

            Assert.NotNull(model);
            Assert.Equal(this.Network.Consensus.CoinType, (int)model.CoinType);
            Assert.Equal(accountAddress.Address, model.Address);
            Assert.Equal(new Money(75000), model.AmountConfirmed);
            Assert.Equal(new Money(500000), model.AmountUnconfirmed);
        }

        [Fact]
        public void GetAddressBalanceWithExceptionReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(m => m.GetAddressBalance("MyAddress"))
                  .Throws(new InvalidOperationException("Issue retrieving address balance."));

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetReceivedByAddress(new ReceivedByAddressRequest
            {
                Address = "MyAddress"
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);

            Assert.Equal(400, errorResponse.Status);
            Assert.StartsWith("System.InvalidOperationException", errorResponse.Description);
            Assert.Equal("Issue retrieving address balance.", errorResponse.ErrorCode);
        }

        [Fact]
        public void GetAddressBalanceWithInvalidModelStateReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("Address", "An address is required.");
            IActionResult result = controller.GetReceivedByAddress(new ReceivedByAddressRequest
            {
                Address = ""
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);

            Assert.Equal(400, errorResponse.Status);
            Assert.Equal("An address is required.", errorResponse.ErrorCode);
        }

        [Fact]
        public void BuildTransactionWithValidRequestAllowingUnconfirmedReturnsWalletBuildTransactionModel()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            var mockWalletTransactionHandler = new Mock<IWalletTransactionHandler>();
            var key = new Key();
            var sentTrx = new Transaction();
            mockWalletTransactionHandler.Setup(m => m.BuildTransaction(It.IsAny<TransactionBuildContext>()))
                .Returns(sentTrx);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, mockWalletTransactionHandler.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.BuildTransaction(new BuildTransactionRequest
            {
                AccountName = "Account 1",
                AllowUnconfirmed = true,
                Amount = new Money(150000).ToString(),
                DestinationAddress = key.PubKey.GetAddress(this.Network).ToString(),
                FeeType = "105",
                Password = "test",
                WalletName = "myWallet"
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletBuildTransactionModel;

            Assert.NotNull(model);
            Assert.Equal(sentTrx.ToHex(), model.Hex);
            Assert.Equal(sentTrx.GetHash(), model.TransactionId);
        }

        [Fact]
        public void BuildTransactionWithCustomFeeAmountAndFeeTypeReturnsWalletBuildTransactionModelWithFeeAmount()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            var mockWalletTransactionHandler = new Mock<IWalletTransactionHandler>();
            var key = new Key();
            var sentTrx = new Transaction();
            mockWalletTransactionHandler.Setup(m => m.BuildTransaction(It.IsAny<TransactionBuildContext>()))
                .Returns(sentTrx);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, mockWalletTransactionHandler.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.BuildTransaction(new BuildTransactionRequest
            {
                AccountName = "Account 1",
                AllowUnconfirmed = true,
                Amount = new Money(150000).ToString(),
                DestinationAddress = key.PubKey.GetAddress(this.Network).ToString(),
                FeeType = "105",
                FeeAmount = "0.1234",
                Password = "test",
                WalletName = "myWallet"
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletBuildTransactionModel;

            Assert.NotNull(model);
            Assert.Equal(new Money(12340000), model.Fee);
        }

        [Fact]
        public void BuildTransactionWithCustomFeeAmountAndNoFeeTypeReturnsWalletBuildTransactionModelWithFeeAmount()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            var mockWalletTransactionHandler = new Mock<IWalletTransactionHandler>();
            var key = new Key();
            var sentTrx = new Transaction();
            mockWalletTransactionHandler.Setup(m => m.BuildTransaction(It.IsAny<TransactionBuildContext>()))
                .Returns(sentTrx);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, mockWalletTransactionHandler.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.BuildTransaction(new BuildTransactionRequest
            {
                AccountName = "Account 1",
                AllowUnconfirmed = true,
                Amount = new Money(150000).ToString(),
                DestinationAddress = key.PubKey.GetAddress(this.Network).ToString(),

                FeeAmount = "0.1234",
                Password = "test",
                WalletName = "myWallet"
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletBuildTransactionModel;

            Assert.NotNull(model);
            Assert.Equal(new Money(12340000), model.Fee);
        }

        [Fact]
        public void BuildTransactionWithValidRequestNotAllowingUnconfirmedReturnsWalletBuildTransactionModel()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            var mockWalletTransactionHandler = new Mock<IWalletTransactionHandler>();
            var key = new Key();
            var sentTrx = new Transaction();
            mockWalletTransactionHandler.Setup(m => m.BuildTransaction(It.IsAny<TransactionBuildContext>()))
                .Returns(sentTrx);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, mockWalletTransactionHandler.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.BuildTransaction(new BuildTransactionRequest
            {
                AccountName = "Account 1",
                AllowUnconfirmed = false,
                Amount = new Money(150000).ToString(),
                DestinationAddress = key.PubKey.GetAddress(this.Network).ToString(),
                FeeType = "105",
                Password = "test",
                WalletName = "myWallet"
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletBuildTransactionModel;

            Assert.NotNull(model);
            Assert.Equal(sentTrx.ToHex(), model.Hex);
            Assert.Equal(sentTrx.GetHash(), model.TransactionId);
        }

        [Fact]
        public void BuildTransactionWithInvalidModelStateReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("WalletName", "A walletname is required.");
            IActionResult result = controller.BuildTransaction(new BuildTransactionRequest
            {
                WalletName = ""
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);

            Assert.Equal(400, errorResponse.Status);
            Assert.Equal("A walletname is required.", errorResponse.ErrorCode);
        }

        [Fact]
        public void BuildTransactionWithExceptionReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            var mockWalletTransactionHandler = new Mock<IWalletTransactionHandler>();

            var key = new Key();
            mockWalletTransactionHandler.Setup(m => m.BuildTransaction(It.IsAny<TransactionBuildContext>()))
                .Throws(new InvalidOperationException("Issue building transaction."));

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, mockWalletTransactionHandler.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.BuildTransaction(new BuildTransactionRequest
            {
                AccountName = "Account 1",
                AllowUnconfirmed = false,
                Amount = new Money(150000).ToString(),
                DestinationAddress = key.PubKey.GetAddress(this.Network).ToString(),
                FeeType = "105",
                Password = "test",
                WalletName = "myWallet"
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);

            Assert.Equal(400, errorResponse.Status);
            Assert.StartsWith("System.InvalidOperationException", errorResponse.Description);
            Assert.Equal("Issue building transaction.", errorResponse.ErrorCode);
        }
        /// <summary>
        /// This explicitly need BRhodiumMainNet NOT BRhodiumRegTest config.
        /// </summary>
        [Fact]
        public void SendTransactionSuccessfulReturnsWalletSendTransactionModelResponse()
        {
            string transactionHex = "010000000189c041f79aac3aa7e7a72804a9a55cd9eceba41a0586640f602eb9823540ce89010000006b483045022100ab9597b37cb8796aefa30b207abb248c8003d4d153076997e375b0daf4f9f7050220546397fee1cefe54c49210ea653e9e61fb88adf51b68d2c04ad6d2b46ddf97a30121035cc9de1f233469dad8a3bbd1e61b699a7dd8e0d8370c6f3b1f2a16167da83546ffffffff02f6400a00000000001976a914accf603142aaa5e22dc82500d3e187caf712f11588ac3cf61700000000001976a91467872601dda216fbf4cab7891a03ebace87d8e7488ac00000000";

            var mockWalletWrapper = new Mock<IBroadcasterManager>();
            var connectionManagerMock = new Mock<IConnectionManager>();
            var peers = new List<INetworkPeer>();
            peers.Add(null);
            connectionManagerMock.Setup(c => c.ConnectedPeers).Returns(new TestReadOnlyNetworkPeerCollection(peers));

            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object,
                new Mock<IWalletSyncManager>().Object, connectionManagerMock.Object, Network.BRhodiumMain, new Mock<ConcurrentChain>().Object, mockWalletWrapper.Object, DateTimeProvider.Default);
            IActionResult result = controller.SendTransaction(new SendTransactionRequest(transactionHex));

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletSendTransactionModel;
            Assert.NotNull(model);
            Assert.Equal(new uint256("96b4f0c2f0aa2cecd43fa66b5e3227c56afd8791e18fcc572d9625ee05d6741c"), model.TransactionId);
            Assert.Equal("RpNXiLdgr6h5GpqLJe6uvskCj5LF6mLUt9", model.Outputs.First().Address);
            Assert.Equal(new Money(671990), model.Outputs.First().Amount);
            Assert.Equal("Ri4CZfRL4QHS6GLKmkp7RvaVFDf3HP1LyB", model.Outputs.ElementAt(1).Address);
            Assert.Equal(new Money(1570364), model.Outputs.ElementAt(1).Amount);
        }

        [Fact]
        public void SendTransactionFailedBecauseNoNodesConnected()
        {
            var mockWalletWrapper = new Mock<IBroadcasterManager>();

            var connectionManagerMock = new Mock<IConnectionManager>();
            connectionManagerMock.Setup(c => c.ConnectedPeers)
                .Returns(new NetworkPeerCollection());

            WalletController controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object,
                new Mock<IWalletSyncManager>().Object, connectionManagerMock.Object, this.Network, new Mock<ConcurrentChain>().Object, mockWalletWrapper.Object, DateTimeProvider.Default);

            bool walletExceptionOccurred = false;

            try
            {
                controller.SendTransaction(new SendTransactionRequest(new uint256(15555).ToString()));
            }
            catch (WalletException)
            {
                walletExceptionOccurred = true;
            }

            Assert.True(walletExceptionOccurred);
        }

        [Fact]
        public void SendTransactionWithInvalidModelStateReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("Hex", "Hex required.");
            IActionResult result = controller.SendTransaction(new SendTransactionRequest(""));

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);

            Assert.Equal(400, errorResponse.Status);
            Assert.Equal("Hex required.", errorResponse.ErrorCode);
        }

        //[Fact]
        //public void ListWalletFilesWithExistingWalletFilesReturnsWalletFileModel()
        //{
        //    string walletPath = "walletPath";
        //    var walletManager = new Mock<IWalletManager>();
        //    walletManager.Setup(m => m.GetWalletsFiles())
        //        .Returns((walletPath, new[] { "wallet1.wallet.json", "wallet2.wallet.json" }));

        //    walletManager.Setup(m => m.GetWalletFileExtension()).Returns("wallet.json");

        //    var controller = new WalletController(this.LoggerFactory.Object, walletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

        //    IActionResult result = controller.ListWalletsFiles();

        //    JsonResult viewResult = Assert.IsType<JsonResult>(result);
        //    var model = viewResult.Value as WalletFileModel;

        //    Assert.NotNull(model);
        //    Assert.Equal(walletPath, model.WalletsPath);
        //    Assert.Equal(2, model.WalletsFiles.Count());
        //    Assert.EndsWith("wallet1.wallet.json", model.WalletsFiles.ElementAt(0));
        //    Assert.EndsWith("wallet2.wallet.json", model.WalletsFiles.ElementAt(1));
        //}

        //[Fact]
        //public void ListWalletFilesWithoutExistingWalletFilesReturnsWalletFileModel()
        //{
        //    string walletPath = "walletPath";
        //    var walletManager = new Mock<IWalletManager>();
        //    walletManager.Setup(m => m.GetWalletsFiles())
        //        .Returns((walletPath, Enumerable.Empty<string>()));

        //    var controller = new WalletController(this.LoggerFactory.Object, walletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

        //    IActionResult result = controller.ListWalletsFiles();

        //    JsonResult viewResult = Assert.IsType<JsonResult>(result);
        //    var model = viewResult.Value as WalletFileModel;

        //    Assert.NotNull(model);
        //    Assert.Equal(walletPath, model.WalletsPath);
        //    Assert.Empty(model.WalletsFiles);
        //}

        //[Fact]
        //public void ListWalletFilesWithExceptionReturnsBadRequest()
        //{
        //    var walletManager = new Mock<IWalletManager>();
        //    walletManager.Setup(m => m.GetWalletsFiles())
        //        .Throws(new Exception("something happened."));

        //    var controller = new WalletController(this.LoggerFactory.Object, walletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

        //    IActionResult result = controller.ListWalletsFiles();

        //    ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
        //    ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);

        //    Assert.Equal(400, errorResponse.Status);
        //    Assert.Equal("something happened.", errorResponse.ErrorCode);
        //}

        [Fact]
        public void CreateNewAccountWithValidModelReturnsAccountName()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(m => m.GetUnusedAccount("myWallet", "test"))
                .Returns(new HdAccount { Name = "Account 1" });

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.CreateNewAccount(new GetUnusedAccountModel
            {
                WalletName = "myWallet",
                Password = "test"
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            Assert.Equal("Account 1", viewResult.Value as string);
        }

        [Fact]
        public void CreateNewAccountWithInvalidValidModelReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("Password", "A password is required.");

            IActionResult result = controller.CreateNewAccount(new GetUnusedAccountModel
            {
                WalletName = "myWallet",
                Password = ""
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);

            Assert.Equal(400, errorResponse.Status);
            Assert.Equal("A password is required.", errorResponse.ErrorCode);
        }

        [Fact]
        public void CreateNewAccountWithExceptionReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(m => m.GetUnusedAccount("myWallet", "test"))
                .Throws(new InvalidOperationException("Wallet not found."));

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.CreateNewAccount(new GetUnusedAccountModel
            {
                WalletName = "myWallet",
                Password = "test"
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);

            Assert.Equal(400, errorResponse.Status);
            Assert.StartsWith("System.InvalidOperationException", errorResponse.Description);
            Assert.StartsWith("Wallet not found.", errorResponse.ErrorCode);
        }

        [Fact]
        public void ListAccountsWithValidModelStateReturnsAccounts()
        {
            var walletName = "wallet 1";
            Wallet wallet = WalletTestsHelpers.CreateWallet(walletName, this.Network);
            wallet.AccountsRoot.Clear();
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                Accounts = new List<HdAccount>()
                {
                    new HdAccount
                    {
                        Name = "account 0"
                    },
                    new HdAccount
                    {
                        Name = "account 1"
                    }
                }
            });

            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(m => m.GetAccounts(walletName)).Returns(wallet.AccountsRoot.SelectMany(x => x.Accounts));

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.ListAccounts(new ListAccountsModel
            {
                WalletName = "wallet 1"
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as IEnumerable<string>;

            Assert.NotNull(model);
            Assert.Equal(2, model.Count());
            Assert.Equal("account 0", model.First());
            Assert.Equal("account 1", model.Last());
        }

        [Fact]
        public void ListAccountsWithInvalidModelReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("WalletName", "A wallet name is required.");

            IActionResult result = controller.ListAccounts(new ListAccountsModel
            {
                WalletName = ""
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);

            Assert.Equal(400, errorResponse.Status);
            Assert.Equal("A wallet name is required.", errorResponse.ErrorCode);
        }

        [Fact]
        public void ListAccountsWithExceptionReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(m => m.GetAccounts("wallet 0"))
                .Throws(new InvalidOperationException("Wallet not found."));

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.ListAccounts(new ListAccountsModel
            {
                WalletName = "wallet 0",
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);

            Assert.Equal(400, errorResponse.Status);
            Assert.StartsWith("System.InvalidOperationException", errorResponse.Description);
            Assert.StartsWith("Wallet not found.", errorResponse.ErrorCode);
        }

        [Fact]
        public void GetUnusedAddressWithValidModelReturnsUnusedAddress()
        {
            HdAddress address = WalletTestsHelpers.CreateAddress();
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(m => m.GetUnusedAddress(new WalletAccountReference("myWallet", "Account 1")))
                .Returns(address);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetUnusedAddress(new GetUnusedAddressModel
            {
                WalletName = "myWallet",
                AccountName = "Account 1"
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            Assert.Equal(address.Address, viewResult.Value as string);
        }

        [Fact]
        public void GetUnusedAddressWithInvalidValidModelReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("AccountName", "An account name is required.");

            IActionResult result = controller.GetUnusedAddress(new GetUnusedAddressModel
            {
                WalletName = "myWallet",
                AccountName = ""
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);

            Assert.Equal(400, errorResponse.Status);
            Assert.Equal("An account name is required.", errorResponse.ErrorCode);
        }

        [Fact]
        public void GetUnusedAddressWithExceptionReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(m => m.GetUnusedAddress(new WalletAccountReference("myWallet", "Account 1")))
                .Throws(new InvalidOperationException("Wallet not found."));

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetUnusedAddress(new GetUnusedAddressModel
            {
                WalletName = "myWallet",
                AccountName = "Account 1"
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);

            Assert.Equal(400, errorResponse.Status);
            Assert.StartsWith("System.InvalidOperationException", errorResponse.Description);
            Assert.StartsWith("Wallet not found.", errorResponse.ErrorCode);
        }

        [Fact]
        public void GetAllAddressesWithValidModelReturnsAllAddresses()
        {
            var walletName = "myWallet";
            var walletPassword = Guid.NewGuid().ToString();

            Wallet wallet = WalletTestsHelpers.GenerateBlankWallet(walletName, walletPassword, this.Network);

            // Receive address with a transaction
            HdAddress usedReceiveAddress = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
            TransactionData receiveTransaction = WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(500000), 1);
            usedReceiveAddress.Transactions.Add(receiveTransaction);

            // Receive address without a transaction
            HdAddress unusedReceiveAddress = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1);

            // Change address with a transaction
            HdAddress usedChangeAddress = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0);
            TransactionData changeTransaction = WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(500000), 1);
            usedChangeAddress.Transactions.Add(changeTransaction);

            // Change address without a transaction
            HdAddress unusedChangeAddress = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(1);

            var receiveAddresses = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses;
            var changeAddresses = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses;


            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(m => m.GetWallet(walletName)).Returns(wallet);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetAllAddresses(new GetAllAddressesModel { WalletName = "myWallet", AccountName = "account 0" });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as AddressesModel;

            Assert.NotNull(model);
            Assert.Equal(40, model.Addresses.Count());

            var modelUsedReceiveAddress = model.Addresses.Single(a => a.Address == usedReceiveAddress.Address);
            Assert.Equal(modelUsedReceiveAddress.Address, model.Addresses.Single(a => a.Address == modelUsedReceiveAddress.Address).Address);
            Assert.False(model.Addresses.Single(a => a.Address == modelUsedReceiveAddress.Address).IsChange);
            Assert.True(model.Addresses.Single(a => a.Address == modelUsedReceiveAddress.Address).IsUsed);

            var modelUnusedReceiveAddress = model.Addresses.Single(a => a.Address == unusedReceiveAddress.Address);
            Assert.Equal(modelUnusedReceiveAddress.Address, model.Addresses.Single(a => a.Address == modelUnusedReceiveAddress.Address).Address);
            Assert.False(model.Addresses.Single(a => a.Address == modelUnusedReceiveAddress.Address).IsChange);
            Assert.False(model.Addresses.Single(a => a.Address == modelUnusedReceiveAddress.Address).IsUsed);

            var modelUsedChangeAddress = model.Addresses.Single(a => a.Address == usedChangeAddress.Address);
            Assert.Equal(modelUsedChangeAddress.Address, model.Addresses.Single(a => a.Address == modelUsedChangeAddress.Address).Address);
            Assert.True(model.Addresses.Single(a => a.Address == modelUsedChangeAddress.Address).IsChange);
            Assert.True(model.Addresses.Single(a => a.Address == modelUsedChangeAddress.Address).IsUsed);

            var modelUnusedChangeAddress = model.Addresses.Single(a => a.Address == unusedChangeAddress.Address);
            Assert.Equal(modelUnusedChangeAddress.Address, model.Addresses.Single(a => a.Address == modelUnusedChangeAddress.Address).Address);
            Assert.True(model.Addresses.Single(a => a.Address == modelUnusedChangeAddress.Address).IsChange);
            Assert.False(model.Addresses.Single(a => a.Address == modelUnusedChangeAddress.Address).IsUsed);
        }

        [Fact]
        public void GetMaximumBalanceWithValidModelStateReturnsMaximumBalance()
        {
            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("Error in model", "There was an error in the model.");
            IActionResult result = controller.GetMaximumSpendableBalance(new WalletMaximumBalanceRequest
            {
                WalletName = "myWallet",
                AccountName = "account 1",
                FeeType = "low",
                AllowUnconfirmed = true
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);

            Assert.NotNull(errorResult.StatusCode);
            Assert.Equal((int)HttpStatusCode.BadRequest, errorResult.StatusCode.Value);
            Assert.Equal("There was an error in the model.", errorResponse.ErrorCode);
        }

        [Fact]
        public void GetMaximumBalanceSuccessfullyReturnsMaximumBalanceAndFee()
        {
            var mockWalletTransactionHandler = new Mock<IWalletTransactionHandler>();
            mockWalletTransactionHandler.Setup(w => w.GetMaximumSpendableAmount(It.IsAny<WalletAccountReference>(), It.IsAny<FeeType>(), true)).Returns((new Money(1000000), new Money(100)));

            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, mockWalletTransactionHandler.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetMaximumSpendableBalance(new WalletMaximumBalanceRequest
            {
                WalletName = "myWallet",
                AccountName = "account 1",
                FeeType = "low",
                AllowUnconfirmed = true
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as MaxSpendableAmountModel;

            Assert.NotNull(model);
            Assert.Equal(new Money(1000000), model.MaxSpendableAmount);
            Assert.Equal(new Money(100), model.Fee);
        }

        [Fact]
        public void GetMaximumBalanceWithExceptionReturnsBadRequest()
        {
            var mockWalletTransactionHandler = new Mock<IWalletTransactionHandler>();
            mockWalletTransactionHandler.Setup(w => w.GetMaximumSpendableAmount(It.IsAny<WalletAccountReference>(), It.IsAny<FeeType>(), true)).Throws(new Exception("failure"));

            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, mockWalletTransactionHandler.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetMaximumSpendableBalance(new WalletMaximumBalanceRequest
            {
                WalletName = "myWallet",
                AccountName = "account 1",
                FeeType = "low",
                AllowUnconfirmed = true
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);

            Assert.NotNull(errorResult.StatusCode);
            Assert.Equal((int)HttpStatusCode.BadRequest, errorResult.StatusCode.Value);
        }

        [Fact]
        public void GetTransactionFeeEstimateWithValidRequestReturnsFee()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            var mockWalletTransactionHandler = new Mock<IWalletTransactionHandler>();
            Key key = new Key();
            Money expectedFee = new Money(1000);
            mockWalletTransactionHandler.Setup(m => m.EstimateFee(It.IsAny<TransactionBuildContext>()))
                .Returns(expectedFee);

            WalletController controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, mockWalletTransactionHandler.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, new Mock<ConcurrentChain>().Object, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetTransactionFeeEstimate(new TxFeeEstimateRequest
            {
                AccountName = "Account 1",
                Amount = new Money(150000).ToString(),
                DestinationAddress = key.PubKey.GetAddress(this.Network).ToString(),
                FeeType = "105",
                WalletName = "myWallet"
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            Money actualFee = viewResult.Value as Money;

            Assert.NotNull(actualFee);
            Assert.Equal(expectedFee, actualFee);
        }

        [Fact]
        public void RemoveAllTransactionsWithSyncEnabledSyncsAfterRemoval()
        {
            // Arrange.
            string walletName = "wallet1";
            Wallet wallet = WalletTestsHelpers.CreateWallet(walletName, this.Network);
            wallet.AccountsRoot.Add(new AccountRoot());
            uint256 trxId1 = uint256.Parse("d6043add63ec364fcb591cf209285d8e60f1cc06186d4dcbce496cdbb4303400");
            uint256 trxId2 = uint256.Parse("a3dd63ec364fcb59043a1cf209285d8e60f1cc06186d4dcbce496cdbb4303401");
            HashSet<(uint256 trxId, DateTimeOffset creationTime)> resultModel = new HashSet<(uint256 trxId, DateTimeOffset creationTime)>();
            resultModel.Add((trxId1, DateTimeOffset.Now));
            resultModel.Add((trxId2, DateTimeOffset.Now));

            var walletManager = new Mock<IWalletManager>();
            var walletSyncManager = new Mock<IWalletSyncManager>();
            walletManager.Setup(manager => manager.RemoveAllTransactions(walletName)).Returns(resultModel);
            walletManager.Setup(manager => manager.GetWallet(walletName)).Returns(wallet);
            walletSyncManager.Setup(manager => manager.SyncFromHeight(It.IsAny<int>()));
            ConcurrentChain chain = WalletTestsHelpers.GenerateChainWithHeight(3, this.Network);

            var controller = new WalletController(this.LoggerFactory.Object, walletManager.Object, new Mock<IWalletTransactionHandler>().Object, walletSyncManager.Object, It.IsAny<ConnectionManager>(), this.Network, chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            RemoveTransactionsModel requestModel = new RemoveTransactionsModel
            {
                WalletName = walletName,
                ReSync = true,
                DeleteAll = true
            };

            // Act.
            IActionResult result = controller.RemoveTransactions(requestModel);

            // Assert.
            walletManager.VerifyAll();
            walletSyncManager.Verify(manager => manager.SyncFromHeight(It.IsAny<int>()), Times.Once);

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as IEnumerable<RemovedTransactionModel>;
            Assert.NotNull(model);
            Assert.Equal(2, model.Count());
            Assert.True(model.SingleOrDefault(t => t.TransactionId == trxId1) != null);
            Assert.True(model.SingleOrDefault(t => t.TransactionId == trxId2) != null);
        }

        [Fact]
        public void RemoveAllTransactionsWithSyncDisabledDoesNotSyncAfterRemoval()
        {
            // Arrange.
            string walletName = "wallet1";
            uint256 trxId1 = uint256.Parse("d6043add63ec364fcb591cf209285d8e60f1cc06186d4dcbce496cdbb4303400");
            uint256 trxId2 = uint256.Parse("a3dd63ec364fcb59043a1cf209285d8e60f1cc06186d4dcbce496cdbb4303401");
            HashSet<(uint256 trxId, DateTimeOffset creationTime)> resultModel = new HashSet<(uint256 trxId, DateTimeOffset creationTime)>();
            resultModel.Add((trxId1, DateTimeOffset.Now));
            resultModel.Add((trxId2, DateTimeOffset.Now));

            var walletManager = new Mock<IWalletManager>();
            var walletSyncManager = new Mock<IWalletSyncManager>();
            walletManager.Setup(manager => manager.RemoveAllTransactions(walletName)).Returns(resultModel);
            ConcurrentChain chain = WalletTestsHelpers.GenerateChainWithHeight(3, this.Network);

            var controller = new WalletController(this.LoggerFactory.Object, walletManager.Object, new Mock<IWalletTransactionHandler>().Object, walletSyncManager.Object, It.IsAny<ConnectionManager>(), this.Network, chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            RemoveTransactionsModel requestModel = new RemoveTransactionsModel
            {
                WalletName = walletName,
                ReSync = false,
                DeleteAll = true
            };

            // Act.
            IActionResult result = controller.RemoveTransactions(requestModel);

            // Assert.
            walletManager.VerifyAll();
            walletSyncManager.Verify(manager => manager.SyncFromHeight(It.IsAny<int>()), Times.Never);

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as IEnumerable<RemovedTransactionModel>;
            Assert.NotNull(model);
            Assert.Equal(2, model.Count());
            Assert.True(model.SingleOrDefault(t => t.TransactionId == trxId1) != null);
            Assert.True(model.SingleOrDefault(t => t.TransactionId == trxId2) != null);
        }

        [Fact]
        public void RemoveTransactionsWithIdsRemovesAllTransactionsByIds()
        {
            // Arrange.
            string walletName = "wallet1";
            Wallet wallet = WalletTestsHelpers.CreateWallet(walletName, this.Network);
            wallet.AccountsRoot.Add(new AccountRoot());
            uint256 trxId1 = uint256.Parse("d6043add63ec364fcb591cf209285d8e60f1cc06186d4dcbce496cdbb4303400");
            HashSet<(uint256 trxId, DateTimeOffset creationTime)> resultModel = new HashSet<(uint256 trxId, DateTimeOffset creationTime)>();
            resultModel.Add((trxId1, DateTimeOffset.Now));

            var walletManager = new Mock<IWalletManager>();
            var walletSyncManager = new Mock<IWalletSyncManager>();
            walletManager.Setup(manager => manager.RemoveTransactionsByIds(walletName, new[] { trxId1 })).Returns(resultModel);
            walletManager.Setup(manager => manager.GetWallet(walletName)).Returns(wallet);
            walletSyncManager.Setup(manager => manager.SyncFromHeight(It.IsAny<int>()));
            ConcurrentChain chain = WalletTestsHelpers.GenerateChainWithHeight(3, this.Network);

            var controller = new WalletController(this.LoggerFactory.Object, walletManager.Object, new Mock<IWalletTransactionHandler>().Object, walletSyncManager.Object, It.IsAny<ConnectionManager>(), this.Network, chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            RemoveTransactionsModel requestModel = new RemoveTransactionsModel
            {
                WalletName = walletName,
                ReSync = true,
                TransactionsIds = new[] { "d6043add63ec364fcb591cf209285d8e60f1cc06186d4dcbce496cdbb4303400" }
            };

            // Act.
            IActionResult result = controller.RemoveTransactions(requestModel);

            // Assert.
            walletManager.VerifyAll();
            walletManager.Verify(manager => manager.RemoveAllTransactions(It.IsAny<string>()), Times.Never);
            walletSyncManager.Verify(manager => manager.SyncFromHeight(It.IsAny<int>()), Times.Once);

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as IEnumerable<RemovedTransactionModel>;
            Assert.NotNull(model);
            Assert.Single(model);
            Assert.True(model.SingleOrDefault(t => t.TransactionId == trxId1) != null);
        }
    }

    public class TestReadOnlyNetworkPeerCollection : IReadOnlyNetworkPeerCollection
    {
        public event EventHandler<NetworkPeerEventArgs> Added;
        public event EventHandler<NetworkPeerEventArgs> Removed;

        private List<INetworkPeer> networkPeers;

        public TestReadOnlyNetworkPeerCollection()
        {
            this.Added = new EventHandler<NetworkPeerEventArgs>((obj, eventArgs) => { });
            this.Removed = new EventHandler<NetworkPeerEventArgs>((obj, eventArgs) => { });
            this.networkPeers = new List<INetworkPeer>();
        }

        public TestReadOnlyNetworkPeerCollection(List<INetworkPeer> peers)
        {
            this.Added = new EventHandler<NetworkPeerEventArgs>((obj, eventArgs) => { });
            this.Removed = new EventHandler<NetworkPeerEventArgs>((obj, eventArgs) => { });
            this.networkPeers = peers;
        }

        public INetworkPeer FindByEndpoint(IPEndPoint endpoint)
        {
            return null;
        }

        public INetworkPeer FindByIp(IPAddress ip)
        {
            return null;
        }

        public INetworkPeer FindLocal()
        {
            return null;
        }

        public IEnumerator<INetworkPeer> GetEnumerator()
        {
            return this.networkPeers.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}