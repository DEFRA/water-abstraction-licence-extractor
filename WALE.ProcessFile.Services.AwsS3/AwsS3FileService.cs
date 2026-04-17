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
    string bucketName) : IFileService
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
        
        var awsCredentials = new BasicAWSCredentials(accessKey, secretKey);
        
        var client = new AmazonS3Client(
            awsCredentials,
            new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(regionName)
            });
        
        _client = client;
        return client;
    }

    private AmazonS3Client? _client;
}