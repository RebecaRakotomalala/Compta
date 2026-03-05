FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app
COPY . .
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .

# Database connection for Render
ENV DATABASE_URL=postgresql://root:W9Ef3i4w32hj5xFzxfqfPvZeXKINTXLj@dpg-d6kic49aae7s73ae0l20-a/compta_duko

ENTRYPOINT ["dotnet", "dadaApp"]
