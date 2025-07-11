namespace WALE.ProcessFile.Services.Interfaces;

public interface INoOcrPdfImageService
{
    public Task<byte[]> GetImageBytesAsync(int imageNumber, int pageNumber, string outputFolder);
}