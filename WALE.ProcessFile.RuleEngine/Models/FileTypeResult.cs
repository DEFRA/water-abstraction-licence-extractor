namespace WALE.ProcessFile.RuleEngine.Models;

/// <summary>
/// Represents the result of file type identification
/// </summary>
public class FileTypeResult
{
    /// <summary>
    /// The identified file type
    /// </summary>
    public string FileType { get; set; } = string.Empty;

    /// <summary>
    /// The confidence level of the identification (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// The rule that identified this file type
    /// </summary>
    public string IdentifiedByRule { get; set; } = string.Empty;

    /// <summary>
    /// Additional metadata about the identification
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// The matched terms that led to this identification
    /// </summary>
    public List<string> MatchedTerms { get; set; } = new();
}
