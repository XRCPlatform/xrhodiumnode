FROM microsoft/dotnet:2.0-runtime AS base
WORKDIR /app

FROM microsoft/dotnet:2.0-sdk AS build
WORKDIR /src
COPY BRhodium/BRhodium.csproj BRhodium/
COPY BRhodium.Bitcoin.Features.RPC/BRhodium.Bitcoin.Features.RPC.csproj BRhodium.Bitcoin.Features.RPC/
COPY BRhodium.Node/BRhodium.Node.csproj BRhodium.Node/
COPY NBitcoin/NBitcoin.csproj NBitcoin/
COPY BRhodium.Bitcoin.Features.Consensus/BRhodium.Bitcoin.Features.Consensus.csproj BRhodium.Bitcoin.Features.Consensus/
COPY BRhodium.Bitcoin.Features.BlockStore/BRhodium.Bitcoin.Features.BlockStore.csproj BRhodium.Bitcoin.Features.BlockStore/
COPY BRhodium.Bitcoin.Features.Miner/BRhodium.Bitcoin.Features.Miner.csproj BRhodium.Bitcoin.Features.Miner/
COPY BRhodium.Bitcoin.Features.MemoryPool/BRhodium.Bitcoin.Features.MemoryPool.csproj BRhodium.Bitcoin.Features.MemoryPool/
COPY BRhodium.Bitcoin.Features.Wallet/BRhodium.Bitcoin.Features.Wallet.csproj BRhodium.Bitcoin.Features.Wallet/
RUN dotnet restore BRhodium/BRhodium.csproj
COPY . .
WORKDIR /src/BRhodium
RUN dotnet build BRhodium.csproj -c Release -o /app

FROM build AS publish
RUN dotnet publish BRhodium.csproj -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "BRhodium.dll"]
