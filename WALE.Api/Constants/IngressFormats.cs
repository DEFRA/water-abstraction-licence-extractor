namespace WALE.Api.Constants;

/// <summary>
/// What the ingress bucket takes, against the content type it is stored and served as.
///
/// Word documents are accepted because they are converted to PDF in the bucket afterwards, by
/// ConvertWordInspectionReportsToPdf - the people holding them cannot run a converter themselves,
/// so the bucket is where the Word file and its PDF meet.
///
/// Shared by the upload and download endpoints, which sit in different areas and have to agree:
/// what a file is stored as is what it has to be served back as.
/// </summary>
public static class IngressFormats
{
    private const string Fallback = "application/octet-stream";

    private static readonly Dictionary<string, string> ContentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".doc"] = "application/msword",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        };

    public static IReadOnlyCollection<string> AcceptedExtensions => ContentTypes.Keys;

    public static bool IsAccepted(string? extension) =>
        extension != null && ContentTypes.ContainsKey(extension);

    /// <summary>
    /// The type to store an accepted upload as. Taken from the extension rather than the request: a
    /// browser sends application/msword for one .doc and application/octet-stream for the next, so the
    /// request's own type rejects valid files. The extension is both the reliable signal and the one
    /// the pipeline itself keys on.
    /// </summary>
    public static bool TryGetContentType(string? extension, out string contentType)
    {
        if (extension != null && ContentTypes.TryGetValue(extension, out var found))
        {
            contentType = found;
            return true;
        }

        contentType = Fallback;
        return false;
    }

}
