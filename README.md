# Water Abstraction Licence Extractor

## Pre-reqs

- Have Brew installed
- Run the following to install Tesseract;

```brew install tesseract```

- Have an instance of PostgreSQL running, e.g.

```
docker run -d \
 --name postgres-dev \
 -e POSTGRES_PASSWORD=EnvironmentAgency1 \
 -e POSTGRES_USER=ea \
 -e POSTGRES_DB=wale \
 -p 5432:5432 \
 -v postgres-data:/var/lib/postgresql \
 postgres:18
```

Example 'PostgresConnectionString' connection string for appsettings;
`Host=localhost;Port=5432;Database=wale;Username=ea;Password=EnvironmentAgency1;Timeout=300;CommandTimeout=300;KeepAlive=300;`

To run migrations, use the WALE.Tools.Database.PostgreSQL.MigrationRunner tool

## Generating TypeScript client
(Requires NSwag CLI to be installed on your machine - `dotnet tool install --global NSwag.ConsoleCore`)
This codegen is used to generate a TypeScript client for the API, so that we can easily consume it from the frontend.
- Run the WALE.Api project
- Open a terminal in the following directory:
  - `/WALE.Portal/src/api/generated`:
- Run this command:
  - `dotnet nswag openapi2tsclient /input:http://localhost:8080/openapi/v1.json /output:apiClient.ts`

## Running the web portal
(Requires Node.js to be installed on your machine)
- Run the WALE.Api project – this is the backend for the web portal
- Open a terminal in the WALE.Portal directory
- Run `npm install` (only needs to be done once)
- Run `npm run dev`