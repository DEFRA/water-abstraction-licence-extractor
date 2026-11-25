namespace WALE.ProcessFile.Core.Models;

public class ProcessRunNonOkFilesResponse
{
    public List<MatchResultError> Items { get; set; } = [];

    public int TotalCount { get; set; }

    public int DeclaredFileCount { get; set; }

    public int ProcessedFileCount { get; set; }

    public List<ProcessRunFailureGroup> FailureGroups { get; set; } = [];
}
