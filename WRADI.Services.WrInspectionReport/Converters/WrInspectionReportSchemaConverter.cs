using System.Text;
using WALE.ProcessFile.Core.Models;
using WRADI.DocumentType.WrInspectionReport.Enums;
using WRADI.DocumentType.WrInspectionReport.Models;

namespace WRADI.DocumentType.WrInspectionReport.Converters;

public static class WrInspectionReportSchemaConverter
{
    public static Models.WrInspectionReport ToForm(MatchesResult matchesResult, DmsFileData? dmsFileData)
    {
        var rawFormDate = GetMultilineText(matchesResult, "Date");
        
        DateOnly? formDate = null;
        if (DateOnly.TryParse(rawFormDate, out var tFormDate))
        {
            formDate = tFormDate;
        }

        var rawInspectionDate = GetMultilineText(matchesResult, "InspectionDate");
        var rawInspectionTime = GetMultilineText(matchesResult, "Time");

        var currentDay = DateTime.Today;
        var currentYear = currentDay.Year;
        var currentYearLast2Digits = currentYear.ToString()[2..];
        
        DateTime? inspectionDateTime = null;
        if (!string.IsNullOrWhiteSpace(rawInspectionDate))
        {
            var potentialDates = new List<string>();
            
            // We sometimes get some weird stuff
            var rawInspectionDateTweaked = RemoveSpecialCharacters(rawInspectionDate);
            
            rawInspectionDateTweaked = rawInspectionDateTweaked
                .Replace("n/a", string.Empty) // This presumably comes from a later column
                .Replace("N/A", string.Empty) // This presumably comes from a later column                
                .Replace("Quantities:", string.Empty) // This shouldn't have really come through
                .Replace("Q u a n t i t i e s", string.Empty) // This shouldn't have really come through + still has the weird spaces issue
                .Replace("Date of certificate or", string.Empty) // This shouldn't have really come through
                .Replace("Desktop Review", string.Empty) // Don't know why we get this
                .Replace("NI", string.Empty) // Don't know why we get this
                .Replace("\r", string.Empty)
                .Replace("  ", " ");
            
            if (rawInspectionDateTweaked.Contains('&'))
            {
                var parts = rawInspectionDateTweaked.Split("&");
                rawInspectionDateTweaked = parts[1].Trim();
            }
            
            if (rawInspectionDateTweaked.Contains('+'))
            {
                var parts = rawInspectionDateTweaked.Split("+");
                rawInspectionDateTweaked = parts[1].Trim();
            }

            if (rawInspectionDateTweaked.EndsWith(" P"))
            {
                rawInspectionDateTweaked = rawInspectionDateTweaked[..^2].Trim();
            }
            
            if (rawInspectionDateTweaked.Contains("Inspecting Officer"))
            {
                var parts = rawInspectionDateTweaked.Split("Inspecting Officer");
                rawInspectionDateTweaked = parts[0].Trim();

                var lastLine = parts.Last().Split('\n').Last();
                potentialDates.Add(lastLine);
            }
            
            if (rawInspectionDateTweaked.Contains("Land"))
            {
                var parts = rawInspectionDateTweaked.Split("Land");
                rawInspectionDateTweaked = parts[0].Trim();
            }
            
            if (rawInspectionDateTweaked.Contains("Time"))
            {
                var parts = rawInspectionDateTweaked.Split("Time");
                rawInspectionDateTweaked = parts[0].Trim();

                // "Time" usually trails the date ("22/01/2026 Time: 0930"), but some template
                // layouts capture it first ("Time:\n27/01/26") - keep the existing before-Time
                // behaviour as the primary value, and add whatever follows "Time" as a fallback
                // candidate so that layout isn't silently discarded.
                var afterTime = parts.Length > 1 ? parts[^1].TrimStart(':').Trim() : string.Empty;

                if (!string.IsNullOrWhiteSpace(afterTime))
                {
                    potentialDates.Add(afterTime);

                    // Some layouts glue a time value directly after "Time" with no colon,
                    // and the actual date sits on the following line - e.g.
                    // "Time 12.20\n20/01/2026". The whole afterTime blob won't parse as one
                    // date, but its last line usually will.
                    if (afterTime.Contains('\n'))
                    {
                        var lastLineAfterTime = afterTime.Split('\n').Last().Trim();

                        if (!string.IsNullOrWhiteSpace(lastLineAfterTime))
                        {
                            potentialDates.Add(lastLineAfterTime);
                        }
                    }
                }
            }

            rawInspectionDateTweaked = rawInspectionDateTweaked
                .Replace("th", string.Empty)
                .Replace("st", string.Empty)
                .Replace("rd", string.Empty)
                .Replace("nd", string.Empty)
                .Replace("\n", " ")
                .Replace("  ", " ")
                .Trim();

            if (rawInspectionDateTweaked.EndsWith(" :"))
            {
                rawInspectionDateTweaked = rawInspectionDateTweaked[..^2];
            }
            
            if (rawInspectionTime?.Length == 4 && !rawInspectionDateTweaked.Contains(':'))
            {
                rawInspectionTime = $"{rawInspectionTime[..2]}:{rawInspectionTime[2..]}";
            }

            potentialDates.Add($"{rawInspectionDateTweaked} {rawInspectionTime}");
            potentialDates.Add(rawInspectionDateTweaked);

            if (rawInspectionDateTweaked.Count(c => c == '-') == 1)
            {
                var parts = rawInspectionDateTweaked.Split('-');
                potentialDates.Add(parts[0].Trim());
            }
            
            if (rawInspectionDateTweaked.Count(c => c == '.') == 1
                && rawInspectionDateTweaked.Count(c => c == ':') == 1
                && rawInspectionDateTweaked.All(c => c != '/')
                && rawInspectionDateTweaked.All(c => c != ' '))
            {
                potentialDates.Add(rawInspectionDateTweaked.Replace(".", ":"));
            }

            var words = rawInspectionDateTweaked.Split(' ');

            if (words.Length == 4)
            {
                potentialDates.Add($"{words[0]} {words[1]} {words[2]}");
                potentialDates.Add($"{words[1]} {words[2]} {words[3]}");
            }
            
            foreach (var potentialDate in potentialDates)
            {
                if (!DateTime.TryParse(potentialDate, out var tInspectionDateTime))
                {
                    continue;
                }
                
                // If we pulled a date that wasn't the current year, we must have contained a year
                var year = tInspectionDateTime.Year.ToString();
                var year2Digits = year.Length == 4 ? year.Substring(2, 2) : year;
                var containsYear = potentialDate.Contains($"/{year2Digits}")
                    || potentialDate.Contains($"/{year}")
                    || potentialDate.Contains($".{year2Digits}")
                    || potentialDate.Contains($".{year}")
                    || potentialDate.Contains($":{year2Digits}")
                    || potentialDate.Contains($":{year}")
                    || potentialDate.Contains($"-{year2Digits}")
                    || potentialDate.Contains($"-{year}")
                    || potentialDate.Contains($" {year2Digits}")
                    || potentialDate.Contains($" {year}");
                        
                // Needs to contain a year and should never be today
                if (!containsYear || tInspectionDateTime.Date == DateTime.Today)
                {
                    continue;
                }

                inspectionDateTime = tInspectionDateTime;
                break;
            }
        }

        var rawDateOfCertificateOrRecord = GetMultilineText(matchesResult, "DateOfCertification");
        DateOnly? dateOfCertificateOrRecord = null;
        if (DateOnly.TryParse(rawDateOfCertificateOrRecord, out var tDateOfCertificateOrRecord))
        {
            dateOfCertificateOrRecord = tDateOfCertificateOrRecord;
        }

        var nameAndAddress = GetMultilineText(matchesResult, "NameAndAddress");
        var siteAddress = GetMultilineText(matchesResult, "SiteAddress");

        if (siteAddress?.Equals("Same as above", StringComparison.InvariantCultureIgnoreCase) == true
            || siteAddress?.Equals("As above", StringComparison.InvariantCultureIgnoreCase) == true)
        {
            siteAddress = nameAndAddress;
        }

        var whereKept = GetMultilineText(matchesResult, "WhereKept");
        if (string.IsNullOrWhiteSpace(whereKept)) whereKept = null;

        var documentTemplateVerison = GetMultilineText(matchesResult, "DocumentTemplateVersion");
        var isNewTemplate = documentTemplateVerison == "2026_07_10_v1";

        var documentHeader = GetMultilineText(matchesResult, "DocumentHeader");
        if (!string.IsNullOrWhiteSpace(documentHeader)) documentHeader = $"Form WR - {documentHeader}";
        
        string? maintenanceYesNo = null;

        if (isNewTemplate)
        {
            maintenanceYesNo = GetSingleLineSubFieldText(matchesResult, "MaintenanceLine", "MaintenanceLineMaintenance");
        }
        else
        {
            var maintenanceYes = GetSingleLineSubFieldText(matchesResult, "MaintenanceLine", "MaintenanceLineMaintenanceYes");
            var maintenanceNo = GetSingleLineSubFieldText(matchesResult, "MaintenanceLine", "MaintenanceLineMaintenanceNo");

            if (maintenanceYes?.Equals("✓") == true)
            {
                maintenanceYesNo = "Yes";
            }
            else if (maintenanceNo?.Equals("✓") == true)
            {
                maintenanceYesNo = "No";
            }
        }

        string? readingsTakenYesNo = null;
        
        if (isNewTemplate)
        {
            readingsTakenYesNo = GetSingleLineSubFieldText(matchesResult, "ReadingsTakenLine", "ReadingsTakenLineReadingsTaken");
        }
        else
        {
            var readingsTakenYes = GetSingleLineSubFieldText(matchesResult, "ReadingsTakenLine", "ReadingsTakenLineReadingsTakenYes");
            var readingsTakenNo = GetSingleLineSubFieldText(matchesResult, "ReadingsTakenLine", "ReadingsTakenLineReadingsTakenNo");

            if (readingsTakenYes?.Equals("✓") == true)
            {
                readingsTakenYesNo = "Yes";
            }
            else if (readingsTakenNo?.Equals("✓") == true)
            {
                readingsTakenYesNo = "No";
            }
        }

        var images = new List<string>();
        var pageIndex = 1;

        var filenameNoExtension = Path.GetFileNameWithoutExtension(matchesResult.Filename);
        
        foreach (var page in matchesResult.Pages)
        {
            for (var idx = 1; idx <= page.NumberOfImages; idx++)
            {
                if (pageIndex == 1 && idx == 1)
                {
                    // Skip EA Logo
                    continue;
                }
                
                var imageIndex = pageIndex == 1 && idx >= 2 ? idx - 1 : idx;
                images.Add($"{filenameNoExtension}/pdfpig-page{pageIndex}-image{imageIndex}.jpg");
            }

            pageIndex += 1;
        }
        
        return new Models.WrInspectionReport()
        {
            Metadata = new WrInspectionReportMetadata()
            {
                DocumentTemplateVerison = documentTemplateVerison,
                DocumentHeader = documentHeader,
                Filename = matchesResult.Filename,
                FileId = dmsFileData?.FileId,
                IsScan = matchesResult.ScannedFile,
                FormSentTo = GetMultilineText(matchesResult, "FormSentTo"),
                Date = new WrInspectionReportInspectionDate()
                {
                    RawDate = rawFormDate,
                    Date = formDate   
                }
            },
            LicenceNumber = GetMultilineText(matchesResult, "LicenceNumber"),
            InspectionClass = GetMultilineText(matchesResult, "InspectionClass"),
            Address = new WrInspectionReportAddress()
            {
                NameAndAddress = nameAndAddress,
                TelephoneNumber = CollapseSpacedDigits(GetMultilineText(matchesResult, "TelephoneNumber")),
                SiteAddress = siteAddress
            },
            MetWith = new WrInspectionReportMetWith()
            {
                Name = GetMultilineText(matchesResult, "MetWith"),
                Position = GetMultilineText(matchesResult, "Position"),                
            },
            InspectingOfficer = GetMultilineText(matchesResult, "InspectingOfficer"),
            InspectionDate = new WrInspectionReportInspectionDateTime()
            {
                DateTime = inspectionDateTime,
                RawDate = rawInspectionDate,
                RawTime = rawInspectionTime
            },
            LicenceProvisions = new WrInspectionReportLicenceProvisions()
            {
                SourceOfSupply = GetInOrderStatus(matchesResult, "SourceOfSupply"),
                Purposes = GetInOrderStatus(matchesResult, "Purposes"),
                PointOfAbstraction = GetInOrderStatus(matchesResult, "PointOfAbstraction"),
                SpecialConditions = GetInOrderStatus(matchesResult, "SpecialConditions"),
                ChargingFactors = GetInOrderStatus(matchesResult, "ChargingFactors"),
                Land = GetInOrderStatus(matchesResult, "Land"),
                MeansOfAbstraction = GetInOrderStatus(matchesResult, "MeansOfAbstraction"),
                MeansOfMeasurement = GetInOrderStatus(matchesResult, "MeansOfMeasurement"),
                ProvisionOfInformation = GetInOrderStatus(matchesResult, "ProvisionOfInformation"),
                Quantities = GetInOrderStatus(matchesResult, "Quantities"),
                Records = GetInOrderStatus(matchesResult, "Records"),
                OtherProvisions = GetInOrderStatus(matchesResult, "OtherProvisions"),
                Period = GetInOrderStatus(matchesResult, "Period")
            },
            MeasurementDetails = new WrInspectionReportMeasurementDetails()
            {
                MeterName = GetMultilineText(matchesResult, "MeterName"),
                MeterMake = GetMultilineText(matchesResult, "MeterMake"),
                SerialNumber = GetMultilineText(matchesResult, "SerialNumber"),
                MeterAssetNumber = GetMultilineText(matchesResult, "MeterAssetNumber"),
                Reading = GetMultilineText(matchesResult, "Reading"),
                FlowRate = GetMultilineText(matchesResult, "FlowRate"),
                Verification = GetMultilineText(matchesResult, "Verification"),
                SpotCheckResult = GetMultilineText(matchesResult, "SpotCheckResult"),
                Units = GetMultilineText(matchesResult, "Units"),
                Other = GetMultilineText(matchesResult, "Other"),
                CertificatesOrRecordsAvailableFor = GetMultilineText(matchesResult, "CertificatesOfRecords"),
                DateOfCertificateOrRecord = new WrInspectionReportInspectionDate()
                {
                    Date = dateOfCertificateOrRecord,
                    RawDate = rawDateOfCertificateOrRecord
                },
                Calibration = GetMultilineText(matchesResult, "Calibration"),
                Conformance = GetMultilineText(matchesResult, "Conformance"),
                FlowVerification = GetMultilineText(matchesResult, "FlowVerification"),
                MeterVerification = GetMultilineText(matchesResult, "MeterVerification"),
                Maintenance = new WrInspectionReportMaintenance()
                {
                    Maintenance = maintenanceYesNo,
                    Frequency = GetSingleLineSubFieldText(matchesResult, "MaintenanceLine", "MaintenanceLineFrequency"),
                    ByWhom = GetSingleLineSubFieldText(matchesResult, "MaintenanceLine", "MaintenanceLineByWhom")
                },
                ReadingsTaken = new WrInspectionReportReadingsTaken()
                {
                    ReadingsTaken = readingsTakenYesNo,
                    Frequency = GetSingleLineSubFieldText(matchesResult, "ReadingsTakenLine", "ReadingsTakenLineFrequency"),
                    ByWhom = GetSingleLineSubFieldText(matchesResult, "ReadingsTakenLine", "ReadingsTakenLineByWhom")
                },
                WhereKept = whereKept
            },
            GeneralComments = GetMultilineText(matchesResult, "GeneralComments"),
            Images = images
        };
    }

