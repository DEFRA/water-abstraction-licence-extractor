using Microsoft.Extensions.Logging;

namespace WRADI.ProcessFile.Local;

// One independent IServiceProvider per document type - see
// WRADI.Lambda.FileProcess.Orchestrator's MessageReceivedFunction for why these aren't combined
// into one shared container (IFileProcessOrchestrator/IFileProcessSingleService are the same
// WALE.ProcessFile.Core interface for every document type, with non-overlapping concrete
// dependency graphs). Both hosted services resolve their per-message service from here instead
// of taking it directly via constructor injection.
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
