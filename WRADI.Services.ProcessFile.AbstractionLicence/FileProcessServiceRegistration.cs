using System.Net;
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
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Services.Cache.AbstractionLicence;
using WRADI.Services.Output.AbstractionLicence;
using WRADI.Services.ProcessFile.AbstractionLicence.Implementations;

namespace WRADI.Services.ProcessFile.AbstractionLicence;

public static class FileProcessServiceRegistration
{
    public static IServiceCollection AddFileProcessServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<FileProcessAppSettings>(options =>
        {
            options.RefreshCache = ConfigHelper.GetRequiredBool(configuration, "RefreshCache");
            options.DotnetPath = ConfigHelper.GetRequiredString(configuration, "DotnetPath");
            options.ApiBaseUrl = ConfigHelper.GetRequiredString(configuration, "ApiBaseUrl");
            options.PdfFolderPath = ConfigHelper.GetRequiredString(configuration, "PdfFolderPath");

            // Tesseract
            options.TesseractExeName = ConfigHelper.GetRequiredString(configuration, "TesseractExeName");
            options.TesseractExeDirectory = ConfigHelper.GetRequiredString(configuration, "TesseractExeDirectory");
            options.TessDataPrefix = ConfigHelper.GetRequiredString(configuration, "TESSDATA_PREFIX");
            
            // Azure AI
            options.AzureAiVisionEndpoint = ConfigHelper.GetRequiredString(configuration, "AzureAIVisionEndpoint");
            options.AzureAiVisionKey = ConfigHelper.GetRequiredString(configuration, "AzureAIVisionKey");

            // AWS general
            options.AwsRegionName = ConfigHelper.GetRequiredString(configuration, "AwsRegionName");
            
            // Optional S3 support
            options.AwsAccessKey = configuration["AwsAccessKey"];
            options.AwsSecretKey = configuration["AwsSecretKey"];
            options.AwsRegionName = configuration["AwsRegionName"];
            options.AwsS3BucketName = configuration["AwsS3BucketName"];
            
            // SQS
            options.SqsQueueOrchestrationUrl = ConfigHelper.GetRequiredString(configuration, "SqsQueueOrchestrationUrl");
            options.SqsQueueFileProcessUrl = ConfigHelper.GetRequiredString(configuration, "SqsQueueFileProcessUrl");
            options.SqsWaitTimeSeconds = ConfigHelper.GetOptionalInt(configuration, "SqsWaitTimeSeconds") ?? 20;
            options.SqsMaxNumberOfMessages = ConfigHelper.GetOptionalInt(configuration, "SqsMaxNumberOfMessages") ?? 10;
            options.SqsVisibilityTimeoutSeconds = ConfigHelper.GetOptionalInt(configuration, "SqsVisibilityTimeoutSeconds");
        });

        services.AddSingleton(sp =>
            sp.GetRequiredService<IOptions<FileProcessAppSettings>>().Value);
       
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

        services
            .AddHttpClient<ICacheService, ApiCacheService>((sp, client) =>
            {
                var settings = sp.GetRequiredService<FileProcessAppSettings>();
                client.BaseAddress = new Uri(settings.ApiBaseUrl);
            })
            .ConfigurePrimaryHttpMessageHandler(_ => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate
            });
        
        services
            .AddHttpClient<IAbstractionLicenceCacheService, ApiAbstractionLicenceCacheService>((sp, client) =>
            {
                var settings = sp.GetRequiredService<FileProcessAppSettings>();
                client.BaseAddress = new Uri(settings.ApiBaseUrl);
            })
            .ConfigurePrimaryHttpMessageHandler(_ => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate
            });

        services
            .AddHttpClient<IOutputService, ApiOutputService>((sp, client) =>
            {
                var settings = sp.GetRequiredService<FileProcessAppSettings>();
                client.BaseAddress = new Uri(settings.ApiBaseUrl);
            })
            .ConfigurePrimaryHttpMessageHandler(_ => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate
            });
        
        services
            .AddHttpClient<IAbstractionLicenceOutputService, ApiAbstractionLicenceOutputService>((sp, client) =>
            {
                var settings = sp.GetRequiredService<FileProcessAppSettings>();
                client.BaseAddress = new Uri(settings.ApiBaseUrl);
            })
            .ConfigurePrimaryHttpMessageHandler(_ => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate
            });
        
        services
            .AddHttpClient<IMessageQueueService, ApiMessageQueueService>((sp, client) =>
            {
                var settings = sp.GetRequiredService<FileProcessAppSettings>();
                client.BaseAddress = new Uri(settings.ApiBaseUrl);
            })
            .ConfigurePrimaryHttpMessageHandler(_ => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate
            });;

        services.AddSingleton<PdfPigNoOcrPdfDocumentService>();
        services.AddSingleton<DocnetNoOcrAlternativePdfDocumentService>();

        services.AddSingleton<IPdfDataExtractorService>(sp =>
        {
            var settings = sp.GetRequiredService<FileProcessAppSettings>();
            var cacheService = sp.GetRequiredService<ICacheService>();
            var outputService = sp.GetRequiredService<IOutputService>();
            var messageQueueService = sp.GetRequiredService<IMessageQueueService>();
            var pdfPigDocumentService = sp.GetRequiredService<PdfPigNoOcrPdfDocumentService>();
            var docnetAlternativeDocumentService = sp.GetRequiredService<DocnetNoOcrAlternativePdfDocumentService>();

            var pdfPigNoOcr = new PdfPigNoOcrDataExtractorService();

            var tesseractOcrSparse = new TesseractOcrDataExtractorService(
                settings.TessDataPrefix,
                WALE.ProcessFile.Core.Enums.PageSegMode.SparseTextOsd,
                cacheService,
                outputService,
                settings.DotnetPath,
                settings.TesseractExeName,
                settings.TesseractExeDirectory);

            var tesseractOcrDefault = new TesseractOcrDataExtractorService(
                settings.TessDataPrefix,
                WALE.ProcessFile.Core.Enums.PageSegMode.Auto,
                cacheService,
                outputService,
                settings.DotnetPath,
                settings.TesseractExeName,
                settings.TesseractExeDirectory);

            var azureAiServices = new AzureAiVisionOcrDataExtractorService(
                settings.AzureAiVisionEndpoint,
                settings.AzureAiVisionKey,
                cacheService,
                outputService);
            
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
                messageQueueService);

            return pdfDataExtractor;
        });

        services.AddSingleton<IFileProcessOrchestrator, FileProcessOrchestrationService>();
        services.AddHttpClient<IMessageQueueService, ApiMessageQueueService>((sp, client) =>
        {
            var settings = sp.GetRequiredService<FileProcessAppSettings>();
            client.BaseAddress = new Uri(settings.ApiBaseUrl);
        });
        
        services.AddSingleton<IFileProcessSingleService, FileProcessSingleService>();
        return services;
    }
}