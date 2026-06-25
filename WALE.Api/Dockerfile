FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY . ./

RUN dotnet restore WALE.Api/WALE.Api.csproj

RUN dotnet publish WALE.Api/WALE.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

COPY ./WALE.Api/nginx.conf /etc/nginx/conf.d/default.conf

WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "WALE.Api.dll"]