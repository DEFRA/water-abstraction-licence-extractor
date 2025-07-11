using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Messaging.ServiceBus;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Models;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Services.PdfPig;

namespace WALE.ProcessFile.AzureFunction.ServiceBusTrigger.Functions;

public class MessageReceivedFunction(
    IConfiguration configuration,
    ILogger<MessageReceivedFunction> logger)
{
    private const string QueueName = "licences-to-process";
    
    [Function(nameof(MessageReceivedFunction))]
    public async Task RunAsync(
        [ServiceBusTrigger(QueueName, Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        var pdfFolderPath = configuration["PdfFolderPath"];
        if (string.IsNullOrEmpty(pdfFolderPath)) throw new Exception($"{nameof(pdfFolderPath)} is missing");
        
        var outputFolder = configuration["OutputFolder"];
        if (string.IsNullOrEmpty(outputFolder)) throw new Exception($"{nameof(outputFolder)} is missing");
        
        var tesseractPath = configuration["TesseractPath"];
        if (string.IsNullOrEmpty(tesseractPath)) throw new Exception($"{nameof(pdfFolderPath)} is missing");
        
        var aiVisionKey = configuration["AiVisionKey"];
        if (string.IsNullOrEmpty(aiVisionKey)) throw new Exception($"{nameof(aiVisionKey)} is missing");
        
        var aiVisionEndpoint = configuration["AiVisionEndpoint"];
        if (string.IsNullOrEmpty(aiVisionEndpoint)) throw new Exception($"{nameof(aiVisionEndpoint)} is missing");
        
        var fileName = Encoding.UTF8.GetString(message.Body);
        var pdfFilePath = $"{pdfFolderPath}/{fileName}";
        
        var previouslyParsedPaths = new List<string>
        {
            pdfFilePath
        };
        
        var fileLicenceMapping = new Dictionary<string, string>();
        const bool useCache = true;

        var tesseract = new TesseractOcrDataExtractorService(tesseractPath);
        
        var pdfDataExtractor = new PdfDataExtractorService(
            new PdfPigNoOcrDataExtractorService(),
            [
                tesseract,
                new AzureAiVisionOcrDataExtractorService(aiVisionEndpoint, aiVisionKey)
            ],
            pdfFolderPath);
        
        var matches = await pdfDataExtractor.GetMatchesAsync(
            pdfFilePath,
            LabelConfiguration.GetLabels(),
            fileLicenceMapping,
            previouslyParsedPaths,
            outputFolder,
            useCache);
        
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };
        
        var filenameOnlyNoExtension = DataHelpers.GetFilenameWithoutExtensions(pdfFilePath);
        NullOutSubLabels(matches);

        var json = JsonSerializer.Serialize(new ParseResult
        {
            Filename = pdfFilePath.Split('/').Last(),
            Matches = matches
        }, jsonOptions);

        var blobClient = GetBlobServiceClient(configuration["BlobAccountName"]!);
        var jsonFileName = $"{filenameOnlyNoExtension}.json";

        var assetsClient = blobClient.GetBlobContainerClient("assets");
        await assetsClient.DeleteBlobIfExistsAsync(jsonFileName);
        await assetsClient.UploadBlobAsync(jsonFileName, BinaryData.FromString(json));

        var licencesClient = blobClient.GetBlobContainerClient("licences");
        var processedLicencesClient = blobClient.GetBlobContainerClient("processed-licences");
        await MoveAsync(licencesClient.GetBlockBlobClient(fileName), processedLicencesClient, fileName);

        await messageActions.CompleteMessageAsync(message);
    }
    
    private static async Task MoveAsync(BlockBlobClient srcBlob, BlobContainerClient destContainer, string name)
    {
        if (srcBlob == null)
        {
            throw new Exception("Source blob cannot be null.");
        }

        if (!await destContainer.ExistsAsync())
        {
            throw new Exception("Destination container does not exist.");
        }
        
        var memoryStream = new MemoryStream();
        await srcBlob.DownloadToAsync(memoryStream);
        memoryStream.Position = 0;
        
        var destBlob = destContainer.GetBlockBlobClient(name);
        await destBlob.UploadAsync(memoryStream);
        
        await srcBlob.DeleteAsync();
    }
    
    private BlobServiceClient GetBlobServiceClient(string accountName)
    {
        var fullyQualifiedNamespace = $"{accountName}.blob.core.windows.net";
        
        return new BlobServiceClient(
            new Uri($"https://{fullyQualifiedNamespace}"),
            new StorageSharedKeyCredential(accountName, configuration["BlobKey"]));
    }
    
    private static void NullOutSubLabels(IReadOnlyList<LabelGroupResult> matches)
    {
        foreach (var match in matches)
        {
            if (match.MatchedLabel != null)
            {
                match.MatchedLabel.SubLabels = null;
            }

            if (match.SubResults != null)
            {
                NullOutSubLabels(match.SubResults);
            }
        }
    }
}