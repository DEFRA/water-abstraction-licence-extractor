namespace WALE.ProcessFile.Services.Interfaces;

public interface INoOcrPdfPageService
{
    public Task<IReadOnlyList<INoOcrPdfImageService>> GetImagesAsync();

    public int Number { get; set; }
}