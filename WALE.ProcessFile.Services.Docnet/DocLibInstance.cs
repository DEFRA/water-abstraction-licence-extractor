using Docnet.Core;
using Docnet.Core.Models;
using Docnet.Core.Readers;
using WALE.ProcessFile.Core.Interfaces;

namespace WALE.ProcessFile.Services.Docnet;

public class DocLibInstance
{
    private static readonly DocLib Instance = DocLib.Instance;
    
    public IDocReader GetDocReader(IFileService fileService, string pdfFilename, PageDimensions pageDimensions)
    {
        return Instance.GetDocReader(fileService.GetFileAsBytes(pdfFilename), pageDimensions);
    }
}