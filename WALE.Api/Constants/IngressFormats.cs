namespace WALE.Api.Constants;

/// Word documents are accepted because ConvertWordInspectionReportsToPdf converts them in the
/// bucket afterwards.
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

    /// From the extension, not the request: a browser sends application/msword for one .doc and
    /// application/octet-stream for the next, so the request's own type rejects valid files.
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

    /// Falls back rather than refusing: a download is no place to reject a key already in the bucket.
    public static string ContentTypeForFile(string fileName) =>
        ContentTypes.GetValueOrDefault(Path.GetExtension(fileName), Fallback);
}
