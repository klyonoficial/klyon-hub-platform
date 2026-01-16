FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["klyon-hub-platform.sln", "./"]
COPY ["KlyonHubPlatform.Api/KlyonHubPlatform.Api.csproj", "KlyonHubPlatform.Api/"]
COPY ["KlyonHubPlatform.Application/KlyonHubPlatform.Application.csproj", "KlyonHubPlatform.Application/"]
COPY ["KlyonHubPlatform.Domain/KlyonHubPlatform.Domain.csproj", "KlyonHubPlatform.Domain/"]
COPY ["KlyonHubPlatform.Domain.Core/KlyonHubPlatform.Domain.Core.csproj", "KlyonHubPlatform.Domain.Core/"]
COPY ["KlyonHubPlatform.Infra.Data/KlyonHubPlatform.Infra.Data.csproj", "KlyonHubPlatform.Infra.Data/"]
COPY ["KlyonHubPlatform.Infra.CrossCutting/KlyonHubPlatform.Infra.CrossCutting.csproj", "KlyonHubPlatform.Infra.CrossCutting/"]
RUN dotnet restore "klyon-hub-platform.sln"
COPY . .
WORKDIR "/src"
RUN dotnet build "klyon-hub-platform.sln" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "KlyonHubPlatform.Api/KlyonHubPlatform.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "KlyonHubPlatform.Api.dll"]
