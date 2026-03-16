using System.Text;
using Azure.Messaging.ServiceBus;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.AzureComputerVision;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Tesseract;

namespace WALE.ProcessFile.AzureFunction.ServiceBusTrigger.Functions;

public class MessageReceivedFunction(
    IOutputService outputService,
    ICacheService cacheService,
    IConfiguration configuration/*,
    ILogger<MessageReceivedFunction> logger*/)
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
        
        var cacheFolder = configuration["CacheFolder"];
        if (string.IsNullOrEmpty(cacheFolder)) throw new Exception($"{nameof(cacheFolder)} is missing");
        
        var tesseractPath = configuration["TesseractPath"];
        if (string.IsNullOrEmpty(tesseractPath)) throw new Exception($"{nameof(pdfFolderPath)} is missing");
        
        var aiVisionKey = configuration["AiVisionKey"];
        if (string.IsNullOrEmpty(aiVisionKey)) throw new Exception($"{nameof(aiVisionKey)} is missing");
        
        var aiVisionEndpoint = configuration["AiVisionEndpoint"];
        if (string.IsNullOrEmpty(aiVisionEndpoint)) throw new Exception($"{nameof(aiVisionEndpoint)} is missing");
        
        var dotnetPath = configuration["DotnetPath"];
        if (string.IsNullOrEmpty(dotnetPath)) throw new Exception($"{nameof(dotnetPath)} is missing");
        
        var tesseractExeName = configuration["TesseractExeName"];
        if (string.IsNullOrEmpty(tesseractExeName)) throw new Exception($"{nameof(tesseractExeName)} is missing");
        
        var tesseractExeDirectory = configuration["TesseractExeDirectory"];
        if (string.IsNullOrEmpty(tesseractExeDirectory)) throw new Exception($"{nameof(tesseractExeDirectory)} is missing");
        
        var fileName = Encoding.UTF8.GetString(message.Body);
        
        var previouslyParsedFiles = new List<string>
        {
            fileName
        };
        
        var pdfPigDocumentService = new PdfPigNoOcrPdfDocumentService();
        var docnetAlternativeDocumentService = new DocnetNoOcrAlternativePdfDocumentService();
        
        var fileLicenceMapping = new Dictionary<string, DmsFileData>();

        var pdfDataExtractor = new PdfDataExtractorService(
            new PdfPigNoOcrDataExtractorService(),
            [
                new TesseractOcrDataExtractorService(tesseractPath, Core.Enums.PageSegMode.SparseTextOsd, cacheService, outputService, dotnetPath, tesseractExeName, tesseractExeDirectory),
                new AzureAiVisionOcrDataExtractorService(aiVisionEndpoint, aiVisionKey, cacheService, outputService),
            ],
            cacheService,
            outputService,
            pdfPigDocumentService,
            docnetAlternativeDocumentService);

        var matches = await pdfDataExtractor.GetMatchesAsync(
            fileName,
            new LookupConfiguration(
                WalLabelConfiguration.GetLabels(),
                fileLicenceMapping,
                await CompanyName.GetFirstNamesCsvFromFileAsync(),
                new LocalFileService(pdfFolderPath),
                1),
            previouslyParsedFiles,
            0);
        
        var json = JsonHelper.GetAsString(matches);
        var blobClient = GetBlobServiceClient(configuration["BlobAccountName"]!);
        
        var filenameNoExtension = FileHelper.GetFilenameWithoutExtension(fileName);
        var jsonFileName = $"{filenameNoExtension}.json";

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
}