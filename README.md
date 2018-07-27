| Windows | Linux | OS X
| :---- | :------ | :---- |

Bitcoin Rhodium (BTR) - BRhodium, BitcoinRh
===========================================

Project web site: [Bitcoin Rhodium](https://www.bitcoinrh.org)

Bitcoin Implementation in C#
----------------------------

Bitcoin Rhodium is an implementation of the Bitcoin in C# on the [.NET Core] platform. Node allow to run BRhodium Node. 
Code is based on [NBitcoin](https://github.com/MetacoSA/NBitcoin) ans [Stratis](https://github.com/stratisproject/StratisBitcoinFullNode) project.  

Bitcoin Rhodium using POW algo only.

[.NET Core](https://dotnet.github.io/) is an open source cross platform framework and enables the development of applications and services on Windows, macOS and Linux.  

Join our community on [Discord](https://t.co/ns9nldLSrv).  

Running a FullNode
------------------

Our full node is currently in alpha.  

Clone repository with GIT
Change dir to sln directory
```
dotnet restore
dotnet build
```
Change dir to BRhodium subfolder
```
dotnet run

```
