namespace WALE.ProcessFile.Core.Models;

public class ImageDetails
{
    public int pageNumber { get; set; }

    public int imageNumber { get; set; }

    public string? extension { get; set; }

    public int width { get; set; }
    
    public int height { get; set; }
}