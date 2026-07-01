using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Services.AwsS3;
using WALE.ProcessFile.Services.AzureComputerVision;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Tesseract;
using WRADI.Services.ProcessFile.Implementations;

namespace WRADI.Services.ProcessFile;

public static class FileProcessServiceRegistration
{
    public static IServiceCollection AddFileProcessServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<FileProcessAppSettings>(options =>
        {
            options.ConcurrentCount = ConfigHelper.GetRequiredInt(configuration, "ConcurrentCount");
            options.RegenerateMappingJson = ConfigHelper.GetRequiredBool(configuration, "REGENERATE_MAPPING_JSON");
            options.LoadAiJs = ConfigHelper.GetRequiredBool(configuration, "LOAD_AI_JS");
            options.RefreshCache = ConfigHelper.GetRequiredBool(configuration, "RefreshCache");
            options.ReportTemplatePath = ConfigHelper.GetRequiredString(configuration, "ReportTemplatePath");
            options.OutputFolder = ConfigHelper.GetRequiredString(configuration, "OutputFolder");
            options.ListDataPath = ConfigHelper.GetRequiredString(configuration, "ListDataPath");
            options.ProcessRunsDataPath = ConfigHelper.GetRequiredString(configuration, "ProcessRunsDataPath");
            options.InternalDataPath = ConfigHelper.GetRequiredString(configuration, "InternalDataPath");
            options.LicenceDataPath = ConfigHelper.GetRequiredString(configuration, "LicenceDataPath");
            options.LicenceSetsDataPath = ConfigHelper.GetRequiredString(configuration, "LicenceSetsDataPath");
            options.ThumbnailImageDataPath = ConfigHelper.GetRequiredString(configuration, "ThumbnailImageDataPath");
            options.FullImageDataPath = ConfigHelper.GetRequiredString(configuration, "FullImageDataPath");
            options.FileMappingPath = ConfigHelper.GetRequiredString(configuration, "FileMappingPath");
            options.DotnetPath = ConfigHelper.GetRequiredString(configuration, "DotnetPath");
            options.TesseractExeName = ConfigHelper.GetRequiredString(configuration, "TesseractExeName");
            options.TesseractExeDirectory = ConfigHelper.GetRequiredString(configuration, "TesseractExeDirectory");
            options.TessDataPrefix = ConfigHelper.GetRequiredString(configuration, "TESSDATA_PREFIX");
            options.ApiBaseUrl = ConfigHelper.GetRequiredString(configuration, "ApiBaseUrl");
            options.PdfFolderPath = ConfigHelper.GetRequiredString(configuration, "PdfFolderPath");
            options.AzureAIVisionEndpoint = ConfigHelper.GetRequiredString(configuration, "AzureAIVisionEndpoint");
            options.AzureAIVisionKey = ConfigHelper.GetRequiredString(configuration, "AzureAIVisionKey");

            // Optional S3 support
            options.AwsAccessKey = configuration["AwsAccessKey"];
            options.AwsSecretKey = configuration["AwsSecretKey"];
            options.AwsRegionName = configuration["AwsRegionName"];
            options.AwsS3BucketName = configuration["AwsS3BucketName"];
            
            options.SqsQueueOrchestrationUrl = ConfigHelper.GetRequiredString(configuration, "SqsQueueOrchestrationUrl");
            options.SqsQueueFileProcessUrl = ConfigHelper.GetRequiredString(configuration, "SqsQueueFileProcessUrl");
            options.AwsRegionName = ConfigHelper.GetRequiredString(configuration, "AwsRegionName");
            options.SqsWaitTimeSeconds = ConfigHelper.GetOptionalInt(configuration, "SqsWaitTimeSeconds") ?? 20;
            options.SqsMaxNumberOfMessages = ConfigHelper.GetOptionalInt(configuration, "SqsMaxNumberOfMessages") ?? 10;
            options.SqsVisibilityTimeoutSeconds = ConfigHelper.GetOptionalInt(configuration, "SqsVisibilityTimeoutSeconds");
        });

        services.AddSingleton(sp => sp.GetRequiredService<IOptions<FileProcessAppSettings>>().Value);
       
