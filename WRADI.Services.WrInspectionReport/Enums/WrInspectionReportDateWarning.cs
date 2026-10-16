namespace WRADI.DocumentType.WrInspectionReport.Enums;

/// An extracted inspection date that cannot be true. A misread year still reports as a found
/// field, so completeness alone does not catch it.
public enum WrInspectionReportDateWarning
{
    None,

    /// An inspection cannot have happened after today.
    FutureDate,

    /// Earlier than the abstraction corpus runs.
    BeforeCorpusEra,

    /// After DMS recorded the document, so the document predates the inspection it describes.
    AfterFiling
}
