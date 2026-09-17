using iTextSharp.text.pdf;
using org.pdfclown.documents.contents.objects;
using org.pdfclown.files;
using File = System.IO.File;

namespace WRADI.Services.AbstractionLicence.Tests.IntegrationTests;

public class PdfClownTests
{
    [Fact]
    public void PdfClown_DetectTableBorders()
    {
        // Arrange
        const string filename = "wr51__sw0480192006__c9b5d652-2132-42be-9542-58f7dd894d92.pdf";
        
        // iTextSharp
        PdfReader.unethicalreading = true;
        var reader = new PdfReaderExtended($"{TestConfig.PdfFolder}/{filename}");
        reader.DecryptOnPurpose();
        
        using var memoryStream = new MemoryStream();
        var stamper = new PdfStamper(reader, memoryStream);
        stamper.Close();
        reader.Close();
        
        File.WriteAllBytes("output-wradi.pdf", memoryStream.ToArray());
        
        using var pdf = new org.pdfclown.files.File("output-wradi.pdf");
        var doc = pdf.Document;
        
        foreach (var page in doc.Pages)
        {
            var updatedContents = new List<(int, ContentObject)>();
            var pageContentIndex = 0;
            
            // TODO, eventually swap for recursion ideally
            foreach (var content0 in page.Contents)
            {
                // E.g. LocalGraphicsState
                if (content0 is CompositeObject compositeObject)
                {
                    var removes0 = new List<ContentObject?>();
                    
                    foreach (var subContent in compositeObject.Objects)
                    {
                        var replacementItem0 = HandleContentObject(subContent);
                        if (replacementItem0 != null)
                        {
                            removes0.Add(subContent);
                        }
                        
                        // E.g. MarkedContent
                        if (subContent is CompositeObject subCompositeObject)
                        {
                            var removes1 = new List<ContentObject?>();
                            
                            foreach (var subSubContent in subCompositeObject.Objects)
                            {
                                var replacementItem1 = HandleContentObject(subSubContent);
                                if (replacementItem1 != null)
                                {
                                    removes1.Add(subSubContent);
                                }
                                
                                // E.g. LocalGraphicsState
                                if (subSubContent is CompositeObject subSubCompositeObject)
                                {
                                    var removes2 = new List<ContentObject?>();
                                    
                                    foreach (var subSubSubContent in subSubCompositeObject.Objects)
                                    {
                                        var replacementItem2 = HandleContentObject(subSubSubContent);
                                        if (replacementItem2 != null)
                                        {
                                            removes2.Add(subSubSubContent);
                                        }
                                        
                                        // E.g. Path
                                        if (subSubSubContent is CompositeObject subSubSubCompositeObject)
                                        {
                                            var removes3 = new List<ContentObject?>();
                                            
                                            foreach (var subSubSubSubContent in subSubSubCompositeObject.Objects)
                                            {
                                                // E.g. Draw Rectangle
                                                var replacementItem3 = HandleContentObject(subSubSubSubContent);
                                                if (replacementItem3 != null)
                                                {
                                                    removes3.Add(subSubSubSubContent);
                                                }
                                                
                                                if (subSubSubSubContent is CompositeObject)
                                                {
                                                    throw new Exception(
                                                        $"Don't expect this level of iteration (4 levels deep) - {subSubSubSubContent.GetType().FullName}");
                                                }
                                            }

                                            foreach (var remove in removes3)
                                            {
                                                var objs = subSubSubCompositeObject.Objects;
                
                                                // Changing inline doesnt work, this does
                                                objs.Remove(remove);
                                                
                                                updatedContents.Add((pageContentIndex, content0));
                                            }
                                        }
                                    }
                                    
                                    foreach (var remove in removes2)
                                    {
                                        var objs = subSubCompositeObject.Objects;
                
                                        // Changing inline doesnt work, this does
                                        objs.Remove(remove);
                                        updatedContents.Add((pageContentIndex, content0));
                                    }
                                }
                            }
                            
                            foreach (var remove in removes1)
                            {
                                var objs = subCompositeObject.Objects;
                
                                // Changing inline doesnt work, this does
                                objs.Remove(remove);
                                updatedContents.Add((pageContentIndex, content0));
                            }
                        }
                    }
                    
                    foreach (var remove in removes0)
                    {
                        var objs = compositeObject.Objects;
                
                        // Changing inline doesnt work, this does
                        objs.Remove(remove);
                        updatedContents.Add((pageContentIndex, content0));
                    }
                }
                else
                {
                    throw new Exception(
                        $"Don't know how to handle a top-level none composite object - {content0.GetType().FullName}");
                }

                pageContentIndex += 1;
            }
            
            var contents = page.Contents;

            var groupedUpdatedContents = updatedContents
                .GroupBy(x => x.Item1)
                .Select(x => x.First())
                .ToList();
            
            foreach (var content in groupedUpdatedContents)
            {
                // Changing inline doesnt work, this does
                contents.RemoveAt(content.Item1);
                contents.Insert(content.Item1, content.Item2);
            }
            
            contents.Flush();
        }

        var allObjectTypes = string.Join('\n', ContentObjectTypes);
        Assert.Equal(0, allObjectTypes.Length);

        pdf.Save("PdfClown_DetectTableBorders.pdf", SerializationModeEnum.Incremental);
        
        // Act
        // Assert
    }

