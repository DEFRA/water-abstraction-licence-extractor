namespace WALE.Api.Areas.BFF.Models;

public record PageImage
{
    public required string FileName { get; init; }
    public required string Extension { get; init; }
    public required int PageNumber { get; init; }
    public required int ImageNumber { get; init; }
}