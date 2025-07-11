using System.Text.Json;
using System.Text.Json.Serialization;
using WALE.ProcessFile.Services.Enums;

namespace WALE.ProcessFile.Services.Models;

public class LabelToMatch
{
    public IReadOnlyList<string>? TextStart { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public IReadOnlyList<string>? Text
    {
        get => TextStart;
        set => TextStart = value;
    }
    
    public bool MatchAllText { get; init; }

    public IReadOnlyList<TextToMatch>? Remove { get; set; }
    public IReadOnlyList<string>? TextEnd { get; set; }
    public IReadOnlyList<string>? MustContain { get; set; }
    public int? MinimumSubMatches { get; init; }
    public LabelPosition Position { get; set; }
    public string? RelatedCategoryName { get; init; }
    public string? RelatedName { get; init; }
    public int LeewayBefore { get; init; } // TODO can likely get rid of this now ordering is sorted
    public IReadOnlyList<LabelToMatch>? SubLabels { get; set; }
    public string Format { get; set; } = "Text";
    public bool IncludeLabelText { get; init; }
    public string? Name { get; init; }
    public string? CategoryName { get; init; }
    public IReadOnlyList<string>? Possibilities { get; set; }
    public int PreviousLinesToFetch { get; init; } = 10;
    public int NextLinesToFetch { get; init; } = 10;
    public MultipleType Multiple { get; init; } = MultipleType.False;
    
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public bool Completed { get; set; }
    
    public LabelToMatch Clone()
    {
        return JsonSerializer.Deserialize<LabelToMatch>(
            JsonSerializer.Serialize(this))!;
    }    
}