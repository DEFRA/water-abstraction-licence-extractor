using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;
using WRADI.DocumentType.WrInspectionReport.Constants;
using WRADI.DocumentType.WrInspectionReport.Enums;
using WRADI.DocumentType.WrInspectionReport.Models;
using WRADI.DocumentType.WrInspectionReport.Services;

namespace WRADI.DocumentType.WrInspectionReport.Converters;

public static class WrInspectionReportSchemaConverter
{
    public const string AdditionalInformationTemplateKey = "WrInspectionReportTemplate";

    /// Constructed, not a named "en-GB": the container's ambient culture is InvariantCulture, and a
    /// named culture throws under globalization-invariant mode.
    private static readonly DateTimeFormatInfo DayFirst = new() { ShortDatePattern = "dd/MM/yyyy" };

    /// <param name="knownTemplate">Supplied when the caller already classified the document, so
    /// the TemplateMarker* fields can be left out of whichever ruleset ran without losing
    /// Metadata.Template. Null re-derives it from <paramref name="matchesResult"/>.</param>
    public static Models.WrInspectionReport ToForm(
        MatchesResult matchesResult,
        DmsFileData? dmsFileData,
        WrTemplateType? knownTemplate = null)
    {
        // Off the marker as well as knownTemplate: callers reading a stored result do not always
        // pass one.
        if (knownTemplate == WrTemplateType.V3 || IsV3Document(matchesResult))
        {
            WrInspectionV3SharedFieldMapper.Apply(matchesResult);
        }

        var rawFormDate = GetMultilineText(matchesResult, WrInspectionReportFieldNames.Date);

        DateOnly? formDate = null;
        if (DateOnly.TryParse(NormaliseOrdinalDateSuffixes(rawFormDate), DayFirst, out var tFormDate))
        {
            formDate = tFormDate;
        }

        var rawInspectionDate = GetMultilineText(matchesResult, WrInspectionReportFieldNames.InspectionDate);

        var inspectingOfficer = TruncateAtKnownSiblingLabel(
            GetMultilineText(matchesResult, WrInspectionReportFieldNames.InspectingOfficer),
            "Inspection Date:");
        var rawInspectionTime = GetMultilineText(matchesResult, WrInspectionReportFieldNames.Time);

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

            // A "Mon D" fragment and its year can land on different lines in either order, and
            // the splitting below keeps only one line, so recombine before it runs.
            var monthDayMatch = Regex.Match(
                rawInspectionDateTweaked,
                @"\b(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\.?\s+\d{1,2}\b",
                RegexOptions.IgnoreCase);
            var yearMatch = Regex.Match(rawInspectionDateTweaked, @"(?<![0-9])(19|20)\d{2}(?![0-9])");

            if (monthDayMatch.Success && yearMatch.Success)
            {
                potentialDates.Add($"{monthDayMatch.Value} {yearMatch.Value}");
            }

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

                // "Time" usually trails the date ("22/01/2026 Time: 0930") but some layouts put it
                // first ("Time:\n27/01/26"), so what follows "Time" is kept as a fallback.
                var afterTime = parts.Length > 1 ? parts[^1].TrimStart(':').Trim() : string.Empty;

                if (!string.IsNullOrWhiteSpace(afterTime))
                {
                    potentialDates.Add(afterTime);

                    // "Time 12.20\n20/01/2026" - no colon, date on the next line. The blob will not
                    // parse whole, but its last line usually does.
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
            
            // All four characters must be digits: a length-only check spliced a colon into "9 am"
            // and "11am", and gave "8:15" a second one.
            if (rawInspectionTime?.Length == 4
                && rawInspectionTime.All(char.IsDigit)
                && !rawInspectionDateTweaked.Contains(':'))
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
                // "27.02:26" - one separator typoed. Normalised toward '.' because "27:02:26"
                // reads as an invalid time rather than a date.
                potentialDates.Add(rawInspectionDateTweaked.Replace(":", "."));
            }

            var words = rawInspectionDateTweaked.Split(' ');

            if (words.Length == 4)
            {
                potentialDates.Add($"{words[0]} {words[1]} {words[2]}");
                potentialDates.Add($"{words[1]} {words[2]} {words[3]}");
            }
            
            foreach (var potentialDate in potentialDates)
            {
                if (!IsUsableInspectionDate(potentialDate, out var tInspectionDateTime))
                {
                    continue;
                }

                inspectionDateTime = tInspectionDateTime;
                break;
            }

            // Gated on everything above failing, so it cannot change a document that resolves.
            // Two-column layouts leak the officer's name in here, stripped by value as the label
            // itself is gone.
            if (inspectionDateTime == null)
            {
                foreach (var salvaged in SalvageDateCandidates(rawInspectionDate, inspectingOfficer))
                {
                    if (!IsUsableInspectionDate(salvaged, out var tSalvagedDateTime))
                    {
                        continue;
                    }

                    inspectionDateTime = tSalvagedDateTime;
                    break;
                }
            }
        }

