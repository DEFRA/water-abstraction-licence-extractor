namespace WALE.ProcessFile.Core.Exceptions;

/// <summary>
/// Thrown by an OCR provider when the failure is expected to clear on its own - throttling
/// (e.g. Textract ProvisionedThroughputExceededException after the SDK's own retries are
/// exhausted), rate limiting, or a provider-side 5xx. Distinguished from permanent failures
/// (bad image, unsupported format) so the extractor can decide to fail the file and let the
/// queue retry it, instead of silently carrying on without that provider.
/// </summary>
public class OcrServiceTransientException(string serviceName, string message, Exception innerException)
    : Exception(message, innerException)
{
    public string ServiceName { get; } = serviceName;
}
