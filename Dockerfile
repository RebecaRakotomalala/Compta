FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app
COPY . .
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Render / Docker : évite souvent les coupures TLS ou DNS AAAA sans route IPv6 (Npgsql EndOfStream au login PG).
ENV DOTNET_SYSTEM_NET_DISABLEIPV6=1

COPY --from=build /app/out .

ENTRYPOINT ["dotnet", "dadaApp.dll"]
