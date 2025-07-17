using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.PdfFonts;

namespace WALE.ProcessFile.Services.Models.PdfPig;

/// <summary>
/// A glyph or combination of glyphs (characters) drawn by a PDF content stream.
/// </summary>
public class DeserialisableLetter
{
    /// <summary>
    /// The text for this letter or unicode character.
    /// </summary>
    public string? Value { get; set; }
    
    /// <summary>
    /// Position of the bounding box for the glyph, this is the box surrounding the visible glyph as it appears on the page.
    /// For example letters with descenders, p, j, etc., will have a box extending below the <see cref="Location"/> they are placed at.
    /// The width of the glyph may also be more or less than the <see cref="Width"/> allocated for the character in the PDF content.
    /// </summary>
    public DeserialisablePdfRectangle? GlyphRectangle { get; set; }
    
    /// <summary>
    /// The placement position of the character in PDF space (the start point of the baseline). See <see cref="Location"/>
    /// </summary>
    public DeserialisablePdfPoint? StartBaseLine { get; set; }
    
    /// <summary>
    /// The end point of the baseline.
    /// </summary>
    public DeserialisablePdfPoint? EndBaseLine { get; set; }
    
    /// <summary>
    /// The width occupied by the character within the PDF content.
    /// </summary>
    public double Width { get; set; }
    
    /// <summary>
    /// Size as defined in the PDF file. This is not equivalent to font size in points but is relative to other font sizes on the page.
    /// </summary>
    public double FontSize { get; set; }
    
    /// <summary>
    /// Details about the font for this letter.
    /// </summary>
    public FontDetails? Font { get; set; }

    /// <summary>
    /// Text rendering mode that indicates whether we should draw this letter's strokes,
    /// fill, both, neither (in case of hidden text), etc.
    /// If it calls for stroking the <see cref="StrokeColor" /> is used.
    /// If it calls for filling, the <see cref="FillColor"/> is used.
    /// In modes that perform both filling and stroking, the effect is as if each glyph outline were filled and then stroked in separate operations.
    /// </summary>
    public TextRenderingMode RenderingMode { get; set; }
    
    /// <summary>
    /// Stroking color
    /// </summary>
    public DeserialisableColor? StrokeColor { get; set; }

    /// <summary>
    /// Non-stroking (fill) color
    /// </summary>
    public DeserialisableColor? FillColor { get; set; }
    
    /// <summary>
    /// The size of the font in points.
    /// </summary>
    public double PointSize { get; set; }
    
    /// <summary>
    /// Sequence number of the ShowText operation that printed this letter.
    /// </summary>
    public int TextSequence { get; set; }
    
    public Letter ToPdfPigLetter()
    {
        return new Letter(
            Value!,
            GlyphRectangle!.ToPdfRectangle(),
            StartBaseLine!.ToPdfPoint(),
            EndBaseLine!.ToPdfPoint(),
            Width,
            FontSize,
            Font!,
            RenderingMode,
            StrokeColor!,
            FillColor!,
            PointSize,
            TextSequence);
    }
}