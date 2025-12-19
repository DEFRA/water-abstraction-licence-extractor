using CsvHelper.Configuration.Attributes;

namespace WALE.ProcessFile.Core.Models;

public class NaldLimitDataLine
{
    [Index(0)]
    public string? LicenceNo { get; set; }

    [Index(1)]
    public long? ConditionId { get; set; }

    [Index(2)]
    public string? Condition { get; set; }

    [Index(3)]
    public string? Code { get; set; }

    [Index(4)]
    public string? Description { get; set; }

    [Index(5)]
    public string? SubCode { get; set; }

    [Index(6)]
    public string? SubCodeDescription { get; set; }

    [Index(7)]
    public string? Text { get; set; }

    [Index(8)]
    public string? Param1 { get; set; }

    [Index(9)]
    public string? Param2 { get; set; }
}