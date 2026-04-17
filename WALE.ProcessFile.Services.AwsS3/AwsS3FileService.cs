using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using WALE.ProcessFile.Core.Interfaces;

namespace WALE.ProcessFile.Services.AwsS3;

public class AwsS3FileService(
    string accessKey,
    string secretKey,
    string regionName,
    string bucketName,
    string? roleArn,
    string? roleSessionName,
    string? sessionToken) : IFileService
{
    public async Task<List<string>> GetAllFilesAsync()
    {
        var client = GetS3Client();
        var response = await client.ListObjectsV2Async(
            new ListObjectsV2Request
            {
                BucketName = FolderPath
            });

        return response.S3Objects
            .Select(s3Object => s3Object.Key)
            .ToList();
    }

    public async Task<byte[]> GetFileAsBytesAsync(string pdfFilename)
    {
        var stream = await GetFileAsStreamAsync(pdfFilename);

        using var binaryReader = new BinaryReader(stream);
        return binaryReader.ReadBytes((int)stream.Length);
    }
    
    public async Task<Stream> GetFileAsStreamAsync(string filename)
    {
        var client = GetS3Client();
        var file = await client.GetObjectAsync(
            new GetObjectRequest
            {
                BucketName = FolderPath,
                Key = filename
            });

        return file.ResponseStream;
    }

    public Task UploadFileAsStreamAsync(string pdfFilename, Stream stream)
    {
        var client = GetS3Client();
        
        return client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = FolderPath,
            Key = pdfFilename,
            InputStream = stream,
            ContentType = "application/pdf"
        }, CancellationToken.None);
    }

    public string FolderPath { get; set; } = bucketName;
    
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
            client = new AmazonS3Client(
                new SessionAWSCredentials(accessKey, secretKey, sessionToken),
                s3Config);
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