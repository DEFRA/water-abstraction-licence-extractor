using System;

namespace WRADI.Services.LicenceFinder.Helpers;

/// <summary>
/// Helper class containing utility methods for licence file processing
/// </summary>
public static class LicenceFileHelpers
{
    /// <summary>
    /// Cleans a license number by removing forward slashes and asterisks
    /// </summary>
    /// <param name="licNo">The original license number (e.g., "6/33/03/*G/0038")</param>
    /// <returns>Cleaned permit number (e.g., "633303G0038")</returns>
    public static string CleanPermitNumber(string licNo)
    {
        if (string.IsNullOrWhiteSpace(licNo))
        {
            return string.Empty;
        }

        // Remove forward slashes and asterisks
        return licNo.Replace("/", string.Empty).Replace("*", string.Empty);
    }

    /// <summary>
    /// Converts date strings in formats like "27Dec2017" or "27 December2017" to "dd/mm/yyyy" format.
    /// If conversion fails, returns the original string unchanged.
    /// </summary>
    /// <param name="inputDateString">The date string to convert</param>
    /// <returns>Formatted date string in "dd/mm/yyyy" format or original string if conversion fails</returns>
    public static string? ConvertDateToStandardFormat(string? inputDateString)
    {
        if (string.IsNullOrWhiteSpace(inputDateString))
        {
            return inputDateString;
        }

        var dateString = DateFormatConsistent(inputDateString);
        
        // Try to parse various date formats
        string[] formats = {
            "ddMMMyyyy",        // 27Dec2017
            "dd MMM yyyy",      // 27 Dec 2017
            "dd MMMM yyyy",     // 27 December 2017
            "ddMMMMyyyy",       // 27December2017
            "dd/MM/yyyy",       // Already in target format
            "MM/dd/yyyy",       // US format
            "yyyy-MM-dd",       // ISO format
            "dd-MM-yyyy"        // Alternative format
        };
        
        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(dateString!.Trim(), format, 
                System.Globalization.CultureInfo.InvariantCulture, 
                System.Globalization.DateTimeStyles.None, out DateTime result))
            {
                return result.ToString("dd/MM/yyyy");
            }
        }
        
        // If none of the specific formats work, try general parsing
        if (DateTime.TryParse(dateString, out DateTime generalResult))
        {
            return generalResult.ToString("dd/MM/yyyy");
        }
        
        // If all parsing attempts fail, return the original string
        return string.Empty;
    }
    
    /// <summary>
    /// Converts date strings in formats like "27Dec2017" or "27 December2017" to "dd/mm/yyyy" format.
    /// If conversion fails, returns the original string unchanged.
    /// </summary>
    /// <param name="inputDateString">The date string to convert</param>
    /// <returns>Formatted date string in "dd/mm/yyyy" format or original string if conversion fails</returns>
    public static DateTime? ConvertDateToStandardFormatReturnDate(string? inputDateString)
    {
        if (string.IsNullOrWhiteSpace(inputDateString))
        {
            return null;
        }

        var dateString = DateFormatConsistent(inputDateString);
        
        // Try to parse various date formats
        string[] formats = {
            "ddMMMyyyy",        // 27Dec2017
            "dd MMM yyyy",      // 27 Dec 2017
            "dd MMMM yyyy",     // 27 December 2017
            "ddMMMMyyyy",       // 27December2017
            "dd/MM/yyyy",       // Already in target format
            "MM/dd/yyyy",       // US format
            "yyyy-MM-dd",       // ISO format
            "dd-MM-yyyy"        // Alternative format
        };
        
        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(dateString!.Trim(), format, 
                    System.Globalization.CultureInfo.InvariantCulture, 
                    System.Globalization.DateTimeStyles.None, out DateTime result))
            {
                return result;
            }
        }
        
        // If none of the specific formats work, try general parsing
        if (DateTime.TryParse(dateString, out DateTime generalResult))
        {
            return generalResult;
        }
        
        // If all parsing attempts fail, return the original string
        return null;
    }
    
    private static void ReplaceIfContains(string input, string match, string replaceWith, out string output)
    {
        output = input;

        if (!input.Contains(match, StringComparison.InvariantCultureIgnoreCase))
        {
            return;
        }
        
        output = input.Replace(match, replaceWith, StringComparison.InvariantCultureIgnoreCase);
    }

    private static string? DateFormatConsistent(string? input)
    {
        if (input == null)
        {
            return null;
        }
        
        // Check if its already in the correct format
        if (DateTime.TryParse(input, out _))
        {
            return input;
        }
        
        ReplaceIfContains(input, " ", string.Empty, out input);
        ReplaceIfContains(input, "first", "1", out input);
        ReplaceIfContains(input, "second", "2", out input);
        ReplaceIfContains(input, "third", "3", out input);
        ReplaceIfContains(input, "fourth", "4", out input);
        ReplaceIfContains(input, "fifth", "5", out input);
        ReplaceIfContains(input, "sixth", "6", out input);
        ReplaceIfContains(input, "seventh", "7", out input);
        ReplaceIfContains(input, "eighth", "8", out input);
        ReplaceIfContains(input, "ninth", "9", out input);
        ReplaceIfContains(input, "tenth", "10", out input);
        ReplaceIfContains(input, "eleventh", "11", out input);
        ReplaceIfContains(input, "twelfth", "12", out input);
        ReplaceIfContains(input, "thirteenth", "13", out input);
        ReplaceIfContains(input, "fourteenth", "14", out input);
        ReplaceIfContains(input, "fifteenth", "15", out input);
        ReplaceIfContains(input, "sixteenth", "16", out input);
        ReplaceIfContains(input, "seventeenth", "17", out input);
        ReplaceIfContains(input, "eighteenth", "18", out input);
        ReplaceIfContains(input, "nineteenth", "19", out input);
        ReplaceIfContains(input, "twentieth", "20", out input);
        ReplaceIfContains(input, "twenty-first", "21", out input);
        ReplaceIfContains(input, "twenty-second", "22", out input);
        ReplaceIfContains(input, "twenty-third", "23", out input);
        ReplaceIfContains(input, "twenty-fourth", "24", out input);
        ReplaceIfContains(input, "twenty-fifth", "25", out input);
        ReplaceIfContains(input, "twenty-sixth", "26", out input);
        ReplaceIfContains(input, "twenty-seventh", "27", out input);
        ReplaceIfContains(input, "twenty-eighth", "28", out input);
        ReplaceIfContains(input, "twenty-ninth", "29", out input);
        ReplaceIfContains(input, "thirtieth", "30", out input);
        ReplaceIfContains(input, "thirty-first", "31", out input);
        ReplaceIfContains(input, "August", "Aug", out input);
        ReplaceIfContains(input, "DAYOF", string.Empty, out input);
        ReplaceIfContains(input, "st", string.Empty, out input);
        ReplaceIfContains(input, "nd", string.Empty, out input);
        ReplaceIfContains(input, "rd", string.Empty, out input);
        ReplaceIfContains(input, "IEH", string.Empty, out input); // misreading of TH
        ReplaceIfContains(input, "th", string.Empty, out input);
        
        ReplaceIfContains(input, "NAY", "MAY", out input); // misreading of TH - TODO should use autocorrect
        
        ReplaceIfContains(input, "196g", "1966", out input); // TODO this should be more generic (regex)
        ReplaceIfContains(input, "1575", "1975", out input); // TODO this should be more generic (regex)

        return input;
    }
    /// <summary>
    /// The permit number and file id that together key the local/S3 file inventory.
    /// </summary>
    /// <param name="filename">
    /// Any of "{permit}__{fileId}", "{prefix}__{permit}__{fileId}" and
    /// "{permit}__{fileId}__{description}". The permit is always the part before the Guid, which
    /// is why this seeks the Guid rather than counting parts.
    /// </param>
    /// <returns>Null when no part is a usable Guid.</returns>
    public static (string PermitNumber, Guid FileId)? ParseIngressFilename(string? filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return null;
        }

        var parts = Path.GetFileNameWithoutExtension(filename).Split("__");

        // From one, because a permit part has to precede the id part.
        for (var index = 1; index < parts.Length; index++)
        {
            if (!Guid.TryParse(parts[index].Trim(), out var fileId) || fileId == Guid.Empty)
            {
                continue;
            }

            var permitNumber = parts[index - 1].Trim().Replace(" ", string.Empty).ToLower();

            if (permitNumber.Length > 0)
            {
                return (permitNumber, fileId);
            }
        }

        return null;
    }

    public static string ExtractFilenameFromUrl(string url)
    {
        // The Uri class helps handle URL decoding and standardisation first
        // before using Path methods. This is optional but helpful for complex URLs.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return Path.GetFileName(url);
        }
        
        // Get the local path component (e.g., "/path/to/file.txt")
        var localPath = uri.LocalPath;

        // Use Path.GetFileName to extract the final part
        var filename = Path.GetFileName(localPath);

        // Handle case where input isn't a valid absolute URI
        // Fall back to just using Path.GetFileName directly on the input string
        return filename;        
    }
}