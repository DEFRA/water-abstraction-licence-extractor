using WALE.ProcessFile.Models;

namespace WALE.ProcessFile.Services.Models;

public class TextAndLabel
{
    public string? Text { get; init; }
    public LabelToMatch? Label { get; init; }
}