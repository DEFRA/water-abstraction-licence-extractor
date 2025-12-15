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
 -v postgres-data:/var/lib/postgresql/data \
 postgres:18
```

Example connection string for appsettings etc:
`Host=localhost;Port=5432;Database=wale;Username=ea;Password=EnvironmentAgency1`