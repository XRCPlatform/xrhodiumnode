Bitcoin Rhodium (XRC) - BRhodium, BitcoinRh
===========================================

Project web site: [Bitcoin Rhodium](https://www.bitcoinrh.org)

Current version: 1.1.8 ([Gitlab link](https://gitlab.com/bitcoinrh/BRhodiumNode/tree/master_1.1.8))

## About Bitcoin Rhodium

Bitcoin Rhodium is a unique crypto commodity with limited supply and strong use case. Its store-of-value qualities are further supported by a set of features that meet the demand for a long-term crypto investment, incentivize strong-hand investor behaviour and bridge the gap between the world of crypto and traditional investment.

## About BRhodiumNode

BRhodiumNode is the full node for Bitcoin Rhodium. It is developed in C#, using the .NET Core platform.

[.NET Core](https://dotnet.github.io/) is an open source cross platform framework and enables the development of applications and services on Windows, macOS and Linux.

Join our community on [Discord](https://t.co/ns9nldLSrv).

## Installation and setup

.NET Core is required to build and run the node software. The installation and setup notes below have been tested on Ubuntu 16.04+. There is a convenience wrapper around most processes is provided to make setup quick.

 1. Clone the repository:

```
    git clone -b master_1.1.8 https://gitlab.com/bitcoinrh/BRhodiumNode.git
    cd BRhodiumNode
```

The `master` branch is bleeding-edge. Use this at your own risk.

 2. Install .NET Core. Follow instructions here: https://docs.microsoft.com/en-us/dotnet/core/linux-prerequisites?tabs=netcore2x.
 3. Build BRhodium. The `bin/brhodium` script contains the required steps to do a build:

 ```
     bin/brhodium build
 ```

 It will create a `build/` directory at the root of the `BRhodium` directory.

You can also do a build or run using the `dotnet` tools. To run the node:

```
$ cd src/BRhodium
$ dotnet restore
$ dotnet run # or dotnet build
```

 4. Start a new node. By default, blockchain data, wallet data and configuration files is stored under `$HOME/.brhodium` by default. You can start the node using your build with:
 ```
     bin/brhodium node
 ```

 This will start the node software and try to start downloading the mainnet chain. Currently, the blockchain is small, so a full download will likely not take that long.

 5. If you want to run a testnet node:

 ```
     bin/brhodium node -testnet
 ```

 6. You can get list of commands using:

 ```
     bin/brhodium help
 ```

## RPC Commands

The node implements a number of RPC commands, similar to Bitcoin's RPC interface. As a convenience, the `bin/brhodium` script provides a useful command-line interface.

RPC commands can be executed using the `rpc` option, along with the RPC name and parameters:

```
    $ bin/brhodium rpc getblockhash 0
    baff5bfd9dc43fb672d003ec20fd21428f9282ca46bfa1730d73e1f2c75f5fdd
```

## Further Information

Documentation is available at http://wiki.bitcoinrh.org/.

## Legal

See LICENSE for details.
