namespace WALE.Api.Areas.BFF.Models;

public record PageImage
{
    public required Guid FileId { get; init; }
    public required string Extension { get; init; }
    public required int PageNumber { get; init; }
    public required int ImageNumber { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
}