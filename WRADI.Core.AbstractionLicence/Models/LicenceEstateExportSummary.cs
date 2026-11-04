namespace WRADI.Core.AbstractionLicence.Models;

public record LicenceEstateExportSummary(
    int Written,
    int NotExtracted,
    int Unreadable,
    int WithoutDocumentText,
    int WithoutTemplateType);