    private static readonly HashSet<string> ContentObjectTypes = [];
    
    private static ContentObject? HandleContentObject(ContentObject contentObject)
    {
        var typeName = contentObject.GetType().Name;
        
        switch (typeName)
        {
            case "DrawRectangle":
                var drawRectangle = contentObject as DrawRectangle;
                drawRectangle!.Height = 50;
                
                return drawRectangle;
            case "LocalGraphicsState":
            case "Path":
            case "ModifyClipPath":
            case "PaintPath":
            case "Text":
            case "SetFont":
            case "SetTextMatrix":
            case "ApplyExtGState":
            case "SetDeviceGrayFillColor":
            case "SetDeviceGrayStrokeColor":
            case "ShowAdjustedText":
            case "MarkedContent":
            case "SetCharSpace":
            case "SetDeviceRGBFillColor":
            case "SetDeviceRGBStrokeColor":
            case "ModifyCTM":
            case "XObject":
            case "PaintXObject":
                break;
            default:
                ContentObjectTypes.Add(typeName);
                break;
        }

        return null;
    }
    
    [Fact]
    public void PdfClown_ReplaceText()
    {
        // Arrange
        const string filename = "NE0270023036__Application - New - Issued Licence 03.03.2017 9705232.pdf";
        
        // iTextSharp
        PdfReader.unethicalreading = true;
        var reader = new PdfReaderExtended($"{TestConfig.PdfFolder}/{filename}");
        reader.DecryptOnPurpose();
        
        using var memoryStream = new MemoryStream();
        var stamper = new PdfStamper(reader, memoryStream);
        stamper.Close();
        reader.Close();
        
        File.WriteAllBytes("output4.pdf", memoryStream.ToArray());
        
        using var pdf = new org.pdfclown.files.File("output4.pdf");
        var doc = pdf.Document;

        ContentObject? updatedContent = null;

        var textToFind = "ABSTRACT".Select(l => (byte)l).ToArray();
        
        // Seem to be limited by what characters are available or registered in the font/glyph
        var replacementText = "ABC     ".Select(l => (byte)l).ToArray();
        
        foreach (var page in doc.Pages)
        {
            var updateIndex = -1;
            var pageContentIndex = 0;
            
            foreach (var content in page.Contents)
            {
                if (content is Text text)
                {
                    foreach (var subContent in text.Objects)
                    {
                        if (subContent is not ShowText showText)
                        {
                            continue;
                        }
                        
                        var existingValue = (byte[])showText.Value[0];

                        if (!existingValue.SequenceEqual(textToFind))
                        {
                            continue;
                        }
                        
                        var updatedValues = showText.Value;
                        updatedValues.Clear();
                        updatedValues.Add(replacementText);

                        showText.Value = updatedValues;
                            
                        updateIndex = pageContentIndex;
                        updatedContent = content;
                    }
                }
                
                pageContentIndex += 1;
            }

            if (updateIndex != -1)
            {
                var contents = page.Contents;
                
                // Changing inline doesnt work, this does
                contents.RemoveAt(updateIndex);
                contents.Insert(updateIndex, updatedContent);
                
                contents.Flush();
            }
        }

        pdf.Save("PdfClown_ReplaceText.pdf", SerializationModeEnum.Incremental);

        // Act
        // Assert
    }
}