        // RawDate is exported and shown as the scraped value, so it has to be clean in its own
        // right. Must stay after the parsing above, which runs against the captured text untouched.
        rawInspectionDate = RemoveSiblingValue(rawInspectionDate, inspectingOfficer);

        var rawDateOfCertificateOrRecord = GetMultilineText(matchesResult, WrInspectionReportFieldNames.DateOfCertification);
        DateOnly? dateOfCertificateOrRecord = null;
        if (DateOnly.TryParse(rawDateOfCertificateOrRecord, DayFirst, out var tDateOfCertificateOrRecord))
        {
            dateOfCertificateOrRecord = tDateOfCertificateOrRecord;
        }

        var nameAndAddress = GetMultilineText(matchesResult, WrInspectionReportFieldNames.NameAndAddress);
        var siteAddress = GetMultilineText(matchesResult, WrInspectionReportFieldNames.SiteAddress);

        if (siteAddress?.Equals("Same as above", StringComparison.InvariantCultureIgnoreCase) == true
            || siteAddress?.Equals("As above", StringComparison.InvariantCultureIgnoreCase) == true)
        {
            siteAddress = nameAndAddress;
        }

        var whereKept = GetMultilineText(matchesResult, WrInspectionReportFieldNames.WhereKept);
        if (string.IsNullOrWhiteSpace(whereKept)) whereKept = null;

        var documentTemplateVerison = GetMultilineText(matchesResult, WrInspectionReportFieldNames.DocumentTemplateVersion);
        var isNewTemplate = documentTemplateVerison == "2026_07_10_v1";

        var documentHeader = GetMultilineText(matchesResult, WrInspectionReportFieldNames.DocumentHeader);
        if (!string.IsNullOrWhiteSpace(documentHeader)) documentHeader = $"Form WR - {documentHeader}";
        
        string? maintenanceYesNo = null;

