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
                BucketName = BucketName
            });

        return response.S3Objects
            .Select(s3Object => s3Object.Key)
            .ToList();
    }

    public Task<Stream> GetFileAsStreamAsync(string filename)
    {
        throw new NotImplementedException();
    }

    public Task<byte[]> GetFileAsBytesAsync(string pdfFilename)
    {
        throw new NotImplementedException();
    }

    public string BucketName { get; set; } = bucketName;
    
    public string FolderPath { get; set; } = bucketName;
    
    private AmazonS3Client GetS3Client()
    {
        var awsCredentials = new BasicAWSCredentials(accessKey, secretKey);
        var client = new AmazonS3Client(awsCredentials, new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(regionName) // eu-west-1
        });
        
        return client;
    }
}