# ============================================================
# Native build stage: build Leptonica 1.82.0 + Tesseract 5.3.0
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

# Build Tesseract 5.3.0
RUN wget https://github.com/tesseract-ocr/tesseract/archive/refs/tags/5.3.0.tar.gz -O tesseract-5.3.0.tar.gz \
    && tar -xzf tesseract-5.3.0.tar.gz \
    && cd tesseract-5.3.0 \
    && ./autogen.sh \
    && PKG_CONFIG_PATH=/opt/tesseract/lib/pkgconfig ./configure --prefix=/opt/tesseract \
    && make -j$(nproc) \
    && make install

# Add English trained data
RUN mkdir -p /opt/tesseract/share/tessdata \
    && wget -O /opt/tesseract/share/tessdata/eng.traineddata \
       https://github.com/tesseract-ocr/tessdata_fast/raw/main/eng.traineddata


# ============================================================
# Final Lambda runtime image
# ============================================================
FROM public.ecr.aws/lambda/dotnet:10

WORKDIR /var/task

# Runtime dependencies required by Tesseract/Leptonica
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

# Copy Tesseract/Leptonica built in the native-build stage
COPY --from=native-build /opt/tesseract /opt/tesseract

# Copy your published Lambda app.
# This assumes your deploy script publishes to:
# WRADI.Lambda.FileProcess.Single/lambda-publish
COPY WRADI.Lambda.FileProcess.Single/lambda-publish/ /var/task/

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

# Create exact filenames expected by the .NET Tesseract wrapper
RUN mkdir -p /var/task/x64 \
    && ln -sf /opt/tesseract/lib/libleptonica.so /var/task/x64/libleptonica-1.82.0.so \
    && ln -sf /opt/tesseract/lib/libleptonica.so /var/task/libleptonica-1.82.0.so \
    && ln -sf /opt/tesseract/lib/liblept.so /var/task/x64/liblept.so \
    && ln -sf /opt/tesseract/lib/liblept.so.5 /var/task/x64/liblept.so.5 \
    && ln -sf /opt/tesseract/lib/libtesseract.so /var/task/x64/libtesseract50.so \
    && ln -sf /opt/tesseract/lib/libtesseract.so /var/task/libtesseract50.so \
    && ln -sf /opt/tesseract/lib/libtesseract.so /var/task/x64/libtesseract.so \
    && ln -sf /opt/tesseract/lib/libtesseract.so.5 /var/task/x64/libtesseract.so.5

ENV LD_LIBRARY_PATH="/var/task:/var/task/x64:/var/task/runtimes/linux-x64/native:/opt/tesseract/lib:/lib64:/usr/lib64:/usr/lib:/lib"
ENV PATH="/opt/tesseract/bin:${PATH}"
ENV TESSDATA_PREFIX="/opt/tesseract/share/tessdata"

# Build-time diagnostics
RUN echo "---- libdl files ----" && \
    find / -name "libdl.so*" 2>/dev/null | sort && \
    echo "---- /var/task/libdl.so ----" && \
    ls -la /var/task/libdl.so && \
    echo "---- Tesseract / Leptonica files ----" && \
    find /var/task /opt/tesseract \
      \( -iname "*tesseract*.so*" -o -iname "*lept*.so*" -o -iname "*leptonica*.so*" \) \
      2>/dev/null | sort && \
    echo "---- ldd libleptonica ----" && \
    ldd /var/task/x64/libleptonica-1.82.0.so && \
    echo "---- ldd libtesseract ----" && \
    ldd /var/task/x64/libtesseract50.so

CMD ["WRADI.Lambda.FileProcess.Single::WRADI.Lambda.FileProcess.Single.MessageReceivedFunction::FunctionHandler"]