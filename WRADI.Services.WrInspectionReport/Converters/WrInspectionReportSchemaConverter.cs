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

    /// <param name="knownTemplate">Supplied when the caller already classified the document, so
    /// the TemplateMarker* fields can be left out of whichever ruleset ran without losing
    /// Metadata.Template. Null re-derives it from <paramref name="matchesResult"/>.</param>
    public static Models.WrInspectionReport ToForm(
        MatchesResult matchesResult,
        DmsFileData? dmsFileData,
        WrTemplateType? knownTemplate = null)
    {
        // v3 writes its own field names, so without this every shared field below reads back
        // empty. Off the marker as well as knownTemplate: callers reading a stored result do not
        // always pass one. Mutates matchesResult, additively, so a second call is a no-op.
        if (knownTemplate == WrTemplateType.V3 || IsV3Document(matchesResult))
        {
            WrInspectionV3SharedFieldMapper.Apply(matchesResult);
        }

        var rawFormDate = GetMultilineText(matchesResult, WrInspectionReportFieldNames.Date);

        DateOnly? formDate = null;
        if (DateOnly.TryParse(NormaliseOrdinalDateSuffixes(rawFormDate), out var tFormDate))
        {
            formDate = tFormDate;
        }

        var rawInspectionDate = GetMultilineText(matchesResult, WrInspectionReportFieldNames.InspectionDate);
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

            // A "Mon D" fragment and its year can land on different lines of the raw capture,
            // in either order - neither parses alone, and the line-splitting heuristics below
            // only ever keep one line as the date candidate. Recombine both into one candidate
            // here, before that splitting runs.
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
            
            // Meant for bare 4-digit military time with no separator ("0930" -> "09:30"), but
            // the length-only check also matched any other 4-character raw time - e.g. "9 am"
            // and "11am" (4 chars each) got a colon spliced into the middle of the letters
            // ("9 :am", "11:am"), and an already-colon-separated "8:15" got a second colon
            // inserted ("8::15"). Requiring all 4 characters to be digits restricts this to the
            // bare-digits case it was actually written for.
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
                // e.g. "27.02:26" - one separator typoed as ':' instead of '.'. Normalise
                // toward '.' (not the other way): confirmed via DateTime.TryParse that
                // "27.02.26" parses fine but "27:02:26" (both separators as ':') doesn't -
                // colon-separated numerics read as an invalid time, not a date.
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

        var rawDateOfCertificateOrRecord = GetMultilineText(matchesResult, WrInspectionReportFieldNames.DateOfCertification);
        DateOnly? dateOfCertificateOrRecord = null;
        if (DateOnly.TryParse(rawDateOfCertificateOrRecord, out var tDateOfCertificateOrRecord))
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

    // Splits a verbatim LicenceNumber cell on the delimiters seen across the real corpus
    // ("X and Y", "X & Y", "X, Y, Z" - all three occur) and canonicalises each piece to
    // alphanumeric-only/uppercase. A cleaned piece with no digits at all is dropped - real
    // licence numbers always contain digits, but "and"/"," can occasionally split a site-name
    // pair rather than two licence numbers (e.g. "...004 Brennand and Whitendale", where
    // "Brennand"/"Whitendale" are reservoir names, not licences) - this heuristic keeps the
    // genuine licence-shaped piece and drops the site-name fragment rather than mis-cleaning
    // it into a false match key. Not perfect (a licence number's own site-name suffix stays
    // glued to it if the split lands there instead), but no false-negative case has been seen
    // in the corpus - see the LicenceNumberCleaned property comment for why this exists.
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

    // Some documents render an ordinal date suffix with a stray space before it - e.g.
    // "10 th February 2026" instead of "10th February 2026" - the same PDF font/kerning
    // artefact class as CollapseSpacedDigits above, just for letters instead of digits.
    // Neither form parses via DateOnly.TryParse - .NET doesn't accept an ordinal suffix at
    // all, glued or not (confirmed: "10th February 2026" fails to parse, "10 February 2026"
    // succeeds) - so the whole suffix needs stripping, not just the gap before it. Mirrors
    // the ordinal handling already applied to InspectionDate further up this file.
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

    // MetWith/InspectingOfficer share a row with a sibling field ("Position:"/"Inspection
    // Date:" respectively) on some real documents. The usual fix for this shape
    // (additionalSameLineEndTexts, bounding WalkSameLineColumns to exclude the sibling's own
    // column) doesn't work here - traced via gated instrumentation on wr51__ne0220003014: the
    // upstream row/column-grouping had already merged both fields' text into a SINGLE
    // DocumentLineColumn before label-matching even runs, so there is no second column for
    // WalkSameLineColumns' TextEnd bound to exclude - confirmed empirically (adding the same
    // endText fix that worked for InspectionClass/SiteAddress had zero effect here). A plain
    // string truncation at the known sibling-label marker works regardless of the upstream
    // column structure, since it operates on the final joined text rather than columns.
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

    // Markers come from the client's TemplateSpec_v5.0.xlsx (T4/T6/T7) plus the GeneralComments
    // heading catalogue. Checked most-specific-first.
    //
    // T1 as a plain fallback measured at 86% of the real corpus against the client's own ~60%
    // expectation, so NonStandardNarrative is excluded on EITHER signal: an alternate-heading
    // match, or the baseline heading being absent. Excluding on an alternate-heading match alone
    // avoided 2 false positives but cost 6 true positives on headingless documents.
    //
    // Deliberately still T1: all_blank_provisions, remote_meeting, no_telephone_field,
    // compliance_only, temporal_meter_change, struck_through_measurement_section, desktop_review -
    // content variation, not a different comments-section shape.
    // Not addressed: Y_N_provisions_grid, narrative_provisions, checkbox_pair_grid, NI_provisions
    // describe the LicenceProvisions grid, which no literal text marker distinguishes.
    // Unknown means extraction did not recognise the WR51 header at all - a parse failure.
    internal static WrTemplateType ClassifyTemplate(MatchesResult matchesResult, string? documentHeader)
    {
        // Before the documentHeader guard: a v3 document prints "Form WR51", which
        // RuleDocumentHeader never matches, so it would otherwise fall through to Unknown.
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

    // Same source as GetMultilineText, but as separate lines rather than one joined string -
    // for a multi-meter table (WrInspectionReportLabelConfiguration.MeterTableNextLines) each
    // line is one meter's value for this field, e.g. "Point 1, Yaxley: Honeywell" / "Point 2,
    // Eye: Honeywell" / ... - see BuildMeters. Blank lines dropped (a table with fewer filled
    // rows than the widened window fetched shouldn't produce empty meter entries).
    private static bool IsV3Document(MatchesResult matchesResult) =>
        !string.IsNullOrWhiteSpace(
            GetMultilineText(matchesResult, WrInspectionReportFieldNames.TemplateMarkerV3));

    /// <summary>
    /// For fields zipped together by line index, where a blank line means "this record has no
    /// value" rather than noise. Leading and trailing blanks are still dropped, so a capture that
    /// merely ends with empty lines does not invent records.
    /// </summary>
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

    // Zips the meter fields by line index, which holds as long as a multi-meter table lists its
    // points in the same order down every column - the only layout confirmed so far. Always
    // returns at least one entry. The "Point N, <site>:" prefix some documents embed in each
    // value is left in rather than parsed out, that format not being confirmed universal.
    /// <summary>Test seam for the v3 per-meter overlay.</summary>
    internal static List<WrInspectionReportMeter> BuildMetersForTests(MatchesResult matchesResult) =>
        BuildMeters(matchesResult);

    /// <summary>
    /// Null rather than empty when the actions grid was not read, so a caller can tell "no actions
    /// on this document" from "this template's actions are not extracted".
    /// </summary>
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
    /// Gap-preserving only on v3, whose meter lines come from the table read and use position as
    /// the meter index. v1's come from line rules, where an interior blank is noise: preserving it
    /// there invented phantom empty meter records on the real corpus.
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

        // Paired-checkbox template: box position carries the meaning, not the glyph used to
        // mark it - see GetInOrderField's Possibilities list for the full evidence and reasoning.
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

        // Tick glyph variants: real WR51 PDFs use whichever tick character the originating
        // export toolchain happened to produce, not consistently ✓ - see
        // WrInspectionReportLabelConfiguration's InOrderPossibilities list for the
        // full evidence (corpus-wide symbol frequency behind each of these).
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

        // v3 states the outcome in words where v1 ticked a box. Same question, so the same enum,
        // rather than a parallel status type the CSV and portal would both have to learn.
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