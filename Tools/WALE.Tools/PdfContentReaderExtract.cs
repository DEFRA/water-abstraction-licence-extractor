using System.Globalization;
using System.Text;
using CsvHelper;
using Tesseract;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Services.PdfPig;
using WALE.Tools.Helpers;
using WALE.Tools.Models;

namespace WALE.Tools;

public static class PdfContentReaderExtract
{
    private static readonly string OutputFolder = KeyConfig.OutputFolder;
    private static readonly string CacheFolder = KeyConfig.CacheFolder;
    private static readonly Dictionary<string, string> FileLicenceMapping = new() {{"", ""}};
    private static readonly IPdfDataExtractorService PdfExtractorService = new PdfDataExtractorService(
        new PdfPigNoOcrDataExtractorService(),
        new List<IOcrDataExtractorService>
        {
            new TesseractOcrDataExtractorService(KeyConfig.TesseractPrefix, PageSegMode.Auto)
        },
        KeyConfig.PdfFolder);

    public static async Task GeneratePdfContentReaderExtractAsync()
    {
        // Step 1: Get all PDF files from the configured folder
        var pdfFilePaths = Directory
            .GetFiles(KeyConfig.PdfFolder)
            .Where(fileName => fileName.EndsWith(".pdf", StringComparison.InvariantCultureIgnoreCase))
            .OrderBy(x => x)
            .ToList();

        var csvResults = new List<PdfContentCsvLine>();

        // Step 2: Process each PDF file to extract all content
        foreach (var pdfFilePath in pdfFilePaths)
        {
            try
            {
                Console.WriteLine($"Processing PDF: {Path.GetFileName(pdfFilePath)}");

                var contentResults = await ExtractAllPdfContentAsync(pdfFilePath);
                csvResults.AddRange(contentResults);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing {Path.GetFileName(pdfFilePath)}: {ex.Message}");

                // Add entry with error information
                csvResults.Add(new PdfContentCsvLine
                {
                    FileName = Path.GetFileName(pdfFilePath),
                    FilePath = pdfFilePath,
                    PageNumber = 0,
                    Headers = "ERROR",
                    Content = $"Error extracting content: {ex.Message}",
                    LicenseNumbers = "",
                    ProcessingDate = DateTime.Now
                });
            }
        }

        // Step 3: Save results to CSV file using ToolHelper
        await ToolHelper.GenerateCsvReportWithSummaryAsync(
            csvResults,
            "PDF-Content-Extract",
            OutputFolder,
            x => x.FileName,
            "content entries",
            "Files Summary");

        Console.WriteLine($"Total files processed: {pdfFilePaths.Count}");
    }

