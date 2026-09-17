using iTextSharp.text.pdf;
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Editing;
using PdfImage = KillerPdf.Engine.Authoring.PdfImage;

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
        
        var image = PdfImage.FromJpeg(File.ReadAllBytes("/Users/ryanbarlow/Downloads/test1.jpg"));

        var content = new PdfContentStreamBuilder()
            .DrawImage(image, x: 72, y: 500, width: 180, height: 120);
        
        var page = PdfPageInformation.Read(document)[0];
        
        var updated = new PdfIncrementalPageEditor(document)
            .AppendPageDescribedContent(0, page.Width, page.Height, content, "...")
            .Build();
        
        File.WriteAllBytes("outputKillerPdf2.pdf", updated);
    }
}