namespace WALE.ProcessFile.Models.Interfaces;

public interface INoOcrPdfPageService
{
    public Task<IReadOnlyList<INoOcrPdfImageService>> GetImagesAsync();

    public int Number { get; set; }
}