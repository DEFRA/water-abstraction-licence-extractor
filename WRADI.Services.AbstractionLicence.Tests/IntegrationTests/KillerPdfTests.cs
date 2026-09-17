using iTextSharp.text.pdf;
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Editing;
using PdfImage = KillerPdf.Engine.Authoring.PdfImage;
using PdfSpotColor = KillerPdf.Engine.Authoring.PdfSpotColor;

namespace WRADI.Services.AbstractionLicence.Tests.IntegrationTests;

public class KillerPdfTests
{
    [Fact]
    public void KillerPdf_AddOverlay()
    {
        // Arrange
        const string filename = "NE0270023036__Application - New - Issued Licence 03.03.2017 9705232.pdf";
        
        PdfReader.unethicalreading = true;
        var reader = new PdfReaderExtended($"{TestConfig.PdfFolder}/{filename}");
        reader.DecryptOnPurpose();
        
        using var memoryStream = new MemoryStream();
        var stamper = new PdfStamper(reader, memoryStream);
        stamper.Close();
        reader.Close();
        
        File.WriteAllBytes("output2.pdf", memoryStream.ToArray());

        // KillerPDF
        var source = File.ReadAllBytes("output2.pdf");
        var document = KillerPdf.Engine.Documents.PdfDocument.Open(source);
        
        var content = new PdfContentStreamBuilder()
            .BeginText()
            .SetFont(PdfStandardFont.Helvetica, 18)
            .MoveText(0, 0)
            .SetStrokeSpotColor(new PdfSpotColor("Black", new PdfCmykColor(0, 0, 0, 1)), 1)
            .ShowLatin1Text("Test to try and replace something")
            .EndText();
        
        var page = PdfPageInformation.Read(document)[0];
        
        var updated = new PdfIncrementalPageEditor(document)
            .AppendPageDescribedContent(0, page.Width, page.Height, content, "...")
            .Build();
        
        File.WriteAllBytes("outputKillerPdf2.pdf", updated);
    }
}