    private static async Task<List<PdfContentCsvLine>> ExtractAllPdfContentAsync(string pdfFilePath)
    {
        Console.WriteLine($"Extracting content from: {Path.GetFileName(pdfFilePath)}");
        var results = new List<PdfContentCsvLine>();

        try
        {
            // Create configuration for content extraction - use empty labels to get all content
            var labels = new List<(string LabelGroupName, List<LabelToMatch> Labels)>();
            var configuration = new LookupConfiguration(
                labels,
                FileLicenceMapping,
                OutputFolder,
                CacheFolder);

            // Extract pages using the PDF service
            var result = await PdfExtractorService.GetPagesAsync(pdfFilePath, configuration);

            if (result?.Pages == null || !result.Pages.Any())
            {
                results.Add(new PdfContentCsvLine
                {
                    FileName = Path.GetFileName(pdfFilePath),
                    FilePath = pdfFilePath,
                    PageNumber = 0,
                    Headers = "",
                    Content = "No content could be extracted",
                    LicenseNumbers = "",
                    ProcessingDate = DateTime.Now
                });
                return results;
            }

            // Extract all text content from each page separately
            foreach (var page in result.Pages)
            {
                Console.WriteLine($"Processing page {page.Number} of {Path.GetFileName(pdfFilePath)}");

                // Extract all content from the page and build header-content pairs
                var allText = GetAllTextFromPage(page);
                var headerContentPairs = BuildHeaderContentPairs(allText, page.Number, page.NumberOfImages, page.ImageFilepath);

                // Create separate entries for each header-content pair
                foreach (var pair in headerContentPairs)
                {
                    // Extract license numbers from both header and content
                    var licenseNumbers = ExtractLicenseNumbers(pair.Content);

                    results.Add(new PdfContentCsvLine
                    {
                        FileName = Path.GetFileName(pdfFilePath),
                        FilePath = pdfFilePath,
                        PageNumber = page.Number,
                        Headers = pair.Header,
                        Content = pair.Content,
                        LicenseNumbers = licenseNumbers,
                        ProcessingDate = DateTime.Now
                    });
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting content from {pdfFilePath}: {ex.Message}");
            throw;
        }
    }
    private static List<HeaderContentPair> BuildHeaderContentPairs(List<string> allText, int pageNumber, int numberOfImages, string? imageFilepath)
    {
        var pairs = new List<HeaderContentPair>();

        try
        {
            // First, let's parse the text more carefully to handle cases where headers might be embedded in longer strings
            var processedLines = new List<string>();

            foreach (var line in allText)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Split lines that might contain multiple headers/content pieces
                var splitLines = SplitLineWithHeaders(line.Trim());
                processedLines.AddRange(splitLines);
            }

            Console.WriteLine($"DEBUG: Processing {processedLines.Count} lines for page {pageNumber}");

            var currentHeader = "";
            var currentContent = new StringBuilder();
            var foundFirstHeader = false;

            // Add page metadata
            var pageMetadata = new StringBuilder();
            pageMetadata.AppendLine($"=== Page {pageNumber} Metadata ===");
            pageMetadata.AppendLine($"Number of Images: {numberOfImages}");
            if (!string.IsNullOrEmpty(imageFilepath))
            {
                pageMetadata.AppendLine($"Image File: {imageFilepath}");
            }

            foreach (var line in processedLines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmedLine))
                    continue;

                bool isHeader = IsHeaderLine(trimmedLine);
                Console.WriteLine($"DEBUG: Line '{trimmedLine.Substring(0, Math.Min(50, trimmedLine.Length))}...' isHeader: {isHeader}");

                if (isHeader)
                {
                    // Save previous header-content pair if we have content
                    if (foundFirstHeader && (!string.IsNullOrWhiteSpace(currentHeader) || currentContent.Length > 0))
                    {
                        pairs.Add(new HeaderContentPair
                        {
                            Header = string.IsNullOrWhiteSpace(currentHeader) ? "[No header]" : currentHeader,
                            Content = currentContent.ToString().Trim()
                        });
                    }

                    // Start new header-content pair
                    currentHeader = trimmedLine;
                    currentContent.Clear();
                    foundFirstHeader = true;
                    Console.WriteLine($"DEBUG: Found header: {currentHeader}");
                }
                else if (foundFirstHeader)
                {
                    // This is content under the current header
                    currentContent.AppendLine(trimmedLine);
                }
                else
                {
                    // Content before any header found
                    if (string.IsNullOrWhiteSpace(currentHeader))
                    {
                        currentHeader = "[Content before headers]";
                    }
                    currentContent.AppendLine(trimmedLine);
                }
            }

            // Add the last header-content pair
            if (!string.IsNullOrWhiteSpace(currentHeader) || currentContent.Length > 0)
            {
                pairs.Add(new HeaderContentPair
                {
                    Header = string.IsNullOrWhiteSpace(currentHeader) ? "[No header]" : currentHeader,
                    Content = currentContent.ToString().Trim()
                });
            }

            Console.WriteLine($"DEBUG: Created {pairs.Count} header-content pairs");

            // If no pairs were created, add all content as one pair
            if (pairs.Count == 0)
            {
                var allContentText = string.Join("\n", processedLines.Where(line => !string.IsNullOrWhiteSpace(line)));
                pairs.Add(new HeaderContentPair
                {
                    Header = "[No headers found]",
                    Content = pageMetadata.ToString() + "\n" + allContentText
                });
            }
            else
            {
                // Add page metadata to the first pair's content
                if (pairs.Count > 0)
                {
                    pairs[0].Content = pageMetadata.ToString() + "\n" + pairs[0].Content;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error building header-content pairs: {ex.Message}");
            pairs.Add(new HeaderContentPair
            {
                Header = "[Error]",
                Content = $"Error building header-content pairs: {ex.Message}"
            });
        }

        return pairs;
    }

    private static List<string> SplitLineWithHeaders(string line)
    {
        var result = new List<string>();

        // Split by common header patterns using regex
        var headerPatterns = new[]
        {
            @"(\d+\.\s+[A-Z\s]+?)(?=\s+\d+\.\d+|\s+\d+\.|$)", // "1. HEADER TEXT" followed by numbered content or next header
            @"(SCHEDULE OF CONDITIONS)", // Specific header
            @"([A-Z\s]{10,}?)(?=\s+\d+\.\d+|\s+\d+\.|\s+[a-z])", // Long uppercase text followed by numbered content
        };

        var splitText = line;
        var foundSplits = new List<(int index, string text)>();

        // Find all header positions
        foreach (var pattern in headerPatterns)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(splitText, pattern);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                foundSplits.Add((match.Index, match.Value.Trim()));
            }
        }

