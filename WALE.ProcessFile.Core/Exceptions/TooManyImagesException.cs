namespace WALE.ProcessFile.Core.Exceptions;

public class TooManyImagesException(string message, int imageCount, int pageCount) : Exception(message)
{
    public int NumberOfImages { get; set; } = imageCount;
    
    public int NumberOfPages { get; set; } = pageCount;
}
