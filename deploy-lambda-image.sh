#!/bin/bash
set -e

export AWS_REGION=eu-north-1
export AWS_ACCOUNT_ID=624426144646
export ECR_REPO=wradi-fileprocess-lambda
export FUNCTION_NAME=wradi-fileprocess-lambda-image

# Date-based image tag
export IMAGE_TAG="$(date +"%Y%m%d-%H%M%S")-TK"
export IMAGE_URI="$AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com/$ECR_REPO:$IMAGE_TAG"

echo "Using image tag: $IMAGE_TAG"
echo "Using image uri: $IMAGE_URI"

echo "Publishing .NET project locally..."

dotnet publish WRADI.Lambda.Orchestrator.FileProcess/WRADI.Lambda.Orchestrator.FileProcess.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained false \
  -o WRADI.Lambda.Orchestrator.FileProcess/lambda-publish \
  /p:PublishReadyToRun=true

echo "Logging in to ECR..."

aws ecr get-login-password --region "$AWS_REGION" | docker login \
  --username AWS \
  --password-stdin "$AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com"

echo "Building and pushing Docker image..."

docker buildx build \
  --platform linux/amd64 \
  --provenance=false \
  --sbom=false \
  -f WRADI.Lambda.Orchestrator.FileProcess/Dockerfile \
  -t "$IMAGE_URI" \
  --push \
  .

echo "Pulling pushed image locally so we can inspect it..."

docker pull --platform linux/amd64 "$IMAGE_URI"

echo "Checking libdl inside Docker image..."

docker run --rm \
  --platform linux/amd64 \
  --entrypoint /bin/sh \
  "$IMAGE_URI" \
  -c 'find / -name "libdl.so*" 2>/dev/null | sort; echo ""; ls -la /var/task/libdl.so 2>/dev/null || true'

echo "Checking native libraries inside Docker image..."

docker run --rm \
  --platform linux/amd64 \
  --entrypoint /bin/sh \
  "$IMAGE_URI" \
  -c '
    set -e

    echo "---- /opt/tesseract/lib ----"
    ls -la /opt/tesseract/lib

    echo "---- expected wrapper files ----"
    ls -la /var/task/x64/libleptonica-1.82.0.so
    ls -la /var/task/x64/libtesseract50.so

    echo "---- Tesseract / Leptonica files ----"
    find /var/task /opt/tesseract \
      \( -iname "*tesseract*.so*" -o -iname "*lept*.so*" -o -iname "*leptonica*.so*" \) \
      2>/dev/null | sort

    echo "---- ldd libleptonica ----"
    ldd /var/task/x64/libleptonica-1.82.0.so

    echo "---- ldd libtesseract ----"
    ldd /var/task/x64/libtesseract50.so

    echo "---- checking for missing dependencies ----"
    if ldd /var/task/x64/libleptonica-1.82.0.so | grep "not found"; then
      echo "ERROR: libleptonica has missing dependencies"
      exit 1
    fi

    if ldd /var/task/x64/libtesseract50.so | grep "not found"; then
      echo "ERROR: libtesseract has missing dependencies"
      exit 1
    fi

    echo "Native dependency check passed."
  '

echo "Updating Lambda function..."

aws lambda update-function-code \
  --function-name "$FUNCTION_NAME" \
  --image-uri "$IMAGE_URI" \
  --region "$AWS_REGION"

echo "Waiting for Lambda update to finish..."

aws lambda wait function-updated \
  --function-name "$FUNCTION_NAME" \
  --region "$AWS_REGION"

echo "Confirming Lambda image URI..."

aws lambda get-function \
  --function-name "$FUNCTION_NAME" \
  --region "$AWS_REGION" \
  --query 'Code.ImageUri' \
  --output text

echo "Deployment complete."

echo "Tailing Lambda logs..."

aws logs tail "/aws/lambda/$FUNCTION_NAME" \
  --follow \
  --region "$AWS_REGION"