        services.AddSingleton<IFileService>(sp =>
        {
            var settings = sp.GetRequiredService<FileProcessAppSettings>();

            var fileServiceType = "api";
            IFileService fileService;

            switch (fileServiceType)
            {
                case "api":
                    var httpClient = HttpHelper.GetResilientHttpClient(
                        settings.ApiBaseUrl,
                        100,
                        30);
                    fileService = new ApiFileService(httpClient);

                    break;
                case "s3":
                {
                    fileService = new AwsS3FileService(
                        settings.AwsRegionName!,
                        settings.AwsS3BucketName!,
                        settings.AwsAccessKey,
                        settings.AwsSecretKey,
                        settings.AwsSessionToken);
                    break;
                }
                default:
                {
                    var pdfFolderPath = settings.PdfFolderPath;
        
                    if (!pdfFolderPath.EndsWith('/'))
                    {
                        pdfFolderPath += "/";
                    }
        
                    fileService = new LocalFileService(pdfFolderPath);
                    break;
                }
            }

            return fileService;
        });

        services.AddHttpClient<ICacheService, ApiCacheService>((sp, client) =>
        {
            var settings = sp.GetRequiredService<FileProcessAppSettings>();
            client.BaseAddress = new Uri(settings.ApiBaseUrl);
        });

        services.AddHttpClient<IOutputService, ApiOutputService>((sp, client) =>
        {
            var settings = sp.GetRequiredService<FileProcessAppSettings>();
            client.BaseAddress = new Uri(settings.ApiBaseUrl);
        });

        services.AddSingleton<PdfPigNoOcrPdfDocumentService>();
        services.AddSingleton<DocnetNoOcrAlternativePdfDocumentService>();

        services.AddSingleton<List<IPdfDataExtractorService>>(sp =>
        {
            var settings = sp.GetRequiredService<FileProcessAppSettings>();
            var cacheService = sp.GetRequiredService<ICacheService>();
            var outputService = sp.GetRequiredService<IOutputService>();
            var pdfPigDocumentService = sp.GetRequiredService<PdfPigNoOcrPdfDocumentService>();
            var docnetAlternativeDocumentService = sp.GetRequiredService<DocnetNoOcrAlternativePdfDocumentService>();

            var pdfDataExtractors = new List<IPdfDataExtractorService>();

            for (var idx = 0; idx < settings.ConcurrentCount; idx++)
            {
                var id = idx + 1;
                var pdfPigNoOcr = new PdfPigNoOcrDataExtractorService();

                var tesseractOcrSparse = new TesseractOcrDataExtractorService(
                    settings.TessDataPrefix,
                    WALE.ProcessFile.Core.Enums.PageSegMode.SparseTextOsd,
                    cacheService,
                    outputService,
                    settings.DotnetPath,
                    settings.TesseractExeName,
                    settings.TesseractExeDirectory,
                    id);

                var tesseractOcrDefault = new TesseractOcrDataExtractorService(
                    settings.TessDataPrefix,
                    WALE.ProcessFile.Core.Enums.PageSegMode.Auto,
                    cacheService,
                    outputService,
                    settings.DotnetPath,
                    settings.TesseractExeName,
                    settings.TesseractExeDirectory,
                    id);

                var azureAiServices = new AzureAiVisionOcrDataExtractorService(
                    settings.AzureAIVisionEndpoint,
                    settings.AzureAIVisionKey,
                    cacheService,
                    outputService,
                    id);

                var pdfDataExtractor = new PdfDataExtractorService(
                    pdfPigNoOcr,
                    [
                        tesseractOcrSparse,
                        tesseractOcrDefault,
                        azureAiServices
                    ],
                    cacheService,
                    outputService,
                    pdfPigDocumentService,
                    docnetAlternativeDocumentService,
                    id);

                pdfDataExtractors.Add(pdfDataExtractor);
            }

            return pdfDataExtractors;
        });

        services.AddSingleton<IOrchestrateFileProcess, FileProcessOrchestrateService>();
        services.AddHttpClient<IOrchestratorService, ApiOrchestratorService>((sp, client) =>
        {
            var settings = sp.GetRequiredService<FileProcessAppSettings>();
            client.BaseAddress = new Uri(settings.ApiBaseUrl);
        });
        
        services.AddSingleton<IScrapeFileService, FileProcessSingleService>();
        return services;
    }
}