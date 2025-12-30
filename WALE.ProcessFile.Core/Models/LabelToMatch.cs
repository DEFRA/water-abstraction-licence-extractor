using System.Text.Json.Serialization;
using WALE.ProcessFile.Core.Enums;

namespace WALE.ProcessFile.Core.Models;

public class LabelToMatch
{
    public IReadOnlyList<TextToMatch>? TextStart { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public IReadOnlyList<TextToMatch>? Text
    {
        get => TextStart;
        set => TextStart = value;
    }
    
    public bool MatchAllText { get; init; }
    public IReadOnlyList<string>? IgnoreBlockIfContains { get; init; }
    public IReadOnlyList<string>? IgnoreMatchIfContains { get; init; }
    public IReadOnlyList<string>? SkipLineWhenContains { get; init; }    
    public IReadOnlyList<TextToMatch>? Remove { get; set; }
    public IReadOnlyList<TextToMatch>? TextEnd { get; set; }
    public IReadOnlyList<string>? MustContain { get; set; }
    public int? MinimumSubMatches { get; init; }

    public MultipleServiceMatchBehaviour MultipleServiceMatchBehaviour { get; init; } =
        MultipleServiceMatchBehaviour.UseLastServiceResult;
    public bool CanGoOverPageBoundary { get; init; }
    public LabelPosition Position { get; set; }
    public string? RelatedCategoryName { get; init; }
    public string? RelatedName { get; init; }
    public int LeewayBefore { get; init; } // TODO can likely get rid of this now ordering is sorted
    public IReadOnlyList<LabelToMatch>? SubLabels { get; set; }
    public string Format { get; set; } = "Text";
    public bool IncludeStartLabelText { get; init; }
    public bool IncludeEndLabelText { get; init; }
    public bool IncludeWholeLine { get; init; }
    public string? Name { get; init; }
    public string? CategoryName { get; init; }
    public IReadOnlyList<string>? Possibilities { get; set; }
    public int PreviousLinesToFetch { get; init; } = 2;
    public int NextLinesToFetch { get; init; } = 4;
    public bool DoNotTrimLines { get; init; }
    public MultipleBehaviour MultipleBehaviour { get; init; } = MultipleBehaviour.FindSingleInstanceOfLabelWithASingleValue;
    public bool FindMultipleOnSingleLine { get; init; }
        
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public bool Completed { get; set; }
    public bool AutoCorrect { get; init; }
    public bool TreatColumnsSeperatelyForBeforeAndAfterText { get; set; }

    public LabelToMatch Clone()
    {
        // TODO swap to a source generator

        return new LabelToMatch
        {
            TextStart = TextStart,
            MatchAllText = MatchAllText,
            IgnoreMatchIfContains = IgnoreMatchIfContains?.ToList(),
            IgnoreBlockIfContains = IgnoreBlockIfContains?.ToList(),
            SkipLineWhenContains = SkipLineWhenContains?.ToList(),
            Remove = Remove?.ToList(),
            TextEnd = TextEnd?.ToList(),
            MustContain = MustContain?.ToList(),
            MinimumSubMatches = MinimumSubMatches,
            MultipleServiceMatchBehaviour = MultipleServiceMatchBehaviour,
            Position = Position,
            RelatedCategoryName = RelatedCategoryName,
            RelatedName = RelatedName,
            LeewayBefore = LeewayBefore,
            SubLabels = SubLabels?.Select(s => s.Clone()).ToList(),
            Format = Format,
            IncludeStartLabelText = IncludeStartLabelText,
            IncludeEndLabelText = IncludeEndLabelText,            
            IncludeWholeLine = IncludeWholeLine,
            Name = Name,
            CategoryName = CategoryName,
            Possibilities = Possibilities?.ToList(),
            PreviousLinesToFetch = PreviousLinesToFetch,
            NextLinesToFetch = NextLinesToFetch,
            MultipleBehaviour = MultipleBehaviour,
            FindMultipleOnSingleLine = FindMultipleOnSingleLine,
            Completed = false,
            DoNotTrimLines = DoNotTrimLines,
            AutoCorrect = AutoCorrect
        };
    }    
}