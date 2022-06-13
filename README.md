xRhodium (XRC)
===========================================

Project web site: [xRhodium](https://xrhodium.org)

Current version: 1.1.30 ([Gitlab link](https://github.com/XRCPlatform/xrhodiumnode/tree/master_1.1.30))


## About xRhodium

xRhodium is a unique crypto commodity with limited supply and strong use case. Its store-of-value qualities are further supported by a set of features that meet the demand for a long-term crypto investment, incentivize strong-hand investor behaviour and bridge the gap between the world of crypto and traditional investment.


## X11

X11’s chained hashing algorithm utilizes a sequence of eleven scientific hashing algorithms for the proof-of-work. This is so that the processing distribution is fair and coins will be distributed in much the same way Bitcoin’s were originally. X11 was intended to make ASICs much more difficult to create, thus giving the currency plenty of time to develop before mining centralization became a threat. This approach was largely successful; as of early 2016, ASICs for X11 now exist and comprise a significant portion of the network hashrate, but have not resulted in the level of centralization present in Bitcoin.

X11 is the name of the chained Proof-of-work (PoW) algorithm that was introduced in Dash (launched January 2014 as “Xcoin”). X11 is an algorithm for mining cryptocurrency which uses 11 different hash functions. X11 was well received by the mining community due to its energy-efficiency when mining with a home rig. It isn't a secret that X11 is more complicated than a SHA​-256 algorithm, which prevented the use of ASIC miners for a time. 

To do this, Evan Duffield combined 11 different hash functions in one algorithm: Blake, BMW, Groestl, JH, Keccak, Skein, Luffa, Cubehash, Shavite, Simd, Echo.

The main feature of cryptocurrency mining on X11 is financial profitability in comparison with other algorithms. Mining efficiency is expressed in three components:

- Performance.
- Minimal costs for payment of electricity bills.
- The cost of altcoins.

**The advantages of the X11 algorithm include:**

- Security. Most cryptographic algorithms used in cryptocurrencies use only one hash function for calculation. There are 11 of them in X11, which provides a higher degree of protection against hackers and scams.

- More secure than Bitcoin. The Bitcoin algorithm is SHA-256 is based on a previous secure hash algorithm family of standards, namely SHA-2, the hash functions within the X11 algorithm all successfully made it into the second-round in search for a new, more secure standard – SHA-3. Keccak, the function which won the competition and is therefore the new standard on which SHA-3 is based on, can at the very least be considered more secure that SHA-256.

- Loyalty. As practice shows, the production of coins on the algorithm X11 requires less energy costs and does not overload the equipment so much. For example, when working with AMD graphics cards, the power consumption is reduced by 40-50%.


## POW XRC DigiShield

DigiShield re-targets a coin’s difficulty to protect against multi-pools and an over-inflation of easily mined new coins. DigiShield was created to account for such wild fluctuations, so that the blockchain doesn't "freeze" when a large exodus of hash power occurs. It also means miners cannot flood a few consecutive blocks with a high amount of hash power and benefit from low difficulty, giving blocks near instantly one after another before traditional difficulty retargeting occurs. 

XRC DigiShield is set to target 10 minutes block time. It will try every next block to set right difficult for next block to reach 10 minutes block time.

**How calculation works (it is using 50 blocks window):**
1) We have block height for calculation(B).
a) We get block time of first block in window (BF) ( = B - 50)
b) We get block time of last block in window (BL) ( = B - 1)
2) We will get block time window (10) of first and last in first block
a) We get block time of first block in window (BFF) ( = BF - 10)
b) We get block time of last block in window (BFL) ( = BF)
c) BFL - BFF = averagetime of first block (BAF)
3) We will get block time window (10) of first and last in last block
a) We get block time of first block in window (BLF) ( = BL - 10)
b) We get block time of last block in window (BLL) ( = BL)
c) BLL - BLF = averagetime of last block (BAL)
4) check difference BAL - BAF = BDIFF
5) if BDIFF > 50 * 10(min) = difficult-
5) if BDIFF < 50 * 10(min) = difficult+

Difference between original and XRC DigiShield is that XRC is using average time between first window (2) and last block window (3).


## About xRhodiumNode

xRhodiumNode is the full node for xRhodium. It is developed in C#, using the .NET Core platform.

[.NET Core](https://dotnet.microsoft.com/en-us/) is an open source cross platform framework and enables the development of applications and services on Windows, macOS and Linux.

Join our community on [Discord, Telegram, Twitter, ..](https://www.xrhodium.org/En/Community).

## Installation and setup

.NET Core is required to build and run the node software. The installation and setup notes below have been tested on Ubuntu 16.04+. There is a convenience wrapper around most processes is provided to make setup quick.

**Follow full installation process at https://github.com/XRCPlatform/xrhodiumnode/wiki.**

 1. Clone the repository:

```
    git clone -b master_1.1.30 https://github.com/XRCPlatform/xrhodiumnode.git
    cd xrhodiumnode
```

The `master` branch is bleeding-edge. Use this at your own risk.

 2. **Install .NET Core (dotnet-sdk-3.1)**. Follow instructions here: 
 https://docs.microsoft.com/en-us/dotnet/core/install/linux.

3. To build the node:

```
$ cd src/BRhodium
$ dotnet restore
$ dotnet build
```

 4. Start a new node. By default, blockchain data, wallet data and configuration files is stored under `$HOME/.brhodium` by default. You can start the node using your build with:
 ```
     dotnet run
 ```

 This will start the node software and try to start downloading the mainnet chain. Currently, the blockchain is small, so a full download will likely not take that long.

 5. If you want to run a testnet node:

 ```
     dotnet run -testnet
 ```

## Further Information

Documentation is available at https://github.com/XRCPlatform/xrhodiumnode/wiki.

## Legal

See LICENSE for details.