        if (isNewTemplate)
        {
            maintenanceYesNo = GetSingleLineSubFieldText(matchesResult, WrInspectionReportFieldNames.MaintenanceLine, WrInspectionReportFieldNames.MaintenanceLineMaintenance);
        }
        else
        {
            var maintenanceYes = GetSingleLineSubFieldText(matchesResult, WrInspectionReportFieldNames.MaintenanceLine, WrInspectionReportFieldNames.MaintenanceLineMaintenanceYes);
            var maintenanceNo = GetSingleLineSubFieldText(matchesResult, WrInspectionReportFieldNames.MaintenanceLine, WrInspectionReportFieldNames.MaintenanceLineMaintenanceNo);

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
            readingsTakenYesNo = GetSingleLineSubFieldText(matchesResult, WrInspectionReportFieldNames.ReadingsTakenLine, WrInspectionReportFieldNames.ReadingsTakenLineReadingsTaken);
        }
        else
        {
            var readingsTakenYes = GetSingleLineSubFieldText(matchesResult, WrInspectionReportFieldNames.ReadingsTakenLine, WrInspectionReportFieldNames.ReadingsTakenLineReadingsTakenYes);
            var readingsTakenNo = GetSingleLineSubFieldText(matchesResult, WrInspectionReportFieldNames.ReadingsTakenLine, WrInspectionReportFieldNames.ReadingsTakenLineReadingsTakenNo);

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

        return new Models.WrInspectionReport
        {
            Metadata = new WrInspectionReportMetadata
            {
                Template = knownTemplate ?? ClassifyTemplate(matchesResult, documentHeader),
                DocumentTemplateVerison = documentTemplateVerison,
                DocumentHeader = documentHeader,
                Filename = matchesResult.Filename,
                FileId = dmsFileData?.FileId,
                IsScan = matchesResult.ScannedFile,
                FormSentTo = GetMultilineText(matchesResult, WrInspectionReportFieldNames.FormSentTo),
                Date = new WrInspectionReportInspectionDate()
                {
                    RawDate = rawFormDate,
                    Date = formDate   
                }
            },
            LicenceNumber = GetMultilineText(matchesResult, WrInspectionReportFieldNames.LicenceNumber),
            LicenceNumberCleaned = CleanLicenceNumbers(GetMultilineText(matchesResult, WrInspectionReportFieldNames.LicenceNumber)),
            InspectionClass = GetMultilineText(matchesResult, WrInspectionReportFieldNames.InspectionClass),
            Address = new WrInspectionReportAddress()
            {
                NameAndAddress = nameAndAddress,
                TelephoneNumber = CollapseSpacedDigits(GetMultilineText(matchesResult, WrInspectionReportFieldNames.TelephoneNumber)),
                SiteAddress = siteAddress,
                Email = GetMultilineText(matchesResult, WrInspectionReportFieldNames.V3SiteContactEmail)
            },
            MetWith = new WrInspectionReportMetWith()
            {
                Name = TruncateAtKnownSiblingLabel(GetMultilineText(matchesResult, WrInspectionReportFieldNames.MetWith), "Position:"),
                Position = GetMultilineText(matchesResult, WrInspectionReportFieldNames.Position),
            },
            InspectingOfficer = TruncateAtKnownSiblingLabel(GetMultilineText(matchesResult, WrInspectionReportFieldNames.InspectingOfficer), "Inspection Date:"),
            InspectionDate = new WrInspectionReportInspectionDateTime()
            {
                DateTime = inspectionDateTime,
                RawDate = rawInspectionDate,
                RawTime = rawInspectionTime,
                RawEndTime = GetMultilineText(matchesResult, WrInspectionReportFieldNames.V3InspectionEndTime)
            },
            LicenceProvisions = new WrInspectionReportLicenceProvisions
            {
                SourceOfSupply = GetInOrderStatus(matchesResult, WrInspectionReportFieldNames.SourceOfSupply),
                Purposes = GetInOrderStatus(matchesResult, WrInspectionReportFieldNames.Purposes),
                PointOfAbstraction = GetInOrderStatus(matchesResult, WrInspectionReportFieldNames.PointOfAbstraction),
                SpecialConditions = GetInOrderStatus(matchesResult, WrInspectionReportFieldNames.SpecialConditions),
                ChargingFactors = GetInOrderStatus(matchesResult, WrInspectionReportFieldNames.ChargingFactors),
                Land = GetInOrderStatus(matchesResult, WrInspectionReportFieldNames.Land),
                MeansOfAbstraction = GetInOrderStatus(matchesResult, WrInspectionReportFieldNames.MeansOfAbstraction),
                MeansOfMeasurement = GetInOrderStatus(matchesResult, WrInspectionReportFieldNames.MeansOfMeasurement),
                ProvisionOfInformation = GetInOrderStatus(matchesResult, WrInspectionReportFieldNames.ProvisionOfInformation),
                Quantities = GetInOrderStatus(matchesResult, WrInspectionReportFieldNames.Quantities),
                Records = GetInOrderStatus(matchesResult, WrInspectionReportFieldNames.Records),
                OtherProvisions = GetInOrderStatus(matchesResult, WrInspectionReportFieldNames.OtherProvisions),
                Period = GetInOrderStatus(matchesResult, WrInspectionReportFieldNames.Period),
                HandsOffFlow = GetInOrderStatus(matchesResult, WrInspectionReportFieldNames.V3ConditionHandsOffFlow),
                CompensationOrAugmentation = GetInOrderStatus(
                    matchesResult, WrInspectionReportFieldNames.V3ConditionCompensationOrAugmentation),
                FishAndEelScreening = GetInOrderStatus(
                    matchesResult, WrInspectionReportFieldNames.V3ConditionFishAndEelScreening)
            },
            MeasurementDetails = new WrInspectionReportMeasurementDetails
            {
                Meters = BuildMeters(matchesResult),
                Verification = GetMultilineText(matchesResult, WrInspectionReportFieldNames.Verification),
                SpotCheckResult = GetMultilineText(matchesResult, WrInspectionReportFieldNames.SpotCheckResult),
                Other = GetMultilineText(matchesResult, WrInspectionReportFieldNames.Other),
                CertificatesOrRecordsAvailableFor = GetMultilineText(matchesResult, WrInspectionReportFieldNames.CertificatesOfRecords),
                DateOfCertificateOrRecord = new WrInspectionReportInspectionDate()
                {
                    Date = dateOfCertificateOrRecord,
                    RawDate = rawDateOfCertificateOrRecord
                },
                Calibration = GetMultilineText(matchesResult, WrInspectionReportFieldNames.Calibration),
                Conformance = GetMultilineText(matchesResult, WrInspectionReportFieldNames.Conformance),
                FlowVerification = GetMultilineText(matchesResult, WrInspectionReportFieldNames.FlowVerification),
                MeterVerification = GetMultilineText(matchesResult, WrInspectionReportFieldNames.MeterVerification),
                Maintenance = new WrInspectionReportMaintenance()
                {
                    Maintenance = maintenanceYesNo,
                    Frequency = GetSingleLineSubFieldText(matchesResult, WrInspectionReportFieldNames.MaintenanceLine, WrInspectionReportFieldNames.MaintenanceLineFrequency),
                    ByWhom = GetSingleLineSubFieldText(matchesResult, WrInspectionReportFieldNames.MaintenanceLine, WrInspectionReportFieldNames.MaintenanceLineByWhom)
                },
                ReadingsTaken = new WrInspectionReportReadingsTaken()
                {
                    ReadingsTaken = readingsTakenYesNo,
                    Frequency = GetSingleLineSubFieldText(matchesResult, WrInspectionReportFieldNames.ReadingsTakenLine, WrInspectionReportFieldNames.ReadingsTakenLineFrequency),
                    ByWhom = GetSingleLineSubFieldText(matchesResult, WrInspectionReportFieldNames.ReadingsTakenLine, WrInspectionReportFieldNames.ReadingsTakenLineByWhom)
                },
                WhereKept = whereKept
            },
            GeneralComments = GetMultilineText(matchesResult, WrInspectionReportFieldNames.GeneralComments),
            Images = images,
            LicenceCriticality = GetMultilineText(matchesResult, WrInspectionReportFieldNames.V3LicenceCriticality),
            EnforcementResponse = WrInspectionV3SharedFieldMapper.StripPageFurniture(
                GetMultilineText(matchesResult, WrInspectionReportFieldNames.V3EnforcementResponse)),
            LicenceConditionsRaw = WrInspectionV3SharedFieldMapper.StripPageFurniture(
                GetMultilineText(matchesResult, WrInspectionReportFieldNames.V3LicenceConditionsBlock)),
            NonCompliancesRaw = WrInspectionV3SharedFieldMapper.StripPageFurniture(
                GetMultilineText(matchesResult, WrInspectionReportFieldNames.V3NonCompliancesBlock)),
            ActionsRaw = WrInspectionV3SharedFieldMapper.StripPageFurniture(
                GetMultilineText(matchesResult, WrInspectionReportFieldNames.V3ActionsBlock)),
            Actions = BuildActions(matchesResult)
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
    
    // A kerning artefact spaces out the digits ("0 7 7 9 4 2 1 8 2 97"). Only spaces sitting
    // directly between two digits are collapsed.
    private static string? CollapseSpacedDigits(string? text)
    {
        return string.IsNullOrEmpty(text)
            ? text
            : System.Text.RegularExpressions.Regex.Replace(text, @"(?<=\d) (?=\d)", string.Empty);
    }

    // Splits on "and", "&" and "," - all three occur. A cleaned piece with no digits is dropped,
    // because those delimiters also join site names ("...004 Brennand and Whitendale", two
    // reservoirs), and a site-name fragment would otherwise become a false match key.
    internal static List<string> CleanLicenceNumbers(string? rawLicenceNumber)
    {
        if (string.IsNullOrWhiteSpace(rawLicenceNumber))
        {
            return [];
        }

        var pieces = System.Text.RegularExpressions.Regex.Split(
            rawLicenceNumber,
            @"\s*(?:,|&|\band\b)\s*",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return pieces
            .Select(piece => System.Text.RegularExpressions.Regex.Replace(piece, @"[^a-zA-Z0-9]", string.Empty).ToUpperInvariant())
            .Where(cleaned => cleaned.Any(char.IsDigit))
            .Distinct()
            .ToList();
    }

    // The same kerning artefact as CollapseSpacedDigits, for letters: "10 th February 2026".
    // The whole suffix is stripped, not just the gap - DateOnly.TryParse rejects an ordinal
    // either way, glued or spaced.
    private static string? NormaliseOrdinalDateSuffixes(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        return System.Text.RegularExpressions.Regex
            .Replace(text, @"(?<=\d)\s*(?:th|st|nd|rd)\b", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            .Trim();
    }

    /// <summary>
    /// Must parse, must carry its own year (a bare "24 March" would otherwise become the current
    /// year), and must not be today (TryParse falls back to today for a time-only string).
    /// </summary>
    private static bool IsUsableInspectionDate(string potentialDate, out DateTime parsed)
    {
        if (!DateTime.TryParse(potentialDate, DayFirst, DateTimeStyles.None, out parsed))
        {
            return false;
        }

        var year = parsed.Year.ToString();
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

        return containsYear && parsed.Date != DateTime.Today;
    }

    /// Most-specific first: a full numeric date beats a day/month needing a year from elsewhere.
    private static IEnumerable<string> SalvageDateCandidates(string rawInspectionDate, string? inspectingOfficer)
    {
        var text = RemoveSpecialCharacters(rawInspectionDate) ?? rawInspectionDate;

        if (!string.IsNullOrWhiteSpace(inspectingOfficer) && text.Contains(inspectingOfficer))
        {
            text = text.Replace(inspectingOfficer, " ");
        }

        text = text.Replace('\n', ' ').Replace("  ", " ").Trim();

        foreach (Match m in Regex.Matches(text, @"\b\d{1,2}[/.\-:]\d{1,2}[/.\-:]\d{2,4}\b"))
        {
            yield return m.Value.Replace(':', '.');
        }

        // The year often wraps onto another line, so pair a bare day/month with any year found.
        var dayMonth = Regex.Match(
            text,
            @"\b\d{1,2}\s*(?:st|nd|rd|th)?\s+(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\b",
            RegexOptions.IgnoreCase);

        if (!dayMonth.Success)
        {
            yield break;
        }

        var cleanedDayMonth = Regex.Replace(dayMonth.Value, @"(?<=\d)\s*(?:st|nd|rd|th)", string.Empty, RegexOptions.IgnoreCase);
        var year = Regex.Match(text, @"(?<![0-9])(19|20)\d{2}(?![0-9])");

        if (year.Success)
        {
            yield return $"{cleanedDayMonth} {year.Value}";
        }
    }

    // For when the sibling's label is absent and only its value is there. Skipped when that value
    // is date- or time-like rather than a name ("2019", "Time: 10:50" both occur as
    // InspectingOfficer): removing one would delete the date's own year from the text.
    private static string? RemoveSiblingValue(string? text, string? siblingValue)
    {
        if (string.IsNullOrWhiteSpace(text)
            || string.IsNullOrWhiteSpace(siblingValue)
            || !siblingValue.Any(char.IsLetter)
            || siblingValue.TrimStart().StartsWith("Time", StringComparison.OrdinalIgnoreCase))
        {
            return text;
        }

        var index = text.IndexOf(siblingValue, StringComparison.OrdinalIgnoreCase);

        if (index < 0)
        {
            return text;
        }

        var cleaned = string.Concat(text.AsSpan(0, index), " ", text.AsSpan(index + siblingValue.Length))
            .Replace("  ", " ")
            .Trim();

        // No date left to keep, and the original text is more use to a verifier than an empty cell.
        return string.IsNullOrWhiteSpace(cleaned) ? text : cleaned;
    }

    // MetWith and InspectingOfficer share a row with a sibling label ("Position:", "Inspection
    // Date:"). additionalSameLineEndTexts cannot fix it: traced on wr51__ne0220003014, the upstream
    // row grouping has already merged both fields into a single DocumentLineColumn before label
    // matching runs, so there is no second column to bound. Truncating the joined text works
    // whatever the column structure.
    private static string? TruncateAtKnownSiblingLabel(string? text, string siblingLabel)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var index = text.IndexOf(siblingLabel, StringComparison.OrdinalIgnoreCase);

        return index < 0
            ? text
            : text[..index].TrimEnd().TrimEnd(',');
    }

    // Checked most-specific-first. NonStandardNarrative is excluded on either signal - an
    // alternate heading matching, or the baseline heading being absent - because the heading match
    // alone avoided 2 false positives but cost 6 true positives on headingless documents. Plain
    // T1 had been taking 86% against the client's own ~60% expectation.
    //
    // Y_N_provisions_grid, narrative_provisions, checkbox_pair_grid and NI_provisions are not
    // addressed: they describe the provisions grid, which no literal text marker distinguishes.
    // Unknown means the WR51 header was not recognised at all.
    internal static WrTemplateType ClassifyTemplate(MatchesResult matchesResult, string? documentHeader)
    {
        // Before the documentHeader guard: v3 prints "Form WR51", which RuleDocumentHeader never
        // matches, so it would fall through to Unknown.
        if (!string.IsNullOrWhiteSpace(GetMultilineText(matchesResult, WrInspectionReportFieldNames.TemplateMarkerV3)))
        {
            return WrTemplateType.V3;
        }

        if (!string.IsNullOrWhiteSpace(GetMultilineText(matchesResult, WrInspectionReportFieldNames.TemplateMarkerImpounding)))
        {
            return WrTemplateType.Impounding;
        }

        if (!string.IsNullOrWhiteSpace(GetMultilineText(matchesResult, WrInspectionReportFieldNames.TemplateMarkerT4)))
        {
            return WrTemplateType.T4;
        }

        if (!string.IsNullOrWhiteSpace(GetMultilineText(matchesResult, WrInspectionReportFieldNames.TemplateMarkerT6)))
        {
            return WrTemplateType.T6;
        }

        if (!string.IsNullOrWhiteSpace(GetMultilineText(matchesResult, WrInspectionReportFieldNames.TemplateMarkerT7)))
        {
            return WrTemplateType.T7;
        }

        if (string.IsNullOrWhiteSpace(documentHeader))
        {
            return WrTemplateType.Unknown;
        }

        var hasBaselineComments = !string.IsNullOrWhiteSpace(GetMultilineText(matchesResult, WrInspectionReportFieldNames.TemplateMarkerBaselineComments));
        var hasAlternateComments = !string.IsNullOrWhiteSpace(GetMultilineText(matchesResult, WrInspectionReportFieldNames.TemplateMarkerAlternateComments));

        return hasBaselineComments && !hasAlternateComments
            ? WrTemplateType.T1
            : WrTemplateType.NonStandardNarrative;
    }

    internal static string? GetMultilineText(MatchesResult matchesResult, string name)
    {
        var matchedLabel = matchesResult.Matches?
            .FirstOrDefault(m => m.MatchedLabelName == name);

        if (matchedLabel?.Text == null)
        {
            return null;
        }
        
        return string.Join("\n", matchedLabel.Text.Select(t => t.Text)!);
    }

    private static bool IsV3Document(MatchesResult matchesResult) =>
        !string.IsNullOrWhiteSpace(
            GetMultilineText(matchesResult, WrInspectionReportFieldNames.TemplateMarkerV3));

    /// A blank line means "this record has no value" rather than noise. Leading and trailing
    /// blanks are still dropped, so a capture merely ending in empty lines invents no records.
    private static List<string> GetMultilineTextLinesPreservingGaps(
        MatchesResult matchesResult,
        string name)
    {
        var matchedLabel = matchesResult.Matches?
            .FirstOrDefault(m => m.MatchedLabelName == name);

        if (matchedLabel?.Text == null)
        {
            return [];
        }

        var lines = matchedLabel.Text
            .Select(t => t.Text?.Trim() ?? string.Empty)
            .ToList();

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
        {
            lines.RemoveAt(0);
        }

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }

    // One line per meter for a multi-meter table, e.g. "Point 1, Yaxley: Honeywell". Blank lines
    // are dropped, so a table with fewer filled rows than the window fetched invents no meters.
    private static List<string> GetMultilineTextLines(MatchesResult matchesResult, string name)
    {
        var matchedLabel = matchesResult.Matches?
            .FirstOrDefault(m => m.MatchedLabelName == name);

        if (matchedLabel?.Text == null)
        {
            return [];
        }

        return matchedLabel.Text
            .Select(t => t.Text?.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t!)
            .ToList();
    }

    internal static List<WrInspectionReportMeter> BuildMetersForTests(MatchesResult matchesResult) =>
        BuildMeters(matchesResult);

    /// Null rather than empty when the grid was not read, so a caller can tell "no actions on this
    /// document" from "this template's actions are not extracted".
    private static List<WrInspectionReportAction>? BuildActions(MatchesResult matchesResult)
    {
        var reference = GetMultilineTextLinesPreservingGaps(
            matchesResult, WrInspectionReportFieldNames.ActionReference);
        var type = GetMultilineTextLinesPreservingGaps(
            matchesResult, WrInspectionReportFieldNames.ActionType);
        var action = GetMultilineTextLinesPreservingGaps(
            matchesResult, WrInspectionReportFieldNames.ActionDescription);
        var completeBy = GetMultilineTextLinesPreservingGaps(
            matchesResult, WrInspectionReportFieldNames.ActionCompleteBy);

        var actionCount = new[] { reference.Count, type.Count, action.Count, completeBy.Count }.Max();

        if (actionCount == 0)
        {
            return null;
        }

        return Enumerable.Range(0, actionCount)
            .Select(i => new WrInspectionReportAction
            {
                Reference = Blank(reference.ElementAtOrDefault(i)),
                Type = Blank(type.ElementAtOrDefault(i)),
                Action = Blank(action.ElementAtOrDefault(i)),
                CompleteBy = Blank(completeBy.ElementAtOrDefault(i))
            })
            .ToList();
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>
    /// Zipped by line index, which holds only while a multi-meter table lists its points in the
    /// same order down every column - the one layout confirmed. Always returns at least one entry.
    ///
    /// Gap-preserving on v3 only, where position is the meter index. v1's lines come from line
    /// rules, where an interior blank is noise: preserving it there invented phantom meter records.
    /// </summary>
    private static List<WrInspectionReportMeter> BuildMeters(MatchesResult matchesResult)
    {
        Func<MatchesResult, string, List<string>> read = IsV3Document(matchesResult)
            ? GetMultilineTextLinesPreservingGaps
            : GetMultilineTextLines;

        var meterName = read(matchesResult, WrInspectionReportFieldNames.MeterName);
        var meterMake = read(matchesResult, WrInspectionReportFieldNames.MeterMake);
        var serialNumber = read(matchesResult, WrInspectionReportFieldNames.SerialNumber);
        var meterAssetNumber = read(matchesResult, WrInspectionReportFieldNames.MeterAssetNumber);
        var reading = read(matchesResult, WrInspectionReportFieldNames.Reading);
        var flowRate = read(matchesResult, WrInspectionReportFieldNames.FlowRate);
        var units = read(matchesResult, WrInspectionReportFieldNames.Units);
        var verificationCertificate = read(
            matchesResult, WrInspectionReportFieldNames.VerificationCertificate);
        var maintenanceRecords = read(
            matchesResult, WrInspectionReportFieldNames.MaintenanceRecords);

        var meterCount = new[]
        {
            meterName.Count, meterMake.Count, serialNumber.Count, meterAssetNumber.Count,
            reading.Count, flowRate.Count, units.Count, verificationCertificate.Count,
            maintenanceRecords.Count
        }.Max();

        meterCount = Math.Max(meterCount, 1);

        return Enumerable.Range(0, meterCount)
            .Select(i => new WrInspectionReportMeter
            {
                MeterName = meterName.ElementAtOrDefault(i),
                MeterMake = meterMake.ElementAtOrDefault(i),
                SerialNumber = serialNumber.ElementAtOrDefault(i),
                MeterAssetNumber = meterAssetNumber.ElementAtOrDefault(i),
                Reading = reading.ElementAtOrDefault(i),
                FlowRate = flowRate.ElementAtOrDefault(i),
                Units = units.ElementAtOrDefault(i),
                VerificationCertificate = verificationCertificate.ElementAtOrDefault(i),
                MaintenanceRecords = maintenanceRecords.ElementAtOrDefault(i)
            })
            .ToList();
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

        // Paired checkboxes: the box position carries the meaning, not the glyph marking it.
        if (text.Equals("☑ ☐", StringComparison.InvariantCultureIgnoreCase)
            || text.Equals("☒ ☐", StringComparison.InvariantCultureIgnoreCase))
        {
            return InOrderStatus.InOrder;
        }

        if (text.Equals("☐ ☑", StringComparison.InvariantCultureIgnoreCase)
            || text.Equals("☐ ☒", StringComparison.InvariantCultureIgnoreCase))
        {
            return InOrderStatus.NotInOrder;
        }

        if (text.Equals("☐ ☐", StringComparison.InvariantCultureIgnoreCase))
        {
            return InOrderStatus.Blank;
        }

        // WR51 PDFs carry whichever tick character the exporting toolchain produced, not
        // consistently ✓. InOrderPossibilities holds the symbol frequency behind each of these.
        if (text.Equals("in", StringComparison.InvariantCultureIgnoreCase)
            || text.Equals("✓", StringComparison.InvariantCultureIgnoreCase)
            || text.Equals("✔", StringComparison.InvariantCultureIgnoreCase)
            || text.Equals("√", StringComparison.InvariantCultureIgnoreCase)
            || text.Equals("🗸", StringComparison.InvariantCultureIgnoreCase)
            || text.Equals("", StringComparison.InvariantCultureIgnoreCase)
            || text.Equals("", StringComparison.InvariantCultureIgnoreCase)
            || text.Equals("", StringComparison.InvariantCultureIgnoreCase)
            || text.Equals("", StringComparison.InvariantCultureIgnoreCase)
            || text.Equals("y", StringComparison.InvariantCultureIgnoreCase))
        {
            return InOrderStatus.InOrder;
        }

        if (text.Equals("not", StringComparison.InvariantCultureIgnoreCase)
            || text.Equals("X", StringComparison.InvariantCultureIgnoreCase)
            || text.Equals("☒", StringComparison.InvariantCultureIgnoreCase)
            || text.Equals("×", StringComparison.InvariantCultureIgnoreCase)
            || text.Equals("n", StringComparison.InvariantCultureIgnoreCase))
        {
            return InOrderStatus.NotInOrder;
        }
        
        if (text.Equals("n/a", StringComparison.InvariantCultureIgnoreCase))
        {
            return InOrderStatus.NotApplicable;
        }

        // v3 states the outcome in words where v1 ticked a box - the same question, so the same
        // enum rather than a parallel status the CSV and portal would both have to learn.
        if (text.Equals("compliant", StringComparison.InvariantCultureIgnoreCase))
        {
            return InOrderStatus.InOrder;
        }

        // Observed on the samples: means of measurement on one document, records on another.
        if (text.Equals("non-compliant", StringComparison.InvariantCultureIgnoreCase)
            || text.Equals("not compliant", StringComparison.InvariantCultureIgnoreCase))
        {
            return InOrderStatus.NotInOrder;
        }

        if (text.Equals("not applicable", StringComparison.InvariantCultureIgnoreCase))
        {
            return InOrderStatus.NotApplicable;
        }

        if (text.Equals("not assessed", StringComparison.InvariantCultureIgnoreCase)
            || text.Equals("not inspected", StringComparison.InvariantCultureIgnoreCase))
        {
            return InOrderStatus.NotInspected;
        }

        if (text.Equals("ni", StringComparison.InvariantCultureIgnoreCase))
        {
            return InOrderStatus.NotInspected;
        }

        return InOrderStatus.Unknown;
    }
}