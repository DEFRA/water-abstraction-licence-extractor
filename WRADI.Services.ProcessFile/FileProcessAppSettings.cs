namespace WRADI.Services.ProcessFile;

public class FileProcessAppSettings
{
    public int ConcurrentCount { get; set; }
    public bool RegenerateMappingJson { get; set; }
    public bool LoadAiJs { get; set; }
    public bool RefreshCache { get; set; }

    public string ReportTemplatePath { get; set; } = "";
    public string OutputFolder { get; set; } = "";
    public string ListDataPath { get; set; } = "";
    public string ProcessRunsDataPath { get; set; } = "";
    public string InternalDataPath { get; set; } = "";
    public string LicenceDataPath { get; set; } = "";
    public string LicenceSetsDataPath { get; set; } = "";
    public string ThumbnailImageDataPath { get; set; } = "";
    public string FullImageDataPath { get; set; } = "";
    public string FileMappingPath { get; set; } = "";
    public string DotnetPath { get; set; } = "";
    public string TesseractExeName { get; set; } = "";
    public string TesseractExeDirectory { get; set; } = "";
    public string TessDataPrefix { get; set; } = "";
    public string ApiBaseUrl { get; set; } = "";
    public string PdfFolderPath { get; set; } = "";
    public string AzureAIVisionEndpoint { get; set; } = "";
    public string AzureAIVisionKey { get; set; } = "";

    public string? AwsSessionToken { get; set; }
    public string? AwsAccessKey { get; set; }
    public string? AwsSecretKey { get; set; }
    public string? AwsRegionName { get; set; }
    public string? AwsS3BucketName { get; set; }
    
    // New SQS settings
    public string SqsQueueOrchestrationUrl { get; set; } = null!;
    
    public string SqsQueueFileProcessUrl { get; set; } = null!;
    public int SqsWaitTimeSeconds { get; set; } = 20;
    public int SqsMaxNumberOfMessages { get; set; } = 10;
    public int? SqsVisibilityTimeoutSeconds { get; set; }
}