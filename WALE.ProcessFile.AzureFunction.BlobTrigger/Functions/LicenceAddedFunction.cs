using System.Net;
using System.Security.Cryptography;
using System.Text;
using Azure;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WALE.ProcessFile.AzureFunction.BlobTrigger.Functions;

public class LicenceAddedFunction(
    IConfiguration configuration,
    ILogger<LicenceAddedFunction> logger)
{
    [Function(nameof(LicenceAddedFunction))]
    public async Task RunAsync(
        [BlobTrigger("licences/{filename}", Connection = "BlobConnection")]
        Stream stream,
        string filename)
    {
        logger.LogInformation("File uploaded. Filename: '{Name}'", filename);
        
        var queueName = configuration["queueName"];
        
        if (string.IsNullOrWhiteSpace(queueName))
        {
            throw new ArgumentNullException(nameof(queueName));
        }
        
        var serviceBus = GetServiceBusClient(queueName);
        var serviceBusSender = serviceBus.CreateSender(queueName);

        await serviceBusSender.SendMessageAsync(new ServiceBusMessage(filename));
    }

    private ServiceBusClient GetServiceBusClient(string queueName)
    {
        var fullyQualifiedNamespace = configuration["FullyQualifiedNamespace"];

        if (string.IsNullOrWhiteSpace(fullyQualifiedNamespace))
        {
            throw new ArgumentNullException(nameof(fullyQualifiedNamespace));
        }
        
        return new ServiceBusClient(
            fullyQualifiedNamespace,
            GetSasCreds(fullyQualifiedNamespace, queueName));
    }

    private AzureSasCredential GetSasCreds(string fullyQualifiedNamespace, string queueName)
    {
        var key = configuration["SasKeyValue"];
        
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentNullException(nameof(key));
        }
        
        using HMACSHA256 hmac = new(Encoding.UTF8.GetBytes(key));

        var builder = new UriBuilder(fullyQualifiedNamespace)
        {
            Scheme = "https",
            Path = queueName
        };

        var url = WebUtility.UrlEncode(builder.Uri.AbsoluteUri);
        var exp = DateTimeOffset.Now.AddHours(1).ToUnixTimeSeconds();
        var sig = WebUtility.UrlEncode(Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(url + "\n" + exp))));

        var keyName = configuration["SasKeyName"];
        var sasToken = $"SharedAccessSignature sr={url}&sig={sig}&se={exp}&skn={keyName}";

        return new AzureSasCredential(sasToken);
    }
}