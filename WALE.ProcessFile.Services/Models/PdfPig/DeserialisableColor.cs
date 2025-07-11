using UglyToad.PdfPig.Graphics.Colors;

namespace WALE.ProcessFile.Services.Models.PdfPig;

public class DeserialisableColor : IColor
{
    public (double r, double g, double b) ToRGBValues()
    {
        // Won't be called
        throw new NotImplementedException();
    }

    public ColorSpace ColorSpace { get; }
}