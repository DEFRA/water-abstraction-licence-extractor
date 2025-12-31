namespace WALE.ProcessFile.Core.Interfaces;

public interface INoOcrPdfPageService
{
    public Task<IReadOnlyList<INoOcrPdfImageService>> GetImagesAsync();

    public int Number { get; set; }
}