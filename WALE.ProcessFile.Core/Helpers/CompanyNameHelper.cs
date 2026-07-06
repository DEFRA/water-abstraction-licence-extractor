using System.Globalization;
using System.Reflection;
using CsvHelper;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Helpers;

public static class CompanyNameHelper
{
    public static async Task<HashSet<string>> GetFirstNamesCsvFromFileAsync()
    {
        var returnList = new HashSet<string>();
        var dtStart = DateTime.Now;

        var basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var path = Path.Combine(basePath, "Data/first-names.csv");
        
        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, new CultureInfo("en-GB"));
        
        var data = csv.GetRecordsAsync<FirstNamesRow>();
        
        await foreach (var record in data)
        {
            var name = record.FirstForename!.ToLower();
            if (name.Length <= 2 || FirstNameAvoidWords.Contains(name))
            {
                continue;
            }
            
            returnList.Add(name);
        }

        ConsoleHelper.WriteLine($"INFO - {nameof(CompanyNameHelper)} - Loading FirstNamesCsv took {(DateTime.Now - dtStart).TotalMilliseconds}ms");
        return returnList;
    }
    
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
    
    private static readonly HashSet<string> FirstNameAvoidWords =
    [
        "the", // Too generic
        "po", // PO box
        "mersey", // Geography
        "june", // Month
        "charity", // Company word
        "grant", // Legal word
        "manor", // house name,
        "red", // color, not common name
        "south", // direction
        "north", // direction
        "west", // direction
        "rho", // In postcodes
        "rivers", // water
        "see", // doing word,
        "heh", // Is it a name?
        "you", //  Is it a name?
        "thames", // River
        "fee"
    ];
    
    public static readonly List<string> CompanyWords = ["trading as"];
}