    private static string? GetSingleLineSubFieldText(MatchesResult matchesResult, string name, string subName)
    {
        return matchesResult.Matches?
            .FirstOrDefault(m => m.MatchedLabelName == name)?
            .SubResults
            .FirstOrDefault(sr => sr.MatchedLabelName == subName)?
            .Text?
            .FirstOrDefault()?
            .Text;
    }
    
    // Some documents render a phone number with a stray space between individual digits
    // (a PDF font/kerning artefact, not a real word-space) - e.g. "0 7 7 9 4 2 1 8 2 97"
    // instead of "07794218297". Collapsing only spaces that sit directly between two
    // digits leaves genuine word-spacing (unlikely here, but harmless) untouched.
    private static string? CollapseSpacedDigits(string? text)
    {
        return string.IsNullOrEmpty(text)
            ? text
            : System.Text.RegularExpressions.Regex.Replace(text, @"(?<=\d) (?=\d)", string.Empty);
    }

    private static string? GetMultilineText(MatchesResult matchesResult, string name)
    {
        var matchedLabel = matchesResult.Matches?
            .FirstOrDefault(m => m.MatchedLabelName == name);

        if (matchedLabel?.Text == null)
        {
            return null;
        }
        
        return string.Join("\n", matchedLabel.Text.Select(t => t.Text)!);
    }
    
