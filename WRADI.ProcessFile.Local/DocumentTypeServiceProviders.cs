using Microsoft.Extensions.Logging;

namespace WRADI.ProcessFile.Local;

public sealed class DocumentTypeServiceProviders(
    IReadOnlyDictionary<string, IServiceProvider> providersByDocumentType)
{
    public IServiceProvider Get(string? documentType, ILogger logger)
    {
        if (documentType != null && providersByDocumentType.TryGetValue(documentType, out var provider))
        {
            return provider;
        }

        logger.LogWarning(
            "Unrecognised DocumentType '{DocumentType}' - falling back to AbstractionLicence",
            documentType);

        return providersByDocumentType["AbstractionLicence"];
    }
}
