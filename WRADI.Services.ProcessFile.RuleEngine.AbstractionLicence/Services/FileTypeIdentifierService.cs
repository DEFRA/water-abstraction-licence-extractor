using WALE.ProcessFile.Core.Models;
using WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Engine;
using WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Interfaces;
using WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Models;
using WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Rules.FileType;

namespace WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Services;

/// <summary>
/// Service for identifying file types based on content analysis using PDF text extraction
/// </summary>
public class FileTypeIdentifierService
{
    private readonly IRuleEngine<FileTypeResult> _ruleEngine;

    /// <summary>
    /// Initializes a new instance of FileTypeIdentifierService with PDF extractor service
    /// </summary>
    public FileTypeIdentifierService()
    {
        _ruleEngine = new RuleEngine<FileTypeResult>();
        InitializeDefaultRules();
    }

    /// <summary>
    /// Identifies the file type based on the content of a file using OCR when needed
    /// </summary>
    /// <param name="content">The path to the file</param>
    /// <param name="filename">The path to the file</param>
    /// <returns>The file type identification result, or null if no type could be identified or an error occurred</returns>
    public FileTypeResult? IdentifyFileType(MatchesResult content, string filename)
    {
        return _ruleEngine.Evaluate(content);
    }
    
    private void InitializeDefaultRules()
    {
        _ruleEngine.AddRule(new LicenceFileTypeRule());
        _ruleEngine.AddRule(new AddendumFileTypeRule());
    }
}