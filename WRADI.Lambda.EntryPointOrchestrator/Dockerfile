FROM public.ecr.aws/lambda/dotnet:10 AS base

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .

RUN dotnet restore WRADI.Lambda.EntryPointOrchestrator/WRADI.Lambda.EntryPointOrchestrator.csproj

RUN dotnet publish WRADI.Lambda.EntryPointOrchestrator/WRADI.Lambda.EntryPointOrchestrator.csproj \
    --configuration Release \
    --runtime linux-x64 \
    --self-contained false \
    --output /app/publish

FROM base AS final
WORKDIR /var/task

COPY --from=build /app/publish .

CMD ["WRADI.Lambda.EntryPointOrchestrator::WRADI.Lambda.EntryPointOrchestrator.MessageReceivedFunction::FunctionHandler"]