using Docnet.Core;
using Docnet.Core.Models;
using Docnet.Core.Readers;
using WALE.ProcessFile.Core.Interfaces;

namespace WALE.ProcessFile.Services.Docnet;

public class DocLibInstance
{
    private static readonly DocLib Instance = DocLib.Instance;
    
    public async Task<IDocReader> GetDocReaderAsync(IFileService fileService, string pdfFilename, PageDimensions pageDimensions)
    {
        var bytes = await fileService.GetFileAsBytesAsync(pdfFilename);
        return Instance.GetDocReader(bytes, pageDimensions);
    }
}