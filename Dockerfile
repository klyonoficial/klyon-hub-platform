FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["klyon-hub-platform.csproj", "./"]
RUN dotnet restore "klyon-hub-platform.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "klyon-hub-platform.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "klyon-hub-platform.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "klyon-hub-platform.dll"]
