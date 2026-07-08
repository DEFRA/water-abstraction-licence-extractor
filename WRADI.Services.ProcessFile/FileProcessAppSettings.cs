namespace WRADI.Services.ProcessFile;

public class FileProcessAppSettings
{
    public bool RefreshCache { get; set; }
    public string DotnetPath { get; set; } = "";
    public string ApiBaseUrl { get; set; } = "";
    public string PdfFolderPath { get; set; } = "";
    
    // Tesseract settings
    public string TesseractExeName { get; set; } = "";
    public string TesseractExeDirectory { get; set; } = "";
    public string TessDataPrefix { get; set; } = "";
    
    // Azure AI Vision settings
    public string AzureAiVisionEndpoint { get; set; } = "";
    public string AzureAiVisionKey { get; set; } = "";

    // General AWS settings
    public string? AwsSessionToken { get; set; }
    public string? AwsAccessKey { get; set; }
    public string? AwsSecretKey { get; set; }
    public string? AwsRegionName { get; set; }
    
    // S3 settings
    public string? AwsS3BucketName { get; set; }
    
    // SQS settings
    public string SqsQueueOrchestrationUrl { get; set; } = null!;
    public string SqsQueueFileProcessUrl { get; set; } = null!;
    public int SqsWaitTimeSeconds { get; set; } = 20;
    public int SqsMaxNumberOfMessages { get; set; } = 10;
    public int? SqsVisibilityTimeoutSeconds { get; set; }
}