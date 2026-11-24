FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . ./

RUN dotnet restore WALE.Api/WALE.Api.csproj

RUN dotnet publish WALE.Api/WALE.Api.csproj -c Release -o /app/publish

# ============================================================
# Native build stage: build Leptonica 1.82.0
# ============================================================
FROM public.ecr.aws/lambda/dotnet:10 AS native-build

RUN dnf install -y \
    gcc \
    gcc-c++ \
    make \
    automake \
    autoconf \
    libtool \
    pkgconfig \
    git \
    wget \
    tar \
    gzip \
    libjpeg-turbo-devel \
    libpng-devel \
    libtiff-devel \
    zlib-devel \
    giflib-devel \
    libwebp-devel \
    openjpeg2-devel \
    && dnf clean all

WORKDIR /tmp

# Build Leptonica 1.82.0
RUN wget https://github.com/DanBloomberg/leptonica/releases/download/1.82.0/leptonica-1.82.0.tar.gz \
    && tar -xzf leptonica-1.82.0.tar.gz \
    && cd leptonica-1.82.0 \
    && ./configure --prefix=/opt/tesseract \
    && make -j$(nproc) \
    && make install

WORKDIR /src

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# Runtime dependencies required by Leptonica
RUN dnf install -y \
    findutils \
    file \
    which \
    tar \
    gzip \
    glibc \
    glibc-langpack-en \
    libgcc \
    libstdc++ \
    libpng \
    libjpeg-turbo \
    giflib \
    libtiff \
    libwebp \
    openjpeg2 \
    fontconfig \
    freetype \
    libgomp \
    && dnf clean all

COPY ./WALE.Api/nginx.conf /etc/nginx/conf.d/default.conf

WORKDIR /app
COPY --from=build /app/publish .

# Remove Windows native binaries from the Linux Lambda image
RUN find /var/task \
    \( -name "leptonica-1.82.0.dll" -o -name "tesseract50.dll" \) \
    -delete || true 

# Add libdl.so symlink for libraries that expect it
RUN if [ -f /lib64/libdl.so.2 ]; then \
      ln -sf /lib64/libdl.so.2 /var/task/libdl.so; \
    elif [ -f /usr/lib64/libdl.so.2 ]; then \
      ln -sf /usr/lib64/libdl.so.2 /var/task/libdl.so; \
    else \
      echo "ERROR: libdl.so.2 not found"; \
      find / -name "libdl.so*" 2>/dev/null; \
      exit 1; \
    fi

EXPOSE 8080

ENTRYPOINT ["dotnet", "WALE.Api.dll"]