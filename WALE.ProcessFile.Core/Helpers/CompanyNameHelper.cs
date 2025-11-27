namespace WALE.ProcessFile.Core.Helpers;

public static class CompanyNameHelper
{
    public static bool MayBeInitials(string word)
    {
        return word.Length is 2
               && word.All(char.IsUpper);
    }
    
    public static bool StartsWithCompanyOrPersonalPrefix(string? text)
    {
        if (text == null)
        {
            return false;
        }
        
        return Prefixes
            .Any(prefix => text.StartsWith(prefix,
                StringComparison.InvariantCultureIgnoreCase));
    }
    
    public static bool EndsWithCompanyOrPersonalSuffix(string? text)
    {
        if (text == null)
        {
            return false;
        }
        
        return CompanySuffixes
            .Any(suffix => text.EndsWith(suffix,
                StringComparison.InvariantCultureIgnoreCase));
    }
    
    private static readonly List<string> Prefixes =
    [
        "department ",
        "university ",
        "mr ",
        "mr. ",
        "mrs ",
        "mrs. ",
        "miss ",
        "miss. ",
        "lord ",
        "lord. ",
        "lady ",
        "lady. "
    ];
    
    public static readonly List<string> CompanySuffixes =
    [
        " agency",
        " limited",
        " charities",
        " ltd",
        " plc",
        " school",
        " corporation",
        " university",
        " and sons",
        " water board",
        " users",
        " estate",
        " quarry",
        " nurseries",
        " esq.", // Personal suffix
        " esq",
        " and son",
        " and partners",
        " farms"
    ];
    
    public static readonly List<string> CompanyWords = ["trading as"];
}