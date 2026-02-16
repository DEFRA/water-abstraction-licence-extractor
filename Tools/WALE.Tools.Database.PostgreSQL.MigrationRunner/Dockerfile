FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY . ./

RUN dotnet restore Tools/WALE.Tools.Database.PostgreSQL.MigrationRunner/WALE.Tools.Database.PostgreSQL.MigrationRunner.csproj

RUN dotnet publish Tools/WALE.Tools.Database.PostgreSQL.MigrationRunner/WALE.Tools.Database.PostgreSQL.MigrationRunner.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "WALE.Tools.Database.PostgreSQL.MigrationRunner.dll"]