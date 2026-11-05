using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using WALE.ProcessFile.Core.Models.Dms;

namespace WRADI.Services.LicenceFinder.Helpers;

/// <summary>
/// Static helper class containing shared methods for license matching rules
/// </summary>
public static class RuleHelpers
{
    /// <summary>
    /// Processes a priority level for license matching
    /// </summary>
    /// <param name="matchingRecords">All available records to filter</param>
    /// <param name="priorityFilter">Function to filter records for this priority level</param>
    /// <param name="ruleName">Base rule name</param>
    /// <param name="priorityLabel">Label for this priority level (e.g., "Priority 1")</param>
    /// <returns>Match result tuple or null if no matches at this priority</returns>
    private static (DmsExtract? dmsExtract, string? ruleName, bool hasDuplicate)? ProcessPriorityLevel(
        List<DmsExtract> matchingRecords,
        Func<DmsExtract, bool> priorityFilter,
        string ruleName,
        string priorityLabel)
    {
        var priorityMatches = matchingRecords.Where(priorityFilter).ToList();

        if (!priorityMatches.Any())
        {
            return null;
        }

        var (document, hasSameDateDuplicates) = SelectLatestDocument(priorityMatches);

        if (hasSameDateDuplicates)
        {
            return (document, $"{ruleName} - Multiple Matches", true);
        }

        return (document, $@"{ruleName} - {priorityLabel}", priorityMatches.Count > 1);
    }

    public static (DmsExtract? dmsExtract, string? ruleName, bool hasDuplicate) FindPriorityMatch(List<DmsExtract> matchingRecords, string ruleName)
    {
        // Define priority levels with their filters and labels
        var priorityLevels = new[]
        {
            (filter: dms => ContainsLicenseVariationPriority1(dms.FileName), label: "Priority 1"),
            (filter: dms => ContainsLicenseVariationPriority2(dms.FileName), label: "Priority 2"),
            (filter: dms => ContainsLicenseVariationPriority3(dms.FileName), label: "Priority 3"),
            (filter: new Func<DmsExtract, bool>(dms => ContainsLicenseVariationPriority4(dms.FileName, dms.PermitNumber)), label: "Permit Number Match - Priority 4")
        };

        // Process each priority level in order
        foreach (var (filter, label) in priorityLevels)
        {
            var result = ProcessPriorityLevel(matchingRecords, filter, ruleName, label);
            
            if (result.HasValue)
            {
                return result.Value;
            }
        }

        // No priority matches found
        return (null, null, false);
    }
    
    /// <summary>
    /// Checks if a filename should be excluded from license matching based on specific terms
    /// </summary>
    /// <param name="fileName">The filename to check</param>
    /// <returns>True if the filename should be excluded, false otherwise</returns>
    private static bool ShouldExcludeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var exclusionTerms = new[] { "letter", "schedule", "addendum" };

