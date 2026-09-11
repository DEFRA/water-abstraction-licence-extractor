using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Services.AwsS3;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WRADI.Services.Cache.WrInspectionReport;
using WRADI.Services.Cache.WrInspectionReport.Interfaces;
using WRADI.Services.ProcessFile.WrInspectionReport.Implementations;

namespace WRADI.Services.ProcessFile.WrInspectionReport;

public static class FileProcessServiceRegistration
{
    public static IServiceCollection AddWrInspectionReportFileProcessServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<FileProcessAppSettings>(options =>
        {
            options.RefreshCache = ConfigHelper.GetRequiredBool(configuration, "RefreshCache");
            options.DotnetPath = configuration["DotnetPath"] ?? string.Empty;
            options.ApiBaseUrl = ConfigHelper.GetRequiredString(configuration, "ApiBaseUrl");
            options.PdfFolderPath = configuration["PdfFolderPath"] ?? string.Empty;

            // Tesseract/Azure AI Vision are unused for WR51 (no OCR extractors registered below -
            // WR51 documents are native PDFs) but left on FileProcessAppSettings for shape parity
            // with AbstractionLicence's settings class, so left optional here rather than required.
            options.TesseractExeName = configuration["TesseractExeName"] ?? string.Empty;
            options.TesseractExeDirectory = configuration["TesseractExeDirectory"] ?? string.Empty;
            options.TessDataPrefix = configuration["TESSDATA_PREFIX"] ?? string.Empty;
            options.AzureAiVisionEndpoint = configuration["AzureAIVisionEndpoint"] ?? string.Empty;
            options.AzureAiVisionKey = configuration["AzureAIVisionKey"] ?? string.Empty;

            // AWS general
            options.AwsRegionName = ConfigHelper.GetRequiredString(configuration, "AwsRegionName");

            // Optional S3 support
            options.AwsAccessKey = configuration["AwsAccessKey"];
            options.AwsSecretKey = configuration["AwsSecretKey"];
            options.AwsRegionName = configuration["AwsRegionName"];
            options.AwsS3BucketName = configuration["AwsS3BucketName"];

            // SQS - only actually read by the Lambda/Local hosts' own polling loops
            // (FileProcessOrchestrationHostedService/FileProcessSingleFileHostedService), not by
            // anything registered in this method, so left optional here rather than required -
            // the Cmd tool (which bypasses queues entirely, calling IFileProcessSingleService
            // directly) doesn't need them set at all.
            options.SqsQueueOrchestrationUrl = configuration["WrInspectionReportSqsQueueOrchestrationUrl"] ?? string.Empty;
            options.SqsQueueFileProcessUrl = configuration["WrInspectionReportSqsQueueFileProcessUrl"] ?? string.Empty;
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
            .AddHttpClient<IInspectionReportFinderCacheService, ApiInspectionReportFinderCacheService>((sp, client) =>
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
            .AddHttpClient<IMessageQueueService, ApiMessageQueueService>((sp, client) =>
            {
                var settings = sp.GetRequiredService<FileProcessAppSettings>();
                client.BaseAddress = new Uri(settings.ApiBaseUrl);
            })
            .ConfigurePrimaryHttpMessageHandler(_ => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate
            });

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

            // WR51 documents are native (non-scanned) PDFs - PdfPig's native text layer is
            // sufficient, matching RunInspectionReportProcessRun.cs's already-proven approach of
            // no OCR extractors at all. Unlike AbstractionLicence's scanned sample set, there's no
            // need for Tesseract/Azure AI Vision here, which also means the WR51 Single Lambda's
            // Docker image doesn't need the native Tesseract/Leptonica build AbstractionLicence's
            // does.
            var pdfDataExtractor = new PdfDataExtractorService(
                pdfPigNoOcr,
                [],
                cacheService,
                outputService,
                pdfPigDocumentService,
                docnetAlternativeDocumentService,
                messageQueueService);

            return pdfDataExtractor;
        });

        services.AddSingleton<IFileProcessOrchestrator, FileProcessOrchestrationService>();
        services.AddSingleton<IFileProcessSingleService, FileProcessSingleService>();

        return services;
    }
}
