namespace WALE.ProcessFile.Core.Exceptions;

/// <summary>
/// Throttling, rate limiting or a provider-side 5xx, as opposed to a permanent failure such as
/// a bad image. Lets the extractor fail the file for the queue to retry rather than degrade.
/// </summary>
public class OcrServiceTransientException(string serviceName, string message, Exception innerException)
    : Exception(message, innerException)
{
    public string ServiceName { get; } = serviceName;
}
