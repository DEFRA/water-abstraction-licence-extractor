namespace WRADI.Services.ProcessFile.AbstractionLicence;

public class FileProcessAppSettings
{
    public bool RefreshCache { get; set; }
    public string DotnetPath { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = string.Empty;
    public string PdfFolderPath { get; set; } = string.Empty;
    
    // Tesseract settings
    public string TesseractExeName { get; set; } = string.Empty;
    public string TesseractExeDirectory { get; set; } = string.Empty;
    public string TessDataPrefix { get; set; } = string.Empty;
    
    // Azure AI Vision settings
    public string AzureAiVisionEndpoint { get; set; } = string.Empty;
    public string AzureAiVisionKey { get; set; } = string.Empty;

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
    public int SqsMaxNumberOfMessages { get; set; } = 5;
    public int? SqsVisibilityTimeoutSeconds { get; set; }
}