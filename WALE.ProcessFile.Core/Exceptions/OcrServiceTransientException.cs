namespace WALE.ProcessFile.Core.Exceptions;

public class OcrServiceTransientException(string serviceName, string message, Exception innerException)
    : Exception(message, innerException)
{
    public string ServiceName { get; } = serviceName;
}
