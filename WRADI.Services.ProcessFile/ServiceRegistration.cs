using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Services.AwsS3;
using WALE.ProcessFile.Services.AzureComputerVision;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Tesseract;
using WRADI.Services.ProcessFile.Orchestrate;
using WRADI.Services.ProcessFile.Orchestrate.Implementations;

namespace WRADI.Services.ProcessFile
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddFileProcessingServices(
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
                options.UseS3 = ConfigHelper.GetOptionalBool(configuration, "UseS3") ?? false;
                options.AwsS3AccessKey = configuration["AwsS3AccessKey"];
                options.AwsS3SecretKey = configuration["AwsS3SecretKey"];
                options.AwsS3RegionName = configuration["AwsS3RegionName"];
                options.AwsS3BucketName = configuration["AwsS3BucketName"];
            
                options.SqsQueueOrchestrationUrl = ConfigHelper.GetRequiredString(configuration, "SqsQueueOrchestrationUrl");
                options.SqsQueueFileProcessUrl = ConfigHelper.GetRequiredString(configuration, "SqsQueueFileProcessUrl");
                options.SqsRegionName = ConfigHelper.GetRequiredString(configuration, "SqsRegionName");
                options.SqsWaitTimeSeconds = ConfigHelper.GetOptionalInt(configuration, "SqsWaitTimeSeconds") ?? 20;
                options.SqsMaxNumberOfMessages = ConfigHelper.GetOptionalInt(configuration, "SqsMaxNumberOfMessages") ?? 10;
                options.SqsVisibilityTimeoutSeconds = ConfigHelper.GetOptionalInt(configuration, "SqsVisibilityTimeoutSeconds");
            });

            services.AddSingleton(sp => sp.GetRequiredService<IOptions<FileProcessAppSettings>>().Value);
       
            services.AddSingleton<IFileService>(sp =>
            {
                var settings = sp.GetRequiredService<FileProcessAppSettings>();

                if (settings.UseS3)
                {
                    return new AwsS3FileService(
                        settings.AwsS3RegionName ?? throw new NullReferenceException("AwsS3RegionName"),
                        settings.AwsS3BucketName ?? throw new NullReferenceException("AwsS3BucketName"),
                        settings.AwsS3AccessKey ?? throw new NullReferenceException("AwsS3AccessKey"),
                        settings.AwsS3SecretKey ?? throw new NullReferenceException("AwsS3SecretKey"),
                        null);
                }

                var pdfFolderPath = settings.PdfFolderPath;
                if (!pdfFolderPath.EndsWith('/'))
                {
                    pdfFolderPath += "/";
                }

                return new LocalFileService(pdfFolderPath);
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
}