        // If no patterns found, return the original line
        if (foundSplits.Count == 0)
        {
            result.Add(line);
            return result;
        }

        // Sort by position and create segments
        foundSplits = foundSplits.OrderBy(x => x.index).ToList();

        int lastEnd = 0;
        foreach (var (index, text) in foundSplits)
        {
            // Add content before this header if any
            if (index > lastEnd)
            {
                var beforeHeader = splitText.Substring(lastEnd, index - lastEnd).Trim();
                if (!string.IsNullOrWhiteSpace(beforeHeader))
                {
                    result.Add(beforeHeader);
                }
            }

            // Add the header
            result.Add(text);
            lastEnd = index + text.Length;
        }

        // Add remaining content after last header
        if (lastEnd < splitText.Length)
        {
            var remaining = splitText.Substring(lastEnd).Trim();
            if (!string.IsNullOrWhiteSpace(remaining))
            {
                result.Add(remaining);
            }
        }

        return result.Count > 0 ? result : new List<string> { line };
    }

    private static List<string> GetAllTextFromPage(PdfPage page)
    {
        var allLines = new List<string>();

        try
        {
            // Extract from all providers
            foreach (var provider in page.Providers)
            {
                if (provider.Text?.Any() == true)
                {
                    allLines.AddRange(provider.Text.Where(line => !string.IsNullOrWhiteSpace(line)));
                }
            }

            // Also extract from main Text property if available
            if (!string.IsNullOrEmpty(page.Text))
            {
                var lines = page.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                allLines.AddRange(lines.Where(line => !string.IsNullOrWhiteSpace(line)));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting all text from page: {ex.Message}");
        }

        return allLines;
    }
    private static bool IsHeaderLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;

        var trimmedLine = line.Trim();

        // Skip empty lines
        if (trimmedLine.Length == 0)
            return false;

        // 1. Check for numbered headers with dots (like "1.1", "2.1", etc.) - can be standalone or with text
        if (System.Text.RegularExpressions.Regex.IsMatch(trimmedLine, @"^\d+\.\d+"))
        {
            return true;
        }

        // 2. Check if line starts with a number and period/space followed by uppercase text (like "1. SOURCE OF SUPPLY")
        var numberedHeaderMatch = System.Text.RegularExpressions.Regex.Match(trimmedLine, @"^(\d+)\.\s+([A-Z\s]+)$");
        if (numberedHeaderMatch.Success)
        {
            var textPart = numberedHeaderMatch.Groups[2].Value.Trim();
            // Must be uppercase text
            if (textPart.Any(char.IsLetter) && textPart == textPart.ToUpperInvariant())
            {
                return true;
            }
        }

        // 3. Check if line is just a number (standalone section numbers like "1", "2", etc.) - but be more restrictive
        if (System.Text.RegularExpressions.Regex.IsMatch(trimmedLine, @"^\d+\.?$") && trimmedLine.Length <= 3)
        {
            return true;
        }

        // 4. Check if line is all uppercase (bold/uppercase headers)
        if (trimmedLine == trimmedLine.ToUpperInvariant() && trimmedLine.Any(char.IsLetter))
        {
            // Be more restrictive - don't treat fragments of numbered headers as separate headers
            // Skip if this looks like part of a numbered header that got split
            if (trimmedLine.Length > 3 && !ContainsObviousNonHeaderPattern(trimmedLine) && 
                !IsPartOfNumberedHeader(trimmedLine))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPartOfNumberedHeader(string text)
    {
        // Check if this text looks like it's the text part of a numbered header
        // These patterns suggest it's part of a "1. HEADER TEXT" that got split
        var commonHeaderWords = new[] 
        { 
            "SOURCE", "SUPPLY", "ABSTRACTION", "PURPOSE", "PERIOD", "QUANTITY", 
            "POINT", "POINTS", "MEANS", "CONDITIONS", "SCHEDULE", "MAXIMUM"
        };

        return commonHeaderWords.Any(word => text.Contains(word));
    }

    private static bool ContainsObviousNonHeaderPattern(string text)
    {
        // More specific checks for non-header patterns
        return ContainsDatePattern(text) || 
               IsNumberWithSpaces(text) ||
               text.Contains("@") || // email addresses
               text.Contains("www.") || // websites
               text.StartsWith("http") || // URLs
               (text.Length > 300) || // very long text is likely a paragraph
               System.Text.RegularExpressions.Regex.IsMatch(text, @"\d{4}-\d{2}-\d{2}") || // date patterns
               System.Text.RegularExpressions.Regex.IsMatch(text, @"Page \d+ of \d+"); // page numbers
    }

    private static bool ContainsDatePattern(string text)
    {
        // Simple check for common date patterns
        return text.Contains("/") && (text.Contains("19") || text.Contains("20")) ||
               text.Contains("-") && (text.Contains("19") || text.Contains("20"));
    }

    private static bool IsNumberWithSpaces(string text)
    {
        // Check if it's mostly numbers with spaces (like "1 2 3 4 5")
        var withoutSpaces = text.Replace(" ", "");
        return withoutSpaces.Length > 0 && withoutSpaces.All(char.IsDigit) && text.Contains(" ");
    }

    private static string ExtractLicenseNumbers(string text)
    {
        var licenseNumbers = new HashSet<string>();

        try
        {
            // Split text into potential words/tokens that could contain license numbers
            var words = text.Split(new[] { ' ', '\t', '\n', '\r', ',', ';', ':', '(', ')', '[', ']', '{', '}' }, 
                                  StringSplitOptions.RemoveEmptyEntries);

            // Process each word individually and also check combinations
            var allPotentialTexts = new List<string>(words);

            // Add combinations of adjacent words for multi-part license numbers
            for (int i = 0; i < words.Length - 1; i++)
            {
                allPotentialTexts.Add($"{words[i]} {words[i + 1]}");
                if (i < words.Length - 2)
                {
                    allPotentialTexts.Add($"{words[i]} {words[i + 1]} {words[i + 2]}");
                }
            }

            // Also check the entire text as one piece
            allPotentialTexts.Add(text);

            // Get license number labels configuration
            var licenseNumberLabels = LicenceReaderConfiguration.GetLicenceNumberLabels();

            foreach (var potentialText in allPotentialTexts)
            {
                if (string.IsNullOrWhiteSpace(potentialText))
                    continue;

                // Create a DocumentLine from the text to use with LicenceNumber.AnyIsLicenceNumber
                var documentLine = new DocumentLine();
                documentLine.Columns.Add(new DocumentLineColumn(potentialText.Trim()));

                // Try each license number label configuration
                foreach (var label in licenseNumberLabels)
                {
                    if (LicenceNumber.AnyIsLicenceNumber(new[] { documentLine }, label, out var matchedLines))
                    {
                        foreach (var matchedLine in matchedLines)
                        {
                            foreach (var column in matchedLine.Columns)
                            {
                                if (!string.IsNullOrWhiteSpace(column.Text))
                                {
                                    var cleanedNumber = CleanLicenseNumber(column.Text);
                                    licenseNumbers.Add(cleanedNumber);
                                }
                            }
                        }
                    }
                }

                // Also try the existing LicenceNumber regex directly on each potential text
                var regex = LicenceNumber.LicenceNumbersRegex();
                var matches = regex.Matches(potentialText);

                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    var licenseNumber = CleanLicenseNumber(match.Value.Trim());
                    licenseNumbers.Add(licenseNumber);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting license numbers: {ex.Message}");
        }

        return string.Join(", ", licenseNumbers.OrderBy(x => x));
    }

    private static string CleanLicenseNumber(string licenseNumber)
    {
        // Remove extra spaces and normalize
        licenseNumber = licenseNumber.Trim();

        // Remove common prefixes that might have been captured
        var prefixesToRemove = new[] { "No:", "Number:", "Serial No:", "Licence Serial No:", "License Serial No:", "Permit:", "Reference:", "Ref:" };
        foreach (var prefix in prefixesToRemove)
        {
            if (licenseNumber.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                licenseNumber = licenseNumber.Substring(prefix.Length).Trim();
            }
        }

        return licenseNumber;
    }

    private static bool IsValidLicenseNumber(string licenseNumber)
    {
        if (string.IsNullOrWhiteSpace(licenseNumber))
            return false;

        // Accept both compact format and slash format
        var validPatterns = new[]
        {
            // Compact format patterns (no slashes)
            @"^\d{4,5}[A-Z]\d{4}[A-Z]?$", // 42901G0001 or 42901G0001A

            // Slash format patterns (existing)
            @"^\d{1,2}\/\d{1,2}\/\d{2}\/\*[A-Z]\/\d{4}$", // 4/29/01/*G/0001
            @"^\d{1,2}\/\d{1,2}\/\d{2}\/\*[A-Z]\/\d{4}\/[A-Z]\d{2}$", // 4/29/01/*S/0033/R01
            @"^\d{1,2}\/\d{1,2}\/\d{2}\/\*[A-Z]\/\d{4}[A-Z]$", // 4/29/03/*G/0023B
        };

        foreach (var pattern in validPatterns)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(licenseNumber, pattern))
            {
                return true;
            }
        }

        return false;
    }


    private class HeaderContentSection
    {
        public string Header { get; set; } = string.Empty;
        public List<string> Content { get; set; } = new();
    }

    private class HeaderContentPair
    {
        public string Header { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}
