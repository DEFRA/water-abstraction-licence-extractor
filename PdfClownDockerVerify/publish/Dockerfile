FROM mcr.microsoft.com/dotnet/runtime:10.0

COPY . ./

RUN apt-get update && \
    apt-get install -y --allow-unauthenticated libgdiplus libc6-dev

ENTRYPOINT ["./PdfClownDockerVerify"]