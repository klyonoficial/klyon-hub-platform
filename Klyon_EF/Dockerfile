FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["KlyonEF.csproj", "./"]
RUN dotnet restore "KlyonEF.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "KlyonEF.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "KlyonEF.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "KlyonEF.dll"]
