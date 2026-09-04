using System.Text;
using WALE.ProcessFile.Core.Models;
using WRADI.DocumentType.WrInspectionReport.Enums;
using WRADI.DocumentType.WrInspectionReport.Models;

namespace WRADI.DocumentType.WrInspectionReport.Converters;

public static class WrInspectionReportSchemaConverter
{
    // knownTemplate: pass this when the caller already classified the document (e.g.
    // WrInspectionReportExtractionOrchestrator's own pre-pass) - lets ClassifyTemplate's
    // TemplateMarker* fields be left out of whichever ruleset actually ran without losing
    // Metadata.Template, so GetT1Labels() can genuinely drop the T4/T6/T7/NonStandardNarrative-
    // only alternates a confirmed-T1 document will never need. Omit it (or pass null) to fall
    // back to the old self-contained behaviour - re-deriving Template from matchesResult itself
    // - for any caller that runs a single pass with no separate classification step.
    public static Models.WrInspectionReport ToForm(MatchesResult matchesResult, DmsFileData? dmsFileData, WrTemplateType? knownTemplate = null)
    {
        var rawFormDate = GetMultilineText(matchesResult, "Date");

        DateOnly? formDate = null;
        if (DateOnly.TryParse(NormaliseOrdinalDateSuffixes(rawFormDate), out var tFormDate))
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
                Template = knownTemplate ?? ClassifyTemplate(matchesResult, documentHeader),
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

    // v2 classifier - see WrTemplateType and the TemplateMarker* label groups in
    // WrInspectionReportLabelConfiguration for the literal markers, sourced from the client's
    // TemplateSpec_v5.0.xlsx (T4/T6/T7 sheets) plus the GeneralComments heading catalogue for
    // the T1-vs-NonStandardNarrative check. Checked in order most-specific-first (Impounding and
    // T4/T6/T7 are all positive, distinguishing markers).
    //
    // v1 made T1 the plain default/fallback for anything not T4/T6/T7/Impounding, which measured
    // at 86% of the real corpus against the client's own ~60% expectation - cross-checking
    // against the golden set's documentShape tags showed the gap was largely documents whose
    // GeneralComments section uses one of the known alternate headings, or none at all
    // (desktop-review/headingless narratives, multi-section long-form reports, appendix-
    // terminated reports - see that field's own catalogue). v2 (current) excludes on EITHER
    // signal: a positive alternate-heading match, or the baseline heading being absent
    // entirely. That's a deliberate choice, not the only option - tried excluding on the
    // alternate-heading match alone (v3, not kept): that avoided two false positives found by
    // cross-checking against documentShape (wr51__1041433013__... and
    // wr51__nw0690016005__..., both tagged plain "baseline" but with nothing written in that
    // section at all, baseline heading or otherwise) but cost six true positives elsewhere -
    // wr51__1955120809__... (defer_to_body_report), wr51__1753001s427__... and
    // wr51__sw0430023015__... (narrative_comments_with_appendix), wr51__53114s0109__... and
    // wr51__nw0760002001__... (headingless_narrative), wr51__25092__... (desktop_review) - all
    // genuinely non-standard documents that "missing baseline heading" alone correctly caught
    // and "alternate heading present" alone missed, because they have no heading at all, not a
    // different one. Chose the 2-false-positive version over the 6-false-negative version;
    // resolving the 2 remaining false positives would need a genuinely new signal (e.g.
    // comparing how much text actually sits between the last measurement field and "Form sent
    // to", to distinguish "nothing written" from "narrative written with no heading") - not
    // attempted here, scope it separately if it matters.
    // Separately confirmed NOT a bug: wr51__83617s0016__... is tagged "desktop_review" but
    // correctly stays T1 - that tag describes the inspection method (no site visit), not the
    // comments-section shape, and the document genuinely has the standard heading.
    // Deliberately NOT treated as anomalies (stay T1): all_blank_provisions,
    // remote_meeting, no_telephone_field, compliance_only, temporal_meter_change,
    // struck_through_measurement_section - these are content variation within a genuinely
    // T1-shaped document, not a different comments-section structure. Not yet resolved: a
    // handful of other documentShape tags found while labelling (Y_N_provisions_grid,
    // narrative_provisions, checkbox_pair_grid, NI_provisions) describe how the LicenceProvisions
    // grid itself is marked, not the comments section - no literal text marker distinguishes
    // these the way a heading does, so they're not addressed by this pass.
    // Unknown is reserved for documents where extraction didn't even recognise the WR51 header
    // at all (DocumentHeader empty) - a parse failure, not a shape ambiguity.
    // internal, not private: WrInspectionReportExtractionOrchestrator's classification pre-pass
    // calls this directly against its own (smaller) MatchesResult, before deciding which full
    // ruleset to extract with.
    internal static WrTemplateType ClassifyTemplate(MatchesResult matchesResult, string? documentHeader)
    {
        if (!string.IsNullOrWhiteSpace(GetMultilineText(matchesResult, "TemplateMarkerImpounding")))
        {
            return WrTemplateType.Impounding;
        }

        if (!string.IsNullOrWhiteSpace(GetMultilineText(matchesResult, "TemplateMarkerT4")))
        {
            return WrTemplateType.T4;
        }

        if (!string.IsNullOrWhiteSpace(GetMultilineText(matchesResult, "TemplateMarkerT6")))
        {
            return WrTemplateType.T6;
        }

        if (!string.IsNullOrWhiteSpace(GetMultilineText(matchesResult, "TemplateMarkerT7")))
        {
            return WrTemplateType.T7;
        }

        if (string.IsNullOrWhiteSpace(documentHeader))
        {
            return WrTemplateType.Unknown;
        }

        var hasBaselineComments = !string.IsNullOrWhiteSpace(GetMultilineText(matchesResult, "TemplateMarkerBaselineComments"));
        var hasAlternateComments = !string.IsNullOrWhiteSpace(GetMultilineText(matchesResult, "TemplateMarkerAlternateComments"));

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
        // WrInspectionReportLabelConfiguration.GetInOrderField's Possibilities list for the
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

        if (text.Equals("ni", StringComparison.InvariantCultureIgnoreCase))
        {
            return InOrderStatus.NotInspected;
        }

        return InOrderStatus.Unknown;
    }
}