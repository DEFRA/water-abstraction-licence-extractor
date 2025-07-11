namespace WALE.ProcessFile.Services.Interfaces;

public interface INoOcrPdfPageService
{
    public Task<IEnumerable<INoOcrPdfImageService>> GetImagesAsync();

    public int Number { get; set; }
}