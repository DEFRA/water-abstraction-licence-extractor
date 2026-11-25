namespace WALE.Api.Interfaces;

public interface IThumbnailUrlService
{
    Task<Dictionary<Guid, string>> GetThumbnailUrlsAsync(IEnumerable<Guid> fileIds);
}