        return exclusionTerms.Any(term => fileName.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if a filename contains variations of the word "license" priority 1
    /// </summary>
    /// <param name="fileName">The filename to check</param>
    /// <returns>True if the filename contains license variations, false otherwise</returns>
    private static bool ContainsLicenseVariationPriority1(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;            
        }

        // Exclude if filename contains exclusion terms
        if (ShouldExcludeFileName(fileName))
        {
            return false;
        }

        // List of exact terms and their common misspellings to match
        var licenseTerms = new[]
        {
            "Issued Licence",
            "Licence Issued", 
            "issue licence",
            "Issued Licece",
            "Issued License"
        };

        // Check if filename contains any of the specified terms (case-insensitive)
        return licenseTerms.Any(term => fileName.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// Checks if a filename contains variations of the word "license" priority 2
    /// </summary>
    /// <param name="fileName">The filename to check</param>
    /// <returns>True if the filename contains license variations, false otherwise</returns>
    private static bool ContainsLicenseVariationPriority2(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        // Exclude if filename contains exclusion terms
        if (ShouldExcludeFileName(fileName))
        {
            return false;
        }

        // List of exact terms to match for Priority 2
        var licenseTerms = new[]
        {
            "New Signed Licence",
            "Non-Application Licence Document",
            "License Issued",
            "Licence document issued",
            "Licence Document",
            "Application New Licence",
            "Application New License",
            "Application New Licence Issued",
            "Original Licence",
            "Original License",
            "Application New Issued",
            "Original Existing Licence"
        };

        // Check if filename contains any of the specified terms (case-insensitive)
        return licenseTerms.Any(term => fileName.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if a filename contains variations of "Abstraction Licence" priority 3
    /// </summary>
    /// <param name="fileName">The filename to check</param>
    /// <returns>True if the filename contains Abstraction Licence variations, false otherwise</returns>
    private static bool ContainsLicenseVariationPriority3(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        // Exclude if filename contains exclusion terms
        if (ShouldExcludeFileName(fileName))
            return false;

        // Pattern to match "Abstraction Licence" and common misspellings
        // Handles: Abstraction Licence, Abstaction Licence, Abstarction Licence, Abstraction License, etc.
        var abstractionLicencePattern = @"abst?r?action\s+licen[cs]e?";

        return Regex.IsMatch(fileName, abstractionLicencePattern, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Checks if a filename contains general license variations priority 4
    /// </summary>
    /// <param name="fileName">The filename to check</param>
    /// <param name="permitNo"></param>
    /// <returns>True if the filename contains general license variations, false otherwise</returns>
    public static bool ContainsLicenseVariationPriority4(string fileName, string permitNo)
    {
        // Exclude if filename contains exclusion terms
        if (ShouldExcludeFileName(fileName))
        {
            return false;
        }

        return ContainsPermitNumberPattern(fileName, permitNo);
    }

    /// <summary>
    /// Extracts the second-to-last path segment from a URL with fallback handling
    /// </summary>
    /// <param name="fileUrl">The file URL to parse</param>
    /// <returns>The second-to-last path segment or null if not available</returns>
    private static string? GetSecondToLastPathSegment(string fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            return null;
        }

        try
        {
            // Parse the URL and get the path segments
            var uri = new Uri(fileUrl);
            var pathSegments = uri.LocalPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            // We need at least 2 segments to get the second-to-last one
            if (pathSegments.Length < 2)
            {
                return null;
            }

            // Get the second-to-last path segment (exclude the filename which is the last segment)
            return pathSegments[pathSegments.Length - 2];
        }
        catch (UriFormatException)
        {
            // If URL parsing fails, fall back to simple string matching
            var pathSegments = fileUrl.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (pathSegments.Length < 2)
            {
                return null;
            }

            return pathSegments[pathSegments.Length - 2];
        }
    }

    /// <summary>
    /// Checks if a file URL is in a Permit Documents folder by examining the second-to-last path segment
    /// </summary>
    /// <param name="fileUrl">The file URL to check</param>
    /// <returns>True if the URL's second-to-last folder matches Permit variations</returns>
    public static bool IsInPermitDocumentsFolder(string fileUrl)
    {
        var secondToLastFolder = GetSecondToLastPathSegment(fileUrl);

        if (string.IsNullOrWhiteSpace(secondToLastFolder))
        {
            return false;
        }

        // Check if the second-to-last folder contains Permit variations
        return secondToLastFolder.Contains("Permit", StringComparison.OrdinalIgnoreCase) ||
               secondToLastFolder.Contains("PermitDoc", StringComparison.OrdinalIgnoreCase) ||
               secondToLastFolder.Contains("PermitDocuments", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a file URL is in an Application & Associated Docs folder or LIB folder by examining the second-to-last path segment
    /// </summary>
    /// <param name="fileUrl">The file URL to check</param>
    /// <returns>True if the URL's second-to-last folder matches Application & Associated Docs or LIB patterns</returns>
    public static bool IsInApplicationAssociatedDocsFolder(string fileUrl)
    {
        var secondToLastFolder = GetSecondToLastPathSegment(fileUrl);

        if (string.IsNullOrWhiteSpace(secondToLastFolder))
        {
            return false;
        }

        // Pattern to match "Application & Associated Docs" and variations
        var applicationDocsPattern = @"^application\s*&?\s*associated\s*docs?$";

        // Pattern to match "LIB" followed by numbers and forward slash (e.g., LIB1/, LIB7/, etc.)
        var libPattern = @"^lib\d+/$";

        return Regex.IsMatch(secondToLastFolder, applicationDocsPattern, RegexOptions.IgnoreCase)
            || Regex.IsMatch(secondToLastFolder, libPattern, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Checks if a filename contains a permit number in various forms and patterns
    /// </summary>
    /// <param name="fileName">The filename to search in</param>
    /// <param name="permitNo">The permit number to search for (e.g., "633303G0038")</param>
    /// <returns>True if the filename contains the permit number in some recognizable form</returns>
    private static bool ContainsPermitNumberPattern(string fileName, string permitNo)
    {
        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(permitNo))
        {
            return false;
        }

        // Remove any non-alphanumeric characters from filename for comparison
        var cleanFileName = Regex.Replace(fileName, @"[^a-zA-Z0-9]", "", RegexOptions.IgnoreCase);

        // Strategy 1: Exact match of cleaned permit number at start
        if (cleanFileName.StartsWith(permitNo, StringComparison.OrdinalIgnoreCase))
        {
            var remainingAfterPermit = cleanFileName[permitNo.Length..];

            if (IsValidRemainingContent(remainingAfterPermit))
            {
                return true;
            }
        }

        // Strategy 2: Match with common separators (-, _, /, \, space, etc.) at start
        var permitWithSeparators = "^" + string.Join(@"[\-_/\\\s]*", permitNo.ToCharArray());
        var match = Regex.Match(fileName, permitWithSeparators, RegexOptions.IgnoreCase);
        
        if (match.Success)
        {
            var remainingAfterMatch = fileName.Substring(match.Length);

            if (IsValidRemainingContentWithSeparators(remainingAfterMatch))
            {
                return true;
            }
        }

        // Strategy 3: Enhanced pattern matching for segmented numbers at start
        // Handle cases like "2-27-17-129" matching "22717129"
        var segmentedPattern = CreateSegmentedPermitPattern(permitNo);
        if (!string.IsNullOrEmpty(segmentedPattern))
        {
            var segmentedMatch = Regex.Match(fileName, "^" + segmentedPattern, RegexOptions.IgnoreCase);
            
            if (segmentedMatch.Success)
            {
                var remainingAfterMatch = fileName.Substring(segmentedMatch.Length);
                
                if (IsValidRemainingContentWithSeparators(remainingAfterMatch))
                {
                    return true;
                }
            }
        }

        // Strategy 4: Partial matching - check for significant chunks (at least 4 characters) at start
        if (permitNo.Length >= 6)
        {
            // Split permit number into meaningful chunks and check each at start
            var chunks = GetPermitNumberChunks(permitNo);
            
            foreach (var chunk in chunks)
            {
                if (chunk.Length < 4 || !cleanFileName.StartsWith(chunk, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                
                var remainingAfterChunk = cleanFileName.Substring(chunk.Length);

                if (IsValidRemainingContent(remainingAfterChunk))
                {
                    return true;
                }
            }
        }

        // Strategy 5: Pattern-based matching for common permit number formats at start
        var flexiblePattern = CreateFlexiblePermitPattern(permitNo);
        
        if (!string.IsNullOrEmpty(flexiblePattern))
        {
            var flexibleMatch = Regex.Match(fileName, "^" + flexiblePattern, RegexOptions.IgnoreCase);
            
            if (flexibleMatch.Success)
            {
                var remainingAfterMatch = fileName.Substring(flexibleMatch.Length);

                if (IsValidRemainingContentWithSeparators(remainingAfterMatch))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Validates that the remaining content after permit number contains only numbers
    /// </summary>
    /// <param name="remaining">The remaining content to validate</param>
    /// <returns>True if content is empty or contains only numbers</returns>
    private static bool IsValidRemainingContent(string remaining)
    {
        if (string.IsNullOrEmpty(remaining))
        {
            return true;
        }

        // Only allow numbers in the remaining content
        return Regex.IsMatch(remaining.Replace(".pdf", string.Empty, StringComparison.OrdinalIgnoreCase), @"^\d*$");
    }

    /// <summary>
    /// Validates that the remaining content after permit number contains only spaces and numbers
    /// </summary>
    /// <param name="remaining">The remaining content to validate</param>
    /// <returns>True if content is empty or contains only spaces and numbers</returns>
    private static bool IsValidRemainingContentWithSeparators(string remaining)
    {
        if (string.IsNullOrEmpty(remaining))
        {
            return true;
        }

        // Only allow spaces and numbers in the remaining content
        return Regex.IsMatch(remaining.Replace(".pdf", string.Empty, StringComparison.OrdinalIgnoreCase), @"^[\s\d]*$");
    }

    /// <summary>
    /// Breaks a permit number into meaningful chunks for partial matching
    /// </summary>
    /// <param name="permitNo">The permit number to chunk</param>
    /// <returns>List of chunks that can be used for partial matching</returns>
    private static List<string> GetPermitNumberChunks(string permitNo)
    {
        var chunks = new List<string>();

        // Extract numeric sequences (4+ digits)
        var numericChunks = Regex.Matches(permitNo, @"\d{4,}")
            .Select(m => m.Value);
        
        chunks.AddRange(numericChunks);

        // Extract alphanumeric sequences (4+ characters)
        var alphanumericChunks = Regex.Matches(permitNo, @"[a-zA-Z0-9]{4,}")
            .Select(m => m.Value);
        
        chunks.AddRange(alphanumericChunks);

        // Get the last 6 characters if permit is long enough
        if (permitNo.Length >= 6)
        {
            chunks.Add(permitNo.Substring(permitNo.Length - 6));
        }

        // Get the first 6 characters if permit is long enough
        if (permitNo.Length >= 6)
        {
            chunks.Add(permitNo.Substring(0, 6));
        }

        return chunks.Distinct().ToList();
    }

    /// <summary>
    /// Creates a segmented regex pattern for permit numbers that handles common segmentation patterns
    /// For example, "22717129" would match "2-27-17-129", "2/27/17/129", etc.
    /// </summary>
    /// <param name="permitNo">The permit number to create a pattern for</param>
    /// <returns>A regex pattern that matches segmented variations of the permit number</returns>
    private static string CreateSegmentedPermitPattern(string permitNo)
    {
        if (string.IsNullOrWhiteSpace(permitNo) || permitNo.Length < 6)
        {
            return string.Empty;
        }

        var patterns = new List<string>();

        // Pattern 1: Single digit segments (e.g., "22717129" -> "2-2-7-1-7-1-2-9")
        var singleDigitPattern = string.Join(@"[\-_/\\\s]+", permitNo.ToCharArray());
        patterns.Add(singleDigitPattern);

        // Pattern 2: Two-digit segments (e.g., "22717129" -> "22-71-71-29" or "2-27-17-129")
        if (permitNo.Length >= 8)
        {
            // Try different two-digit combinations
            for (var firstSegment = 1; firstSegment <= 3 && firstSegment < permitNo.Length; firstSegment++)
            {
                var segments = new List<string>();
                var remaining = permitNo;

                // First segment
                segments.Add(remaining.Substring(0, firstSegment));
                remaining = remaining.Substring(firstSegment);

                // Split remaining into 2-digit segments
                while (remaining.Length >= 2)
                {
                    segments.Add(remaining.Substring(0, 2));
                    remaining = remaining.Substring(2);
                }

                // Add any remaining characters
                if (remaining.Length > 0)
                {
                    segments.Add(remaining);
                }

                if (segments.Count > 1)
                {
                    var segmentPattern = string.Join(@"[\-_/\\\s]+", segments);
                    patterns.Add(segmentPattern);
                }
            }
        }

        // Combine all patterns with OR operator
        return patterns.Count > 0 ? $"({string.Join("|", patterns)})" : string.Empty;
    }

    /// <summary>
    /// Creates a flexible regex pattern that can match permit numbers with various separators
    /// </summary>
    /// <param name="permitNo">The permit number to create a pattern for</param>
    /// <returns>A regex pattern that matches the permit number in various formats</returns>
    private static string CreateFlexiblePermitPattern(string permitNo)
    {
        if (string.IsNullOrWhiteSpace(permitNo))
        {
            return string.Empty;
        }

        // Create pattern that allows for separators between groups of characters
        var chars = permitNo.ToCharArray();
        var patternParts = new List<string>();

        for (var i = 0; i < chars.Length; i++)
        {
            var escapedChar = Regex.Escape(chars[i].ToString());
            patternParts.Add(escapedChar);

            // Add optional separator pattern between characters (but not after the last character)
            if (i < chars.Length - 1)
            {
                patternParts.Add(@"[\-_/\\\s]*");
            }
        }

        return string.Join("", patternParts);
    }

    /// <summary>
    /// Selects the latest document from a collection of DMS records based on document date first, then upload date.
    /// Returns null if the collection is empty or if multiple records have the same latest dates.
    /// </summary>
    /// <param name="dmsRecords">Collection of DMS records to select from</param>
    /// <returns>Tuple containing the latest DMS record (or null) and whether duplicates with same dates exist</returns>
    private static (DmsExtract? document, bool hasSameDateDuplicates) SelectLatestDocument(IEnumerable<DmsExtract> dmsRecords)
    {
        var recordsList = dmsRecords.ToList();

        if (recordsList.Count == 0)
        {
            return (null, false);
        }

        if (recordsList.Count == 1)
        {
            return (recordsList[0], false);            
        }
        
        // Order by DocumentDate descending (latest first), then by UploadDate descending
        var orderedRecords = recordsList
            .OrderByDescending(r => GetSafeDateTime(r.DocumentDate))
            .ThenByDescending(r => GetSafeDateTime(r.UploadDate))
            .ToList();

        var latestRecord = orderedRecords.First();
        var latestDocDate = GetSafeDateTime(latestRecord.DocumentDate);
        var latestUploadDate = GetSafeDateTime(latestRecord.UploadDate);

        // Check if there are multiple records with the same latest dates
        var duplicatesWithSameDates = orderedRecords
            .Count(r => GetSafeDateTime(r.DocumentDate) == latestDocDate
                && GetSafeDateTime(r.UploadDate) == latestUploadDate) > 1;

        return (latestRecord, duplicatesWithSameDates);
    }

    /// <summary>
    /// Safely converts a string date to a DateTime for sorting purposes.
    /// Null, empty, or unparseable values are treated as the oldest date (DateTime.MinValue).
    /// Supports multiple common date formats.
    /// </summary>
    /// <param name="dateString">The string date to convert</param>
    /// <returns>DateTime.MinValue if null/empty/unparseable, otherwise the parsed DateTime value</returns>
    private static DateTime GetSafeDateTime(string dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
        {
            return DateTime.MinValue;
        }

        // Common date formats to try parsing
        var dateFormats = new[]
        {
            "yyyy-MM-dd",           // ISO format: 2023-12-25
            "dd/MM/yyyy",           // UK format: 25/12/2023
            "MM/dd/yyyy",           // US format: 12/25/2023
            "dd-MM-yyyy",           // European: 25-12-2023
            "yyyy/MM/dd",           // Alternative ISO: 2023/12/25
            "dd/MM/yy",             // Short UK: 25/12/23
            "MM/dd/yy",             // Short US: 12/25/23
            "yyyy-MM-dd HH:mm:ss",  // ISO with time: 2023-12-25 14:30:00
            "dd/MM/yyyy HH:mm:ss",  // UK with time: 25/12/2023 14:30:00
            "MM/dd/yyyy HH:mm:ss"   // US with time: 12/25/2023 14:30:00
        };

        // Try parsing with specific formats first
        foreach (var format in dateFormats)
        {
            if (DateTime.TryParseExact(dateString.Trim(), format,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var exactResult))
            {
                return exactResult;
            }
        }

        // Fall back to general DateTime.TryParse as last resort
        if (DateTime.TryParse(dateString.Trim(), out var generalResult))
        {
            return generalResult;
        }

        // If all parsing fails, return minimum value (oldest date)
        return DateTime.MinValue;
    }
}