using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using WALE.ProcessFile.Core.Interfaces;

namespace WALE.ProcessFile.Services.AwsS3;

public class S3ImageService(
    string regionName,
    string bucketName,
    string? accessKey,
    string? secretKey,
    string? sessionToken) : IImageService
{
    public Task UploadAsync(string key, Stream data, string contentType)
    {
        var client = GetS3Client();

        return client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucketName,
            Key = key,
            InputStream = data,
            ContentType = contentType
        }, CancellationToken.None);
    }

    public async Task<byte[]?> DownloadAsync(string key)
    {
        var client = GetS3Client();

        try
        {
            using var response = await client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = bucketName,
                Key = key
            });

            await using var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream);
            return memoryStream.ToArray();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        var client = GetS3Client();

        try
        {
            await client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = bucketName,
                Key = key
            });

            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public Task<string> GetPresignedUrlAsync(string key)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucketName,
            Key = key,
            Expires = DateTime.Now.AddMinutes(60),
            Protocol = Protocol.HTTPS
        };

        return GetS3Client().GetPreSignedURLAsync(request);
    }

    public Task DeleteAsync(string key)
    {
        var client = GetS3Client();

        return client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = bucketName,
            Key = key
        });
    }

    private AmazonS3Client GetS3Client()
    {
        if (_client != null)
        {
            return _client;
        }

        var s3Config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(regionName)
        };

        AmazonS3Client client;

        if (!string.IsNullOrEmpty(accessKey))
        {
            client = !string.IsNullOrEmpty(sessionToken)
                ? new AmazonS3Client(new SessionAWSCredentials(accessKey, secretKey, sessionToken), s3Config)
                : new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), s3Config);
        }
        else
        {
            client = new AmazonS3Client(s3Config);
        }

        _client = client;
        return client;
    }

    private AmazonS3Client? _client;
}
