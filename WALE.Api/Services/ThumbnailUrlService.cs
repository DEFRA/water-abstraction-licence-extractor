using WALE.Api.Interfaces;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Interfaces;

namespace WALE.Api.Services;

public class ThumbnailUrlService(IFileService fileService) : IThumbnailUrlService
{
    /// A list request can ask for every row, and signing is one call per file.
    private const int BatchSize = 20;

    public async Task<Dictionary<Guid, string>> GetThumbnailUrlsAsync(IEnumerable<Guid> fileIds)
    {
        var urls = new Dictionary<Guid, string>();

        var wanted = fileIds
            .Where(fileId => fileId != Guid.Empty)
            .Distinct()
            .ToArray();

        foreach (var batch in wanted.Chunk(BatchSize))
        {
            var signing = batch
                .Select(fileId => (
                    FileId: fileId,
                    Url: fileService.GetPresignedUrlAsync(
                        $"thumbnail_{fileId}.jpg",
                        StorageFolder.Assets)))
                .ToArray();

            foreach (var (fileId, url) in signing)
            {
                urls[fileId] = await url;
            }
        }

        return urls;
    }
}
