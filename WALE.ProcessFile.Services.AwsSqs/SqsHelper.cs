using Amazon;
using Amazon.Runtime;
using Amazon.SQS;

namespace WALE.ProcessFile.Services.AwsSqs;

public static class SqsHelper
{
    public static AmazonSQSClient GetSqsClient(
        string regionName,
        string? accessKey,
        string? secretKey,
        string? sessionToken)
    {
        var sqsConfig = new AmazonSQSConfig
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(regionName)
        };
        
        AmazonSQSClient client;

        if (!string.IsNullOrEmpty(accessKey))
        {
            if (!string.IsNullOrEmpty(sessionToken))
            {
                client = new AmazonSQSClient(
                    new SessionAWSCredentials(accessKey, secretKey, sessionToken),
                    sqsConfig);                
            }
            else
            {
                client = new AmazonSQSClient(
                    new BasicAWSCredentials(accessKey, secretKey),
                    sqsConfig);
            }
        }
        else
        {
            client = new AmazonSQSClient(sqsConfig);
        }
    
        return client;
    }
}