    private static string RemoveSpecialCharacters(this string str)
    {
        var sb = new StringBuilder();
        
        foreach (var c in str)
        {
            if (c is >= '0' and <= '9'
                or >= 'A' and <= 'Z'
                or >= 'a' and <= 'z'
                or '.'
                or '/'
                or '-'
                or ':'
                or '+'
                or '&'                
                or '\r'
                or '\n'                
                or ' ')
            {
                sb.Append(c);
            }
        }
        
        return sb.ToString();
    }
    
    private static InOrderStatus GetInOrderStatus(MatchesResult matchesResult,  string name)
    {
        var labelGroupResult = matchesResult.Matches?
            .FirstOrDefault(m => m.MatchedLabelName == name);
        
        if (labelGroupResult == null)
        {
            return InOrderStatus.DidntMatch;
        }

        if (labelGroupResult.Text == null || labelGroupResult.Text.Count == 0)
        {
            return InOrderStatus.Blank;
        }

        var text = string.Join(" ", labelGroupResult.Text.Select(t => t.Text));

        if (string.IsNullOrWhiteSpace(text))
        {
            return InOrderStatus.Blank;
        }

        if (text.Equals("in", StringComparison.InvariantCultureIgnoreCase)
            || text.Equals("✓", StringComparison.InvariantCultureIgnoreCase)
            || text.Equals("y", StringComparison.InvariantCultureIgnoreCase))
        {
            return InOrderStatus.InOrder;
        }

        if (text.Equals("not", StringComparison.InvariantCultureIgnoreCase)
            || text.Equals("X", StringComparison.InvariantCultureIgnoreCase)
            || text.Equals("n", StringComparison.InvariantCultureIgnoreCase))
        {
            return InOrderStatus.NotInOrder;
        }
        
        if (text.Equals("n/a", StringComparison.InvariantCultureIgnoreCase))
        {
            return InOrderStatus.NotApplicable;
        }
        
        return InOrderStatus.Unknown;
    }
}