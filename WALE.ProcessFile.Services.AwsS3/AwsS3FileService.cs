using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.AwsS3;

public class AwsS3FileService(
    string regionName,
    string bucketName,
    string? accessKey,
    string? secretKey,
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


        var returnList = response.S3Objects
            .Select(s3Object => s3Object.Key)
            .ToList();

        while (!string.IsNullOrEmpty(response.NextContinuationToken))
        {
            response = await client.ListObjectsV2Async(
                new ListObjectsV2Request
                {
                    ContinuationToken = response.NextContinuationToken,
                    BucketName = FolderPath
                });
            
            returnList.AddRange(response.S3Objects
                .Select(s3Object => s3Object.Key));
        }

        return returnList
            .OrderBy(filename => filename)
            .ToList();
    }

    public async Task<List<FileMetadata>> GetAllFilesWithMetadataAsync()
    {
        var client = GetS3Client();
        var response = await client.ListObjectsV2Async(
            new ListObjectsV2Request
            {
                BucketName = FolderPath
            });


        var returnList = response.S3Objects
            .Select(s3Object => new FileMetadata
            {
               Filename = s3Object.Key,
               Filesize = s3Object.Size!.Value,
               ModifiedTime = s3Object.LastModified ?? DateTime.MinValue
            })
            .ToList();

        while (!string.IsNullOrEmpty(response.NextContinuationToken))
        {
            response = await client.ListObjectsV2Async(
                new ListObjectsV2Request
                {
                    ContinuationToken = response.NextContinuationToken,
                    BucketName = FolderPath
                });
            
            returnList.AddRange(response.S3Objects
                .Select(s3Object => new FileMetadata
                {
                    Filename = s3Object.Key,
                    Filesize = s3Object.Size!.Value,
                    ModifiedTime = s3Object.LastModified ?? DateTime.MinValue
                }));
        }

        return returnList
            .OrderBy(fm => fm.Filename)
            .ToList();
    }

    public async Task<byte[]> GetFileAsBytesAsync(string filename)
    {
        var stream = await GetFileAsStreamAsync(filename);

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

    public Task UploadFileAsStreamAsync(string filename, Stream stream)
    {
        var client = GetS3Client();
        
        return client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = FolderPath,
            Key = filename,
            InputStream = stream,
            ContentType = "application/pdf"
        }, CancellationToken.None);
    }

    public async Task<string?> UploadFileChunkAsync(string filename, Stream stream, int chunkIndex, int totalChunks, string? uploadId = null)
    {
        var client = GetS3Client();

        if (chunkIndex == 0)
        {
            var initiateResponse = await client.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest
            {
                BucketName = FolderPath,
                Key = filename,
                ContentType = "application/pdf"
            });
            uploadId = initiateResponse.UploadId;
        }

        if (string.IsNullOrEmpty(uploadId))
        {
            throw new InvalidOperationException($"No multipart upload in progress for file {filename}");
        }

        await client.UploadPartAsync(new UploadPartRequest
        {
            BucketName = FolderPath,
            Key = filename,
            UploadId = uploadId,
            PartNumber = chunkIndex + 1,
            InputStream = stream,
            PartSize = stream.Length
        });

        if (chunkIndex == totalChunks - 1)
        {
            var parts = await client.ListPartsAsync(new ListPartsRequest
            {
                BucketName = FolderPath,
                Key = filename,
                UploadId = uploadId
            });

            await client.CompleteMultipartUploadAsync(new CompleteMultipartUploadRequest
            {
                BucketName = FolderPath,
                Key = filename,
                UploadId = uploadId,
                PartETags = parts.Parts.Select(p => new PartETag(p.PartNumber!.Value, p.ETag)).ToList()
            });
        }

        return uploadId;
    }

    public string FolderPath { get; set; } = bucketName;
    
    public Task DeleteAsync(string filename)
    {
        var client = GetS3Client();
        
        return client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = FolderPath,
            Key = filename
        });
    }

    public async Task<bool> ExistsAsync(string filename)
    {
        var client = GetS3Client();
        var file = await client.GetObjectAsync(
            new GetObjectRequest
            {
                BucketName = FolderPath,
                Key = filename
            });

        return file != null;
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
            if (!string.IsNullOrEmpty(sessionToken))
            {
                client = new AmazonS3Client(
                    new SessionAWSCredentials(accessKey, secretKey, sessionToken),
                    s3Config);                
            }
            else
            {
                client = new AmazonS3Client(
                    new BasicAWSCredentials(accessKey, secretKey),
                    s3Config);
            }
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