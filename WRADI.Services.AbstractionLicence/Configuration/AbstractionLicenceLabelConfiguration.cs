using System.Text.RegularExpressions;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Formats;
using WRADI.DocumentType.AbstractionLicence.Enums;
using WRADI.DocumentType.AbstractionLicence.Formats;

namespace WRADI.DocumentType.AbstractionLicence.Configuration;

public static partial class AbstractionLicenceLabelConfiguration
{
    public static List<(string LabelGroupName, List<LabelToMatch> Labels)> GetLabels()
    {
        return
        [
            ("Company", GetCompanyNameLabels()),
            ("LicenceNumber", SharedLabels.GetLicenceNumberLabels()),
            ("MeansOfAbstraction", GetMeansOfAbstractionLabels()),
            ("PeriodsOfAbstraction", GetPeriodsOfAbstractionLabels()),
            (DocumentSectionNames.AbstractionLimits, GetAbstractionLimitsLabels()),
            (DocumentSectionNames.Purposes, GetPurposeLabels()),
            (DocumentSectionNames.Points, GetPointsLabels()),
            (DocumentSectionNames.SourceOfSupply, GetSourceOfSupplyLabels()),
            ("DateOfIssue", SharedLabels.GetDateOfIssueLabels()),
            ("DateOfOriginalIssue", GetDateOfOriginalIssueLabels()),
            ("DateEffective", GetDateEffectiveLabels()),
            ("DateOfExpiry", GetDateOfExpiryLabels()),
            ("Issuer", GetIssuerLabels()),
            (DocumentSectionNames.Records, GetRecords()),
            (DocumentSectionNames.FurtherConditions, GetFurtherConditions()),
            (DocumentSectionNames.Additional, GetAdditional()),
            (DocumentSectionNames.ReasonsForConditions, GetReasonsForConditions()),
            (DocumentSectionNames.OtherConditions, GetOtherConditions()),            
            (DocumentSectionNames.LicenceHistory, GetLicenceHistory()),
            (DocumentSectionNames.FurtherProvisions, GetFurtherProvisions()),
            ("LinkedLicenceNumber", GetGeneralLinkedLicenceNumbers()),
            
            ("ScheduleOfConditionsA", GetScheduleOfConditionsA()),
            ("ScheduleOfConditionsB", GetScheduleOfConditionsB())
        ];
    }
    
    private static List<LabelToMatch> GetRecords()
    {
        return
        [
            new LabelToMatch
            {
                Name = "RecordsAll",
                TextStart =
                [
                    new("8. Records[END_OF_LINE]"),
                    new("9. Records[END_OF_LINE]"),
                    new("PARTICULARS OF LICENCE[END_OF_LINE]"), // TODO - NOT NECESSARILY HERE, BUT WANTED TO FETCH IT
                    new("Records[END_OF_LINE]") { LineMustStartWith = true }
                ],
                TextEnd =
                [
                    new("9. Further conditions"),
                    new("9. Further provisions"),
                    new("10. Further conditions"),
                    new("10 Further conditions") { LineMustStartWith = true },  
                    new("10. Further provisions"),
                    new("10 Further provisions") { LineMustStartWith = true },                 
                    new("Further Conditions[END_OF_LINE]") { ColumnMustStartWith = true },
                    new("10. FURTHER PROVISIONS[END_OF_LINE]") { LineMustStartWith = true },
                    new("FURTHER PROVISIONS[END_OF_LINE]") { LineMustStartWith = true },
                    new("Additional Information[END_OF_LINE]") { LineMustStartWith = true },
                    new("Would you like to find out") { LineMustStartWith = true },
                    new("[END_OF_BLOCK]")
                ],
                Remove =
                [
                    PageNumberPattern,
                    LicenceNumberInHeaderPattern
                ],
                Position = LabelPosition.TextToFindIsBetweenLabels,
                MultipleServiceMatchBehaviour =
                    MultipleServiceMatchBehaviour.UseMostSubResultsUseLastServiceResultIfEqual,
                IncludeWholeLine = true,
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 100,
                SubLabels = 
                [
                    new()
                    {
                      Name = "RecordsPoint",
                        TextStart = [
                            new("8.1"),
                            new("8.2"),
                            new("8.3"),
                            new("8.4"),
                            new("8.5"),
                            new("8.6"),
                            new("8.7"),
                            new("8.8"),
                            new("8.9"),
                            new("8.10"),
                            new("[START_OF_BLOCK]")
                        ],
                        TextEnd = [
                            new("8.2"),
                            new("8.3"),
                            new("8.4"),
                            new("8.5"),
                            new("8.6"),
                            new("8.7"),
                            new("8.8"),
                            new("8.9"),
                            new("8.10"),
                            new("8.11"),
                            new("[END_OF_BLOCK]")
                        ],
                        Position = LabelPosition.TextToFindIsBetweenLabels,
                        Format = "Text",
                        PreviousLinesToFetch = 0,
                        NextLinesToFetch = 30,
                        IncludeStartLabelText = true,
                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithMultipleValuesPerLabel,
                        SubLabels = [
                            GetLinkedLicenceNumber("RecordsLinkedLicenceNumber"),
                            ..GetLimitLineSubLabels(8)
                        ]
                    }
                ]
            }
        ];
    }

    private static List<LabelToMatch> GetReasonsForConditions()
    {
        return
        [
            new LabelToMatch
            {
                Name = "ReasonsForConditionsAll",
                TextStart =
                [
                    new("REASONS FOR CONDITIONS[END_OF_LINE]") { LineMustStartWith = true }
                ],
                TextEnd =
                [
                    new("IMPORTANT NOTES[END_OF_LINE]") { LineMustStartWith = true },
                    new("History of licence[END_OF_LINE]") { LineMustStartWith = true },
                    new("Licence History[END_OF_LINE]") { LineMustStartWith = true },
                    new("Summary of Change[END_OF_LINE]") { ColumnMustStartWith = true },
                    new("SCHEDULE OF LICENCES[END_OF_LINE]") { LineMustStartWith = true },
                    new("Would you like to find out") { LineMustStartWith = true },
                    new("Map accompanying licence number"),
                    new("[END_OF_BLOCK]")
                ],
                Remove =
                [
                    PageNumberPattern,
                    LicenceNumberInHeaderPattern
                ],
                Position = LabelPosition.TextToFindIsBetweenLabels,
                MultipleServiceMatchBehaviour =
                    MultipleServiceMatchBehaviour.UseMostSubResultsUseLastServiceResultIfEqual,
                IncludeWholeLine = true,
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 100,
                SubLabels = 
                [
                    new()
                    {
                        Name = "ReasonsForConditionsPoint",
                        TextStart = [
                            new("Abstraction period details[END_OF_LINE]") { LineMustStartWith = true },
                            new("Metering[END_OF_LINE]") { LineMustStartWith = true },
                            new("Abstraction Reform[END_OF_LINE]") { LineMustStartWith = true },
                            new("REASONS FOR CONDITIONS[END_OF_LINE]") { LineMustStartWith = true },
                            new("Water efficiency note[END_OF_LINE]") { LineMustStartWith = true },
                            new("[START_OF_BLOCK]")
                        ],
                        TextEnd = [
                            new("Metering[END_OF_LINE]") { LineMustStartWith = true },
                            new("Abstraction Reform[END_OF_LINE]") { LineMustStartWith = true },
                            new("REASONS FOR CONDITIONS[END_OF_LINE]") { LineMustStartWith = true },
                            new("Water efficiency note[END_OF_LINE]") { LineMustStartWith = true },
                            new("Would you like") { LineMustStartWith = true },
                            new("[END_OF_BLOCK]")
                        ],
                        Position = LabelPosition.TextToFindIsBetweenLabels,
                        Format = "Text",
                        PreviousLinesToFetch = 0,
                        NextLinesToFetch = 30,
                        IncludeStartLabelText = true,
                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithMultipleValuesPerLabel,
                        SubLabels = [
                            GetLinkedLicenceNumber("ReasonsForConditionsLinkedLicenceNumber"),
                            ..GetLimitLineSubLabels(null)
                        ]
                    }
                ]
            }
        ];
    }
    
    private static List<LabelToMatch> GetOtherConditions()
    {
        return
        [
            new LabelToMatch
            {
                Name = "OtherConditionsAll",
                TextStart =
                [
                    new("OTHER CONDITIONS SUBJECT TO WHICH ABSTRACTION IS AUTHORISED[END_OF_LINE]") { LineMustStartWith = true },
                    new("OTHER CONDITIONS SUBJECT TO WHICH ABSTRACTION IS AUTHORISED[END_OF_LINE]") { ColumnMustStartWith = true },
                    new("7. OTHER CONDITIONS SUBJECT TO WHICH ABSTRACTION IS AUTHORISED[END_OF_LINE]") { LineMustStartWith = true }
                ],
                TextEnd =
                [
                    new("REASONS FOR CONDITIONS[END_OF_LINE]") { LineMustStartWith = true },
                    new("[END_OF_BLOCK]")
                ],
                Remove =
                [
                    PageNumberPattern,
                    LicenceNumberInHeaderPattern
                ],
                Position = LabelPosition.TextToFindIsBetweenLabels,
                MultipleServiceMatchBehaviour =
                    MultipleServiceMatchBehaviour.UseMostSubResultsUseLastServiceResultIfEqual,
                IncludeWholeLine = true,
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 100,
                SubLabels = 
                [
                    new()
                    {
                        Name = "OtherConditionsPoint",
                        TextStart = [
                            new("1.") { LineMustStartWith = true },
                            new("2.") { LineMustStartWith = true },
                            new("3.") { LineMustStartWith = true },
                            new("4.") { LineMustStartWith = true },
                            new("5.") { LineMustStartWith = true },
                            new("6.") { LineMustStartWith = true },
                            new("7.") { LineMustStartWith = true },
                            new("8.") { LineMustStartWith = true }
                        ],
                        TextEnd = [
                            new("2.") { LineMustStartWith = true },
                            new("3.") { LineMustStartWith = true },
                            new("4.") { LineMustStartWith = true },
                            new("5.") { LineMustStartWith = true },
                            new("6.") { LineMustStartWith = true },
                            new("7.") { LineMustStartWith = true },
                            new("8.") { LineMustStartWith = true },
                            new("[END_OF_BLOCK]")
                        ],
                        Position = LabelPosition.TextToFindIsBetweenLabels,
                        Format = "Text",
                        PreviousLinesToFetch = 0,
                        NextLinesToFetch = 30,
                        IncludeStartLabelText = true,
                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithMultipleValuesPerLabel,
                        SubLabels = [
                            GetLinkedLicenceNumber("OtherConditionsLinkedLicenceNumber"),
                            ..GetLimitLineSubLabels(7)
                        ]
                    }
                ]
            }
        ];
    }
    
    private static List<LabelToMatch> GetAdditional()
    {
        return
        [
            new LabelToMatch
            {
                Name = "AdditionalAll",
                TextStart =
                [
                    new("ADDITIONAL INFORMATION[END_OF_LINE]") { LineMustStartWith = true },
                    new("ADDITIONAL[END_OF_LINE]") { LineMustStartWith = true }
                ],
                TextEnd =
                [
                    new("History of licence[END_OF_LINE]") { LineMustStartWith = true },
                    new("Licence History[END_OF_LINE]") { LineMustStartWith = true },
                    new("Summary of Change[END_OF_LINE]"),
                    new("The following changes to the licence have taken place:"),
                    new("SCHEDULE OF LICENCES[END_OF_LINE]") { LineMustStartWith = true },
                    new("Would you like to find out") { LineMustStartWith = true },
                    new("Map accompanying licence number"),
                    new("[END_OF_BLOCK]")
                ],
                Remove =
                [
                    PageNumberPattern,
                    LicenceNumberInHeaderPattern
                ],
                Position = LabelPosition.TextToFindIsBetweenLabels,
                MultipleServiceMatchBehaviour =
                    MultipleServiceMatchBehaviour.UseMostSubResultsUseLastServiceResultIfEqual,
                IncludeWholeLine = true,
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 100,
                SubLabels = 
                [
                    new()
                    {
                        Name = "AdditionalPoint",
                        TextStart = [
                            new("Abstraction period details[END_OF_LINE]") { LineMustStartWith = true },
                            new("Metering[END_OF_LINE]") { LineMustStartWith = true },
                            new("Screening[END_OF_LINE]") { LineMustStartWith = true },
                            new("Associated abstraction and impoundment licence[END_OF_LINE]") { LineMustStartWith = true },
                            new("Water Vole[END_OF_LINE]") { LineMustStartWith = true },
                            new("Hands off Flow notification[END_OF_LINE]") { LineMustStartWith = true },
                            new("Abstraction Reform[END_OF_LINE]") { LineMustStartWith = true },
                            new("REASONS FOR CONDITIONS[END_OF_LINE]") { LineMustStartWith = true },
                            new("Water efficiency note[END_OF_LINE]") { LineMustStartWith = true },
                            new("IMPORTANT NOTES[END_OF_LINE]") { LineMustStartWith = true },
                            new("[START_OF_BLOCK]")
                        ],
                        TextEnd = [
                            new("Abstraction period details[END_OF_LINE]") { LineMustStartWith = true },
                            new("Metering[END_OF_LINE]") { LineMustStartWith = true },
                            new("Screening[END_OF_LINE]") { LineMustStartWith = true },
                            new("Associated abstraction and impoundment licence[END_OF_LINE]") { LineMustStartWith = true },
                            new("Water Vole[END_OF_LINE]") { LineMustStartWith = true },
                            new("Hands off Flow notification[END_OF_LINE]") { LineMustStartWith = true },
                            new("Abstraction Reform[END_OF_LINE]") { LineMustStartWith = true },
                            new("REASONS FOR CONDITIONS[END_OF_LINE]") { LineMustStartWith = true },
                            new("Water efficiency note[END_OF_LINE]") { LineMustStartWith = true },
                            new("IMPORTANT NOTES[END_OF_LINE]") { LineMustStartWith = true },
                            new("Would you like") { LineMustStartWith = true },
                            new("[END_OF_BLOCK]")
                        ],
                        Position = LabelPosition.TextToFindIsBetweenLabels,
                        Format = "Text",
                        PreviousLinesToFetch = 0,
                        NextLinesToFetch = 30,
                        IncludeStartLabelText = true,
                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithMultipleValuesPerLabel,
                        SubLabels = [
                            GetLinkedLicenceNumber("AdditionalLinkedLicenceNumber"),
                            ..GetLimitLineSubLabels(null)
                        ]
                    }
                ]
            }
        ];
    }

    private static LabelToMatch GetLinkedLicenceAbstractionAndOrPointsLimits()
    {
        return new()
        {
            Name = "LinkedLicenceNumber",
            Text =
            [
                new("licence number "),
                new("licence serial no "),
                new("licence serial no. "),
                new("licence serial number "),
                new("licence serial numbers "),
                new("serial nos"),
                new("under this licence and licence"),
                new("and licence "),
                new("and under licence "),
                new("and under license ") // spelling mistake in licence                                    
            ],
            Position = LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore,
            Format = LicenceNumber.Constant,
            MultipleMatchBehaviour = MultipleMatchBehaviour.FindSingleInstanceOfLabelWithMultipleValues,
            SkipLineWhenContains = NoneLicenceNumberSkips,
            NextLinesToFetch = 10
        };
    }
    
    private static List<LabelToMatch> GetLicenceHistory()
    {
        return
        [
            new LabelToMatch
            {
                Name = "LicenceHistoryAll",
                TextStart =
                [
                    new("History of licence[END_OF_LINE]") { LineMustStartWith = true },
                    new("Licence History[END_OF_LINE]") { LineMustStartWith = true },
                    new("Summary of Change[END_OF_LINE]") { ColumnMustStartWith = true },
                    new("SCHEDULE OF LICENCES[END_OF_LINE]") { LineMustStartWith = true }
                ],
                TextEnd =
                [
                    new("Would you like to find out") { LineMustStartWith = true },
                    new("Map accompanying licence number"),
                    new("[END_OF_BLOCK]")
                ],
                Remove =
                [
                    PageNumberPattern,
                    LicenceNumberInHeaderPattern
                ],
                Position = LabelPosition.TextToFindIsBetweenLabels,
                MultipleServiceMatchBehaviour =
                    MultipleServiceMatchBehaviour.UseMostSubResultsUseLastServiceResultIfEqual,
                IncludeWholeLine = true,
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 100,
                SubLabels = 
                [
                    GetLinkedLicenceNumber("LicenceHistoryLinkedLicenceNumber")
                ]
            }
        ];
    }
    
    private static List<LabelToMatch> GetGeneralLinkedLicenceNumbers()
    {
        return
        [
            GetLinkedLicenceNumber("GeneralLinkedLicenceNumber")
        ];
    }
    
    private static List<LabelToMatch> GetScheduleOfConditionsA()
    {
        return
        [
            new LabelToMatch
            {
                Name = "ScheduleOfConditionsA",
                TextStart =
                [
                    new("SCHEDULE OF CONDITIONS A[END_OF_LINE]"),
                    new("SCHEDULE OF CONDITIONS[END_OF_LINE]"),
                ],
                TextEnd =
                [
                    new("SCHEDULE OF CONDITIONS B[END_OF_LINE]"),
                    new("ADDITIONAL INFORMATION") { LineMustStartWith = true },
                    new("Would you like to find out") { LineMustStartWith = true }
                ],
                Remove =
                [
                    PageNumberPattern,
                    LicenceNumberInHeaderPattern
                ],
                Position = LabelPosition.TextToFindIsBetweenLabels,
                MultipleServiceMatchBehaviour =
                    MultipleServiceMatchBehaviour.UseMostSubResultsUseLastServiceResultIfEqual,
                IncludeWholeLine = true,
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 1_000
            }
        ];
    }
    
    private static List<LabelToMatch> GetScheduleOfConditionsB()
    {
        return
        [
            new LabelToMatch
            {
                Name = "ScheduleOfConditionsB",
                TextStart =
                [
                    new("SCHEDULE OF CONDITIONS B[END_OF_LINE]")
                ],
                TextEnd =
                [
                    new("ADDITIONAL INFORMATION") { LineMustStartWith = true },
                    new("Would you like to find out") { LineMustStartWith = true }
                ],
                Remove =
                [
                    PageNumberPattern,
                    LicenceNumberInHeaderPattern
                ],
                Position = LabelPosition.TextToFindIsBetweenLabels,
                MultipleServiceMatchBehaviour =
                    MultipleServiceMatchBehaviour.UseMostSubResultsUseLastServiceResultIfEqual,
                IncludeWholeLine = true,
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 1_000
            }
        ];
    }
    
    private static List<LabelToMatch> GetFurtherProvisions()
    {
        return
        [
            new LabelToMatch
            {
                Name = "FurtherProvisionsAll",
                TextStart =
                [
                    new("10. FURTHER PROVISIONS[END_OF_LINE]"),
                    new("10 FURTHER PROVISIONS") { LineMustStartWith = true },
                    new("FURTHER PROVISIONS[END_OF_LINE]") { LineMustStartWith = true }
                ],
                TextEnd =
                [
                    new("Reasons For Conditions") { LineMustStartWith = true },
                    new("[END_OF_BLOCK]")
                ],
                Remove =
                [
                    PageNumberPattern,
                    LicenceNumberInHeaderPattern
                ],
                Position = LabelPosition.TextToFindIsBetweenLabels,
                MultipleServiceMatchBehaviour =
                    MultipleServiceMatchBehaviour.UseMostSubResultsUseLastServiceResultIfEqual,
                IncludeWholeLine = true,
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 100,
                SubLabels = 
                [
                    new()
                    {
                        Name = "FurtherProvisionsPoint",
                        TextStart = [
                            new("10.1"),
                            new("10.2"),
                            new("10.3"),
                            new("10.4"),
                            new("10.5"),
                            new("10.6"),
                            new("10.7"),
                            new("10.8"),
                            new("10.9"),
                            new("10.10"),
                            new("1)") { LineMustStartWith = true },
                            new("(1)") { LineMustStartWith = true },
                            new("2)") { LineMustStartWith = true },
                            new("(2)") { LineMustStartWith = true },
                            new("3)") { LineMustStartWith = true },
                            new("(3)") { LineMustStartWith = true },
                            new("4)") { LineMustStartWith = true },
                            new("(4)") { LineMustStartWith = true },                            
                            new("[START_OF_BLOCK]")
                        ],
                        TextEnd = [
                            new("10.2"),
                            new("10.3"),
                            new("10.4"),
                            new("10.5"),
                            new("10.6"),
                            new("10.7"),
                            new("10.8"),
                            new("10.9"),
                            new("10.10"),
                            new("10.11"),
                            new("2)") { LineMustStartWith = true },
                            new("(2)") { LineMustStartWith = true },
                            new("3)") { LineMustStartWith = true },
                            new("(3)") { LineMustStartWith = true },
                            new("4)") { LineMustStartWith = true },
                            new("(4)") { LineMustStartWith = true },
                            new("5)") { LineMustStartWith = true },
                            new("(5)") { LineMustStartWith = true },                            
                            new("[END_OF_BLOCK]")
                        ],
                        Position = LabelPosition.TextToFindIsBetweenLabels,
                        Format = "Text",
                        PreviousLinesToFetch = 0,
                        NextLinesToFetch = 30,
                        IncludeStartLabelText = true,
                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithMultipleValuesPerLabel,
                        SubLabels = [
                            GetLinkedLicenceNumber("FurtherProvisionsLinkedLicenceNumber"),
                            ..GetLimitLineSubLabels(null)
                        ]
                    }
                ]
            }
        ];
    }

    private static List<LabelToMatch> GetFurtherConditions()
    {
        return
        [
            new LabelToMatch
            {
                Name = "FurtherConditionsAll",
                TextStart =
                [
                    new("FURTHER CONDITIONS[END_OF_LINE]")
                ],
                TextEnd =
                [
                    new("ADDITIONAL INFORMATION[END_OF_LINE]") { LineMustStartWith = true },
                    new("[END_OF_BLOCK]")
                ],
                Remove =
                [
                    PageNumberPattern,
                    LicenceNumberInHeaderPattern
                ],
                Position = LabelPosition.TextToFindIsBetweenLabels,
                MultipleServiceMatchBehaviour =
                    MultipleServiceMatchBehaviour.UseMostSubResultsUseLastServiceResultIfEqual,
                IncludeWholeLine = true,
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 60,
                SubLabels = 
                [
                    new()
                    {
                        Name = "FurtherConditionsPoint",
                        TextStart = [
                            new("9.1") { LineMustStartWith = true},
                            new("9.2") { LineMustStartWith = true},
                            new("9.3") { LineMustStartWith = true},
                            new("9.4") { LineMustStartWith = true},
                            new("9.5") { LineMustStartWith = true},
                            new("9.6") { LineMustStartWith = true},
                            new("9.7") { LineMustStartWith = true},
                            new("9.8") { LineMustStartWith = true},
                            new("9.9") { LineMustStartWith = true},
                            new("9.10") { LineMustStartWith = true},
                            new("[START_OF_BLOCK]")
                        ],
                        TextEnd = [
                            new("9.2") { LineMustStartWith = true},
                            new("9.3") { LineMustStartWith = true},
                            new("9.4") { LineMustStartWith = true},
                            new("9.5") { LineMustStartWith = true},
                            new("9.6") { LineMustStartWith = true},
                            new("9.7") { LineMustStartWith = true},
                            new("9.8") { LineMustStartWith = true},
                            new("9.9") { LineMustStartWith = true},
                            new("9.10") { LineMustStartWith = true},
                            new("9.11") { LineMustStartWith = true},
                            new("[END_OF_BLOCK]")
                        ],
                        Position = LabelPosition.TextToFindIsBetweenLabels,
                        Format = "Text",
                        PreviousLinesToFetch = 0,
                        NextLinesToFetch = 30,
                        IncludeStartLabelText = true,
                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithMultipleValuesPerLabel,
                        SubLabels = [
                            GetLinkedLicenceNumber("FurtherConditionsLinkedLicenceNumber"),
                            ..GetLimitLineSubLabels(9)
                        ]
                    }
                ]
            }
        ];
    }

    private static List<LabelToMatch> GetIssuerLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "Issuer",
                Format = "Text",
                Text =
                [
                    new("Environment Agency"),
                    new("Lee Conservancy Catchment Board"),
                    new("National Rivers Authority"),
                    new("South Water Authority"),
                    new("Northumbrian Water Authority"),
                    new("Northumbrian River Authority"),
                    new("North West Water"),
                    new("Wessex Water Authority"),
                    new("Wessex Water"),
                    new("Essex River Authority"),
                    new("Thames Water Authority"),
                    new("Mersey and Weaver River Authority"),
                    new("Conservators of the The River Thames"),
                    new("Yorkshire Ouse and Hull River Authority"),
                    new("Yorkshire River Authority"),
                    new("Avon and Dorset River authority"),
                    new("The Somerset River Authority"),
                    new("Southern Water Authority"),
                    new("Sussex River Authority"),
                    new("Yorkshire Water Authority")                  
                ],
                Possibilities = [
                    new("Environment Agency"),
                    new("Lee Conservancy Catchment Board"),
                    new("National Rivers Authority"),
                    new("South Water Authority"),
                    new("Northumbrian Water Authority"),
                    new("Northumbrian River Authority"),
                    new("North West Water"),
                    new("Wessex Water Authority"),
                    new("Wessex Water"),
                    new("Essex River Authority"),
                    new("Thames Water Authority"),
                    new("Mersey and Weaver River Authority"),
                    new("Conservators of the The River Thames"),
                    new("Yorkshire Ouse and Hull River Authority"),
                    new("Yorkshire River Authority"),
                    new("Avon and Dorset River authority"),
                    new("The Somerset River Authority"),
                    new("Southern Water Authority"),
                    new("Sussex River Authority"),
                    new("Yorkshire Water Authority")
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.ApplicableToMost,
                IncludeStartLabelText = true
            }
        ];
    }
    
    private static List<LabelToMatch> GetDateOfOriginalIssueLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "DateOfOriginalIssue",
                Format = "DateOrPurpose",
                Text =
                [
                    new("Date of original issue..."),
                    new("Date of original issue ..."),
                    new("Date of original issue")
                ],
                Position = LabelPosition.LabelIsBeforeTextToFind,
                MultipleServiceMatchBehaviour = MultipleServiceMatchBehaviour.UseFullestDateUseHighestOcrConfidenceIfMultipleFull,
                Remove = [
                    new("...")
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 1
            }
        ];
    }
    
    private static List<LabelToMatch> GetDateEffectiveLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "DateEffective",
                Format = "DateOrPurpose",
                Text =
                [
                    new("Date effective..."),
                    new("Date effective ..."),
                    new("Date effective")
                ],
                Position = LabelPosition.LabelIsBeforeTextToFind,
                MultipleServiceMatchBehaviour = MultipleServiceMatchBehaviour.UseFullestDateUseHighestOcrConfidenceIfMultipleFull,
                Remove = [
                    new("...")
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 1
            }
        ];
    }    
    
    private static List<LabelToMatch> GetDateOfExpiryLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "DateOfExpiry",
                Format = "DateOrPurpose",
                Text =
                [
                    new("Date of expiry..."),
                    new("Date of expiry ...")
                ],
                Position = LabelPosition.LabelIsBeforeTextToFind,
                MultipleServiceMatchBehaviour = MultipleServiceMatchBehaviour.UseFullestDateUseHighestOcrConfidenceIfMultipleFull,
                Remove = [
                    new("...")
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 1
            }
        ];
    }
    
    private static List<LabelToMatch> GetPointsLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "DocumentPointsAll",
                TextStart =
                [
                    new("2. POINT OF ABSTRACTION") { IfMultiplePreferLast = true },
                    new("POINT OF ABSTRACTION[END_OF_COLUMN]")
                    {
                        ColumnMustStartWith = true,
                        IfMultiplePreferLast = true
                    },
                    new("2. POINT(S) OF ABSTRACTION") { IfMultiplePreferLast = true },
                    new("2. POINTS OF ABSTRACTION") { IfMultiplePreferLast = true },
                    new("Source of supply and authorised place(s) of abstraction") { IfMultiplePreferLast = true },
                    new("Source of supply and place of abstraction") { IfMultiplePreferLast = true },
                    new("Source(s) of supply and authorised place(s) of abstraction") { IfMultiplePreferLast = true },
                    new("Authorised place(s) of abstraction[END_OF_COLUMN]") { LineMustStartWith = true, IfMultiplePreferLast = true },
                    new("Authorised place(s) of abstraction.[END_OF_COLUMN]") { LineMustStartWith = true, IfMultiplePreferLast = true }
                ],
                TextEnd =
                [
                    new("MEANS OF ABSTRACTION"),
                    new("MEAN OF ABSTRACTION"),
                    new("Land(s) on which water is authorised to be used"),
                    new("Quantity(ies) of Water Authorised to be Abstracted"),
                    new("POINT OF ABSTRACTION[END_OF_COLUMN]")
                    {
                        ColumnMustStartWith = true,
                        IfMultiplePreferLast = true
                    },
                    new("[END_OF_BLOCK]")
                ],
                Remove =
                [
                    PageNumberPattern,
                    LicenceNumberInHeaderPattern
                ],
                MultipleMatchBehaviour = MultipleMatchBehaviour.FindSingleInstanceOfLabelWithMultipleValues, // Only here for 'IfMultiplePreferLast'
                Position = LabelPosition.TextToFindIsBetweenLabels,
                IncludeWholeLine = true,
                MinimumSubMatches = 1,
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 100,
                SubLabels = new List<LabelToMatch>
                {
                    new()
                    {
                        Name = "PointPurposeGroup",
                        TextStart = [
                            new("For Purpose "),
                            new(string.Empty) { SingleLinePerItem = true },
                            new("[START_OF_BLOCK]")
                        ],
                        TextEnd = [
                            new("For Purpose ") { InstanceNumber = 2 },
                            new("[END_OF_BLOCK]")
                        ],
                        Position = LabelPosition.TextToFindIsBetweenLabels,
                        Format = "Text",
                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithMultipleValuesPerLabel,
                        IncludeWholeLine = true,
                        PreviousLinesToFetch = 0,
                        NextLinesToFetch = 100,
                        Remove = [
                            new("2. POINT OF ABSTRACTION"),
                            new("2. POINT(S) OF ABSTRACTION"),
                            new("2. POINTS OF ABSTRACTION"),
                            new("1. SOURCE OF SUPPLY"),
                            new("Source of Supply and authorised Place(s) of abstraction"),
                            new("SOURCE OF SUPPLY"),
                            new("Point Reference")
                        ],
                        SubLabels =
                        [
                            new()
                            {
                                Name = "PurposeGroupName",
                                Text = [
                                    new("For Purpose ")
                                ],
                                Position = LabelPosition.LabelIsBeforeTextToFind,
                                Format = "Text",
                                SubLabels =
                                [
                                    new()
                                    {
                                        Name = "PurposeGroupSub",
                                        Text = [new("and ")],
                                        Position = LabelPosition.SplitAtLabel,
                                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindSingleInstanceOfLabelWithMultipleValues
                                    }
                                ]
                            },
                            new()
                            {
                                Name = "Point",
                                TextStart = [
                                    new("2.1"),
                                    new("2.2"),
                                    new("2.3"),
                                    new("2.4"),
                                    new("2.5"),
                                    new("2.6"),
                                    new("2.7"),
                                    new("2.8"),
                                    new("2.9"),
                                    new("2.10"),
                                    new("(1)"),
                                    new("(2)"),
                                    new("(3)"),
                                    new("(4)"),
                                    new ("NZ ") { ColumnMustStartWith = true },
                                    new ("A[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("B[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("C[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("D[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("E[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("F[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("G[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("H[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("I[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("J[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("K[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("L[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("M[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("N[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("O[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("P[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("Q[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("R[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("S[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("T[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("U[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("V[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("W[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("X[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("Y[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new("[START_OF_BLOCK]")
                                ],
                                TextEnd = [
                                    new("2.2"),
                                    new("2.3"),
                                    new("2.4"),
                                    new("2.5"),
                                    new("2.6"),
                                    new("2.7"),
                                    new("2.8"),
                                    new("2.9"),
                                    new("2.10"),
                                    new("2.11"),
                                    new("(2)"),
                                    new("(3)"),
                                    new("(4)"),
                                    new ("NZ ") { ColumnMustStartWith = true },
                                    new ("B[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("C[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("D[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("E[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("F[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("G[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("H[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("I[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("J[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("K[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("L[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("M[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("N[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("O[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("P[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("Q[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("R[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("S[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("T[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("U[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("V[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("W[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("X[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("Y[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("Z[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new("[END_OF_BLOCK]")
                                ],
                                IgnoreMatchIfContains = [
                                    "At the following National Grid References as marked on the maps"
                                ],
                                Position = LabelPosition.TextToFindIsBetweenLabels,
                                Format = "Text",
                                PreviousLinesToFetch = 0,
                                NextLinesToFetch = 100,
                                IncludeStartLabelText = true,
                                MultipleMatchBehaviour = MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithMultipleValuesPerLabel,
                                SubLabels = new List<LabelToMatch>
                                {
                                    new()
                                    {
                                        Name = "PointTable",
                                        Position =  LabelPosition.TextToFindIsBetweenLabels,
                                        TextStart = [
                                            new("Location Description Map"),
                                        ],
                                        TextEnd = [
                                            new("[END_OF_BLOCK]")
                                        ],
                                        Remove = [
                                            new("Point Reference")
                                        ],
                                        PreviousLinesToFetch = 0,
                                        NextLinesToFetch = 5
                                    },
                                    new()
                                    {
                                        Name = "PointPointNumber",
                                        Possibilities = [
                                            new("2.1"),
                                            new("2.2"),
                                            new("2.3"),
                                            new("2.4"),
                                            new("2.5"),
                                            new("2.6"),
                                            new("2.7"),
                                            new("2.8"),
                                            new("2.9"),
                                            new("2.10"),
                                            new("(1)"),
                                            new("(2)"),
                                            new("(3)"),
                                            new("(4)"),
                                            new("A "),
                                            new("B "),
                                            new("C "),
                                            new("D "),
                                            new("E "),
                                            new("F "),
                                            new("G "),
                                            new("H ")
                                        ],
                                        Position = LabelPosition.ApplicableToMost,
                                        Format = "Text",
                                        PreviousLinesToFetch = 0,
                                        NextLinesToFetch = 0,
                                        IncludeStartLabelText = true
                                    },
                                    new()
                                    {
                                        Name = "PurposeLink",
                                        Text = [
                                            new("For Purpose ")
                                        ],
                                        Position = LabelPosition.LabelIsBeforeTextToFind,
                                        Format = "Text",
                                        SubLabels =
                                        [
                                            new LabelToMatch
                                            {
                                                Name = "PurposeLinkSub",
                                                Text = [new("and ")],
                                                Position = LabelPosition.SplitAtLabel,
                                                MultipleMatchBehaviour = MultipleMatchBehaviour.FindSingleInstanceOfLabelWithMultipleValues
                                            }
                                        ]
                                    },
                                    new()
                                    {
                                        Name = "PointTextWithoutPurposeAndPoint",
                                        Remove = [
                                            new("2.1") { ColumnMustStartWith = true },
                                            new("2.2") { ColumnMustStartWith = true },
                                            new("2.3") { ColumnMustStartWith = true },
                                            new("2.4") { ColumnMustStartWith = true },
                                            new("2.5") { ColumnMustStartWith = true },
                                            new("2.6") { ColumnMustStartWith = true },
                                            new("2.7") { ColumnMustStartWith = true },
                                            new("2.8") { ColumnMustStartWith = true },
                                            new("2.9") { ColumnMustStartWith = true },
                                            new("2.10") { ColumnMustStartWith = true },
                                            new("(1)"),
                                            new("(2)"),
                                            new("(3)"),
                                            new("(4)"),
                                            new("For Purpose 4.1") { RemoveWholeLine = true },
                                            new("For Purpose 4.2") { RemoveWholeLine = true },
                                            new("For Purpose 4.3") { RemoveWholeLine = true },
                                            new("For Purpose 4.4") { RemoveWholeLine = true },
                                            new("Map 1"),
                                            new("Map 2"),
                                            new("Map 3"),
                                            new("A ") { ColumnMustStartWith = true },
                                            new("B ") { ColumnMustStartWith = true },
                                            new("C ") { ColumnMustStartWith = true },
                                            new("D ") { ColumnMustStartWith = true },
                                            new("E ") { ColumnMustStartWith = true },
                                            new("F ") { ColumnMustStartWith = true },
                                            new("G ") { ColumnMustStartWith = true },
                                            new("H ") { ColumnMustStartWith = true },
                                            new("I ") { ColumnMustStartWith = true },
                                            new("J ") { ColumnMustStartWith = true },                                            
                                        ],
                                        Text = [
                                            new("marked") // TODO ' marked ' doesn't work, change so it does
                                        ],
                                        Position = LabelPosition.SplitAtLabel,
                                        Format = "Text",
                                        PreviousLinesToFetch = 100,
                                        NextLinesToFetch = 10,
                                        DoNotTrimLines = true
                                    },
                                    GetLinkedLicenceAbstractionAndOrPointsLimits()
                                }
                            }
                        ]
                    }
                }
            }
        ];
    }
    
    private static List<LabelToMatch> GetSourceOfSupplyLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "SourceOfSupplyAll",
                TextStart =
                [
                    new("Source of supply[END_OF_COLUMN]") { ColumnMustStartWith = true, IfMultiplePreferLast = true },
                    new("1. Source of supply[END_OF_COLUMN]") { LineMustStartWith = true, IfMultiplePreferLast = true }
                ],
                TextEnd =
                [
                    new("MEANS OF ABSTRACTION"),
                    new("MEAN OF ABSTRACTION"),
                    new("Land(s) on which water is authorised to be used"),
                    new("Quantity(ies) of Water Authorised to be Abstracted"),
                    new("POINT OF ABSTRACTION[END_OF_COLUMN]")
                    {
                        ColumnMustStartWith = true,
                        IfMultiplePreferLast = true
                    },
                    new("[END_OF_BLOCK]")
                ],
                Remove =
                [
                    PageNumberPattern,
                    LicenceNumberInHeaderPattern
                ],
                MultipleMatchBehaviour = MultipleMatchBehaviour.FindSingleInstanceOfLabelWithMultipleValues, // Only here for 'IfMultiplePreferLast'
                Position = LabelPosition.TextToFindIsBetweenLabels,
                IncludeWholeLine = true,
                MinimumSubMatches = 1,
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 100,
                SubLabels = new List<LabelToMatch>
                {
                    new()
                    {
                        Name = "PointPurposeGroup",
                        TextStart = [
                            new("For Purpose "),
                            new(string.Empty) { SingleLinePerItem = true },
                            new("[START_OF_BLOCK]")
                        ],
                        TextEnd = [
                            new("For Purpose ") { InstanceNumber = 2 },
                            new("[END_OF_BLOCK]")
                        ],
                        Position = LabelPosition.TextToFindIsBetweenLabels,
                        Format = "Text",
                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithMultipleValuesPerLabel,
                        IncludeWholeLine = true,
                        PreviousLinesToFetch = 0,
                        NextLinesToFetch = 100,
                        Remove = [
                            new("2. POINT OF ABSTRACTION"),
                            new("2. POINT(S) OF ABSTRACTION"),
                            new("2. POINTS OF ABSTRACTION"),
                            new("1. SOURCE OF SUPPLY"),
                            new("Source of Supply and authorised Place(s) of abstraction"),
                            new("SOURCE OF SUPPLY"),
                            new("Point Reference")
                        ],
                        SubLabels =
                        [
                            new()
                            {
                                Name = "PurposeGroupName",
                                Text = [
                                    new("For Purpose ")
                                ],
                                Position = LabelPosition.LabelIsBeforeTextToFind,
                                Format = "Text",
                                SubLabels =
                                [
                                    new()
                                    {
                                        Name = "PurposeGroupSub",
                                        Text = [new("and ")],
                                        Position = LabelPosition.SplitAtLabel,
                                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindSingleInstanceOfLabelWithMultipleValues
                                    }
                                ]
                            },
                            new()
                            {
                                Name = "Point",
                                TextStart = [
                                    new("2.1"),
                                    new("2.2"),
                                    new("2.3"),
                                    new("2.4"),
                                    new("2.5"),
                                    new("2.6"),
                                    new("2.7"),
                                    new("2.8"),
                                    new("2.9"),
                                    new("2.10"),
                                    new("(1)"),
                                    new("(2)"),
                                    new("(3)"),
                                    new("(4)"),
                                    new ("NZ ") { ColumnMustStartWith = true },
                                    new ("A[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("B[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("C[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("D[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("E[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("F[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("G[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("H[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("I[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("J[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("K[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("L[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("M[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("N[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("O[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("P[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("Q[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("R[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("S[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("T[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("U[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("V[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("W[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("X[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("Y[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new("[START_OF_BLOCK]")
                                ],
                                TextEnd = [
                                    new("2.2"),
                                    new("2.3"),
                                    new("2.4"),
                                    new("2.5"),
                                    new("2.6"),
                                    new("2.7"),
                                    new("2.8"),
                                    new("2.9"),
                                    new("2.10"),
                                    new("2.11"),
                                    new("(2)"),
                                    new("(3)"),
                                    new("(4)"),
                                    new ("NZ ") { ColumnMustStartWith = true },
                                    new ("B[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("C[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("D[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("E[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("F[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("G[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("H[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("I[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("J[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("K[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("L[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("M[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("N[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("O[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("P[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("Q[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("R[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("S[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("T[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("U[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("V[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("W[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("X[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("Y[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("Z[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new("[END_OF_BLOCK]")
                                ],
                                IgnoreMatchIfContains = [
                                    "At the following National Grid References as marked on the maps"
                                ],
                                Position = LabelPosition.TextToFindIsBetweenLabels,
                                Format = "Text",
                                PreviousLinesToFetch = 0,
                                NextLinesToFetch = 100,
                                IncludeStartLabelText = true,
                                MultipleMatchBehaviour = MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithMultipleValuesPerLabel,
                                SubLabels = new List<LabelToMatch>
                                {
                                    new()
                                    {
                                        Name = "PointTable",
                                        Position =  LabelPosition.TextToFindIsBetweenLabels,
                                        TextStart = [
                                            new("Location Description Map"),
                                        ],
                                        TextEnd = [
                                            new("[END_OF_BLOCK]")
                                        ],
                                        Remove = [
                                            new("Point Reference")
                                        ],
                                        PreviousLinesToFetch = 0,
                                        NextLinesToFetch = 5
                                    },
                                    new()
                                    {
                                        Name = "PointPointNumber",
                                        Possibilities = [
                                            new("2.1"),
                                            new("2.2"),
                                            new("2.3"),
                                            new("2.4"),
                                            new("2.5"),
                                            new("2.6"),
                                            new("2.7"),
                                            new("2.8"),
                                            new("2.9"),
                                            new("2.10"),
                                            new("(1)"),
                                            new("(2)"),
                                            new("(3)"),
                                            new("(4)"),
                                            new("A "),
                                            new("B "),
                                            new("C "),
                                            new("D "),
                                            new("E "),
                                            new("F "),
                                            new("G "),
                                            new("H ")
                                        ],
                                        Position = LabelPosition.ApplicableToMost,
                                        Format = "Text",
                                        PreviousLinesToFetch = 0,
                                        NextLinesToFetch = 0,
                                        IncludeStartLabelText = true
                                    },
                                    new()
                                    {
                                        Name = "PurposeLink",
                                        Text = [
                                            new("For Purpose ")
                                        ],
                                        Position = LabelPosition.LabelIsBeforeTextToFind,
                                        Format = "Text",
                                        SubLabels =
                                        [
                                            new LabelToMatch
                                            {
                                                Name = "PurposeLinkSub",
                                                Text = [new("and ")],
                                                Position = LabelPosition.SplitAtLabel,
                                                MultipleMatchBehaviour = MultipleMatchBehaviour.FindSingleInstanceOfLabelWithMultipleValues
                                            }
                                        ]
                                    },
                                    new()
                                    {
                                        Name = "PointTextWithoutPurposeAndPoint",
                                        Remove = [
                                            new("2.1") { ColumnMustStartWith = true },
                                            new("2.2") { ColumnMustStartWith = true },
                                            new("2.3") { ColumnMustStartWith = true },
                                            new("2.4") { ColumnMustStartWith = true },
                                            new("2.5") { ColumnMustStartWith = true },
                                            new("2.6") { ColumnMustStartWith = true },
                                            new("2.7") { ColumnMustStartWith = true },
                                            new("2.8") { ColumnMustStartWith = true },
                                            new("2.9") { ColumnMustStartWith = true },
                                            new("2.10") { ColumnMustStartWith = true },
                                            new("(1)"),
                                            new("(2)"),
                                            new("(3)"),
                                            new("(4)"),
                                            new("For Purpose 4.1") { RemoveWholeLine = true },
                                            new("For Purpose 4.2") { RemoveWholeLine = true },
                                            new("For Purpose 4.3") { RemoveWholeLine = true },
                                            new("For Purpose 4.4") { RemoveWholeLine = true },
                                            new("Map 1"),
                                            new("Map 2"),
                                            new("Map 3"),
                                            new("A ") { ColumnMustStartWith = true },
                                            new("B ") { ColumnMustStartWith = true },
                                            new("C ") { ColumnMustStartWith = true },
                                            new("D ") { ColumnMustStartWith = true },
                                            new("E ") { ColumnMustStartWith = true },
                                            new("F ") { ColumnMustStartWith = true },
                                            new("G ") { ColumnMustStartWith = true },
                                            new("H ") { ColumnMustStartWith = true },
                                            new("I ") { ColumnMustStartWith = true },
                                            new("J ") { ColumnMustStartWith = true },                                            
                                        ],
                                        Text = [
                                            new("marked") // TODO ' marked ' doesn't work, change so it does
                                        ],
                                        Position = LabelPosition.SplitAtLabel,
                                        Format = "Text",
                                        PreviousLinesToFetch = 100,
                                        NextLinesToFetch = 10,
                                        DoNotTrimLines = true
                                    },
                                    GetLinkedLicenceAbstractionAndOrPointsLimits()
                                }
                            }
                        ]
                    }
                }
            }
        ];
    }

    private static List<LabelToMatch> GetPurposeLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "DocumentPurposesAll",
                TextStart =
                [
                    new("PURPOSE OF ABSTRACTION"),
                    new("PURPOSE(S) OF ABSTRACTION"),
                    new("PURPOSES OF ABSTRACTION"),
                    new("Purpose for which water is authorised to be used[END_OF_LINE]"),
                    new("Purpose(s) for which water is authorised to be used"),
                    new("Purpose for which the water is to be used") { LineMustStartWith = true },
                    new("Purpose for which water is to be used :") { LineMustStartWith = true }
                ],
                TextEnd =
                [
                    new("PERIODS OF ABSTRACTION"),
                    new("PERIOD(S) OF ABSTRACTION"),
                    new("PERIOD OF ABSTRACTION"),
                    new("LAND ON WHICH LICENCE AUTHORISES USE OF WATER"),
                    new("Quantities of water authorised to be abstracted"),
                    new("QUANTITY(IES) OF WATER AUTHORISED"),
                    new("QUANTITY (IES) OF WATER AUTHORISED"),                    
                    new("The quantity of water authorised to be abstracted shall be"),
                    new("During the months") { LineMustStartWith = true},
                    new("the months of"), // For some licence with bad scanning
                    new("[END_OF_BLOCK]")
                ],
                Remove =
                [
                    PageNumberPattern,
                    LicenceNumberInHeaderPattern
                ],
                IgnoreMatchIfContains = [
                    "You can find our forms"
                ],
                Position = LabelPosition.TextToFindIsBetweenLabels,
                IncludeWholeLine = true,
                MinimumSubMatches = 1,
                NextLinesToFetch = 30,
                SubLabels = 
                [
                    new()
                    {
                        Name = "PurposePointGroup",
                        TextStart = [
                            new("From Point "),
                            new("[START_OF_BLOCK]")
                        ],
                        TextEnd = [
                            new("From Point ") { InstanceNumber = 2 },
                            new("[END_OF_BLOCK]")
                        ],
                        Position = LabelPosition.TextToFindIsBetweenLabels,
                        Format = "Text",
                        NextLinesToFetch = 30,
                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithMultipleValuesPerLabel,
                        IncludeWholeLine = true,
                        Remove = [
                            new("4. PURPOSE OF ABSTRACTION"),
                            new("4. PURPOSE(S) OF ABSTRACTION"),
                            new("4. PURPOSES OF ABSTRACTION"),
                            new("PURPOSE OF ABSTRACTION"),
                            new("PURPOSES OF ABSTRACTION"),
                            new("PURPOSE(S) OF ABSTRACTION"),
                            new("Purpose(s) for which water is authorised to be used"),
                            new("PURPOSE(S) FOR WHICH WATER IS AUTHORISED TO BE USED"), // TODO why does the capitalisation matter here?
                            new("PURPOSE FOR WHICH WATER IS TO BE USED :")
                        ],
                        SubLabels =
                        [
                            new()
                            {
                                Name = "PointGroupName",
                                Text = [
                                    new("From Point ")
                                ],
                                Format = "Number",
                                Position = LabelPosition.LabelIsBeforeTextToFind,
                                Possibilities = [
                                    new("2.1"),
                                    new("2.2"),
                                    new("2.3"),
                                    new("2.4"),
                                    new("2.5"),
                                    new("2.6"),
                                    new("2.7"),
                                    new("2.8"),
                                    new("2.9"),
                                    new("2.10")             
                                ],
                                PreviousLinesToFetch = 0,
                                NextLinesToFetch = 0,
                                SubLabels =
                                [
                                    new()
                                    {
                                        Name = "PointGroupSub",
                                        Text = [new("and ")],
                                        Position = LabelPosition.SplitAtLabel,
                                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindSingleInstanceOfLabelWithMultipleValues
                                    }
                                ]
                            },
                            new()
                            {
                                Name = "Purposes",
                                TextStart = [
                                    new("4.1"),
                                    new("4.2"),
                                    new("4.3"),
                                    new("4.4"),
                                    new("(a)"),
                                    new("(b)"),
                                    new("(c)"),
                                    new("(d)"),
                                    new("(1)"),
                                    new("(2)"),
                                    new("(3)"),
                                    new("(4)"),
                                    new("(5)"),
                                    new("[START_OF_BLOCK]")
                                ],
                                TextEnd = [
                                    new("4.2"),
                                    new("4.3"),
                                    new("4.4"),
                                    new("(b)"),
                                    new("(c)"),
                                    new("(d)"),                                    
                                    new("(e)"),
                                    new("(2)"),
                                    new("(3)"),
                                    new("(4)"),
                                    new("(5)"),
                                    new("(6)"),
                                    new("[END_OF_BLOCK]")
                                ],
                                Position = LabelPosition.TextToFindIsBetweenLabels,
                                IncludeStartLabelText = true,
                                Format = "Text",
                                MultipleMatchBehaviour = MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithMultipleValuesPerLabel,
                                SubLabels =
                                [
                                    new()
                                    {
                                        Name = "PurposeNumber",
                                        Possibilities = [
                                            new("4.1"),
                                            new("4.2"),
                                            new("4.3"),
                                            new("(a)"),
                                            new("(b)"),
                                            new("(c)"),
                                            new("(d)"),
                                            new("(e)"),
                                            new("(1)"),
                                            new("(2)"),
                                            new("(3)"),
                                            new("(4)"),
                                            new("(5)"),
                                        ],
                                        Position = LabelPosition.ApplicableToMost,
                                        Format = "Text",
                                        PreviousLinesToFetch = 0,
                                        NextLinesToFetch = 0
                                    },
                                    new()
                                    {
                                        Name = "TextWithoutPoints",
                                        Remove = [
                                            new("From Point 2.1"),
                                            new("From Point 2.2"),
                                            new("From Point 2.3"),
                                            new("From Point 2.4"),
                                            new("From Point 2.5"),
                                            new("From Point 2.6"),
                                            new("From Point 2.7"),
                                            new("From Point 2.8"),
                                            new("From Point 2.9"),
                                            new("From Point 2.10"),
                                            new("4.1"),
                                            new("4.2"),
                                            new("4.3"),
                                            new("4.4"),
                                            new("(a)"),
                                            new("(b)"),
                                            new("(c)"),
                                            new("(d)"),
                                            new("(e)"),
                                            new("(1)"),
                                            new("(2)"),
                                            new("(3)"),
                                            new("(4)"),
                                            new("(5)")
                                        ],
                                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindSingleInstanceOfLabelWithASingleValueButMultipleLines,
                                        Position = LabelPosition.ApplicableToMost,
                                        Format = "Text"
                                    },
                                    GetLinkedLicenceNumber("PurposeLinkedLicenceNumber")
                                ]
                            }
                        ]
                    }
                ]
            }
        ];
    }

    private static List<LabelToMatch> GetCompanyNameLabels()
    {
        return
        [
            new LabelToMatch
            {
                Text =
                [
                    new("Licensee"),
                    new("\"hereby licence\""),
                    new("\"hereby license\""),
                    new("\"hereby licenge\""),
                    new("hereby licence ..."),
                    new("authority hereby licence"),
                    new("authority hereby license"),
                    new("authority hereby licenge"),
                    new("hereby grant a licence to"),
                    new("(hereinafter referred to as \"the Authority\")")
                ],
                Position = LabelPosition.LabelIsBeforeTextToFind,
                Format = "CompanyName",
                AutoCorrect = true,
                PreviousLinesToFetch = 1,
                NextLinesToFetch = 6,
                Name = "CompanyName",
                IgnoreMatchIfContains = [
                    "source of supply",
                    "abstract water"
                ],
                Remove = [
                    new("hereby grant a licence to")
                ],
                ConfidenceType = ConfidenceType.OcrConfidenceMultiplied,
                ConfidenceIfMatched = 85
            },
            new LabelToMatch
            {
                Text =
                [
                    new("(hereinafter referred to as \"The Licence Holder\")"),
                    new("(hereinafter referred to as \"The Licence Holder\" )"),
                    new("( hereinafter referred to as \"The Licence Holder\" )"),
                    new("( hereinafter referred to as \"The Licence Holder\")"),
                    new("(hereinafter referred to as \" The Licence Holder \")"),
                    new("(hereinafter referred to as \"The Licence Holder)"),
                    new("is hereby licensed")
                ],
                Position = LabelPosition.LabelIsAfterTextToFind,
                Format = "CompanyName",
                Name = "CompanyName2",
                AutoCorrect = true,
                NextLinesToFetch = 4,
                PreviousLinesToFetch = 5,
                IgnoreMatchIfContains = [
                    "source of supply",
                    "abstract water"
                ],
                ConfidenceType = ConfidenceType.OcrConfidenceMultiplied,
                ConfidenceIfMatched = 90
            },
            new LabelToMatch
            {
                Text =
                [
                    new("(\"the Licence Holder\")"),
                    new("(the Licence Holder\")"),
                    new("\"the Licence Holder\""),
                    new("'the Licence Holder\""),
                    new("\"the Licence Holder'")
                ],
                Position = LabelPosition.LabelIsInMiddleOfTextToFind,
                Format = "CompanyName",
                Name = "CompanyName3",
                AutoCorrect = true,
                PreviousLinesToFetch = 2,
                NextLinesToFetch = 4,
                IgnoreMatchIfContains = [
                    "source of supply",
                    "abstract water"
                ],
                ConfidenceType = ConfidenceType.OcrConfidenceMultiplied,
                ConfidenceIfMatched = 85
            },
            new LabelToMatch
            {
                Text =
                [
                    new("Succession to licence"),
                    new("as amended by")
                ],
                Position = LabelPosition.ContractIsSuccession,
                NextLinesToFetch = 10,
                PreviousLinesToFetch = 2,
                Format = "CompanyName",
                MatchAllText = true,
                Name = "IsSuccession",
                ConfidenceType = ConfidenceType.OcrConfidenceMultiplied,
                ConfidenceIfMatched = 95
            }
        ];
    }

    private static List<LabelToMatch> GetPeriodsOfAbstractionLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "DocumentPeriodsOfAbstractionSection",
                TextStart =
                [
                    new("PERIOD OF ABSTRACTION"),
                    new("PERIODS OF ABSTRACTION")
                ],
                TextEnd =
                [
                    new("MAXIMUM QUANTITY OF WATER TO BE ABSTRACTED"),
                    new("FURTHER CONDITIONS"),
                    new("[END_OF_BLOCK]")
                ],
                Position = LabelPosition.TextToFindIsBetweenLabels,
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 15,
                SubLabels = [
                    new()
                    {
                        Name = "PeriodOfAbstractionSubSection",
                        TextStart = [
                            new("5.1"),
                            new("5.2"),
                            new("5.3"),
                            new("5.4"),
                            new("5.5"),
                            new("5.6"),
                            new("5.7"),
                            new("5.8"),
                            new("5.9"),
                            new("5.10"),
                            new("(1)"),
                            new("(2)"),
                            new("(3)"),
                            new("(4)"),
                            new("(5)"),
                            new("[START_OF_BLOCK]")
                        ],
                        TextEnd = [
                            new("5.2"),
                            new("5.3"),
                            new("5.4"),
                            new("5.5"),
                            new("5.6"),
                            new("5.7"),
                            new("5.8"),
                            new("5.9"),
                            new("5.10"),
                            new("(2)"),
                            new("(3)"),
                            new("(4)"),
                            new("(5)"),
                            new("(6)"),
                            new("[END_OF_BLOCK]")
                        ],
                        Position = LabelPosition.TextToFindIsBetweenLabels,
                        Format = "Text",
                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithMultipleValuesPerLabel,
                        PreviousLinesToFetch = 0,
                        NextLinesToFetch = 10,
                        IncludeStartLabelText = true,
                        SubLabels =
                        [
                            new()
                            {
                                Name = "PeriodPeriodNumber",
                                Possibilities = [
                                    new("5.1"),
                                    new("5.2"),
                                    new("5.3"),
                                    new("(1)"),
                                    new("(2)"),
                                    new("(3)"),
                                    new("(4)"),
                                    new("(5)")
                                ],
                                Position = LabelPosition.ApplicableToMost,
                                Format = "Number",
                                PreviousLinesToFetch = 0,
                                NextLinesToFetch = 0                              
                            },
                            new()
                            {
                                Name = "PurposeLink",
                                Text = [
                                    new("For Purpose "),
                                    new("For Purposes ")
                                ],
                                Position = LabelPosition.LabelIsBeforeTextToFind,
                                Format = "Text",
                                SubLabels =
                                [
                                    new()
                                    {
                                        Name = "PurposeLinkSub",
                                        Text = [new("and ")],
                                        Position = LabelPosition.SplitAtLabel,
                                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindSingleInstanceOfLabelWithMultipleValues
                                    }
                                ]
                            },
                            new()
                            {
                                Name = "PeriodTextWithoutPurposeAndPoint",
                                Remove = [
                                    new("5.1") { ColumnMustStartWith = true },
                                    new("5.2") { ColumnMustStartWith = true },
                                    new("5.3") { ColumnMustStartWith = true },
                                    new("5.4") { ColumnMustStartWith = true },
                                    new("(1)") { ColumnMustStartWith = true },
                                    new("(2)") { ColumnMustStartWith = true },
                                    new("(3)") { ColumnMustStartWith = true },
                                    new("(4)") { ColumnMustStartWith = true },
                                    new("(5)") { ColumnMustStartWith = true },
                                    new("For Purpose ") { RemoveWholeLine = true },
                                    new("For Purposes ") { RemoveWholeLine = true }                                   
                                ],
                                MultipleMatchBehaviour = MultipleMatchBehaviour.FindSingleInstanceOfLabelWithASingleValueButMultipleLines,
                                Position = LabelPosition.ApplicableToMost,
                                Format = "Text",
                                SubLabels = [
                                    new()
                                    {
                                        Name = "Dates",
                                        Text = [new("to ")],
                                        Remove = [
                                            new("From "),
                                            new("inclusive")
                                        ],
                                        Position = LabelPosition.SplitAtLabel,
                                        Format = "DateOrPurpose",
                                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindSingleInstanceOfLabelWithMultipleValues
                                    }
                                ]
                            }
                        ]
                    }
                ]
            },
            new LabelToMatch
            {
                Name = "DuringTheMonthsXToYOnlyText",
                TextStart = [
                    new("During the months ") { LineMustStartWith = true }
                ],
                TextEnd = [
                    new("only")
                ],
                NextLinesToFetch = 1,
                PreviousLinesToFetch = 0,
                Remove = [
                    new("only")
                ],
                SubLabels = [
                    new LabelToMatch
                    {
                        Name = "DuringTheMonthsXToYOnlyTextParts",
                        Text = [new("to")],
                        Position = LabelPosition.SplitAtLabel,
                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindSingleInstanceOfLabelWithMultipleValues,
                    }
                ]
            },
        ];
    }

    private static List<LabelToMatch> GetMeansOfAbstractionLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "DocumentMeansOfAbstractionSection",
                TextStart =
                [
                    new("MEANS OF ABSTRACTION")
                ],
                TextEnd =
                [
                    new("PURPOSE OF ABSTRACTION"),
                    new("[END_OF_BLOCK]")
                ],
                Position = LabelPosition.TextToFindIsBetweenLabels,
                PreviousLinesToFetch = 3,
                NextLinesToFetch = 20,
                SubLabels =
                [
                    new()
                    {
                        Name = "Mean",
                        TextStart = [
                            new("3.1"),
                            new("3.2"),
                            new("3.3"),
                            new("3.4"),
                            new("[START_OF_BLOCK]")
                        ],
                        TextEnd = [
                            new("3.2"),
                            new("3.3"),
                            new("3.4"),
                            new("[END_OF_BLOCK]")
                        ],
                        Position = LabelPosition.TextToFindIsBetweenLabels,
                        Format = "Text",
                        NextLinesToFetch = 6,
                        IncludeStartLabelText = true,
                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithMultipleValuesPerLabel,
                        SubLabels =
                        [
                            new()
                            {
                                Name = "MeanId",
                                Possibilities = [
                                    new("3.1"),
                                    new("3.2"),
                                    new("3.3")
                                ],
                                Position = LabelPosition.ApplicableToMost,
                                Format = "Number",
                                PreviousLinesToFetch = 0,
                                NextLinesToFetch = 0
                            },
                            new()
                            {
                                Name = "PerSecondUnitsMeans",                                
                                CategoryName = "PerUnits",                                
                                Text = [new("per second")],
                                Position = LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter,
                                Format = "Units",
                                PreviousLinesToFetch = 1,
                                NextLinesToFetch = 1,
                                Possibilities = new List<TextToMatch>
                                {
                                    new("megalitres"),
                                    new("litres"),
                                    new("cubic metres"),
                                    new("cubic meters"),
                                    new("megagallons"),
                                    new("thousand gallons"),
                                    new("million gallons"),
                                    new("gallons")                                    
                                }
                            },
                            new()
                            {
                                Name = "PerSecondValueMeans",                                
                                CategoryName = "PerValue",
                                Text = [new("per second")],
                                Position = LabelPosition.RelatedCategoryPosition,
                                RelatedCategoryName = "PerUnits",
                                RelatedName = "PerSecondUnits",                                
                                Format = "Number",
                                PreviousLinesToFetch = 1,
                                NextLinesToFetch = 1,
                                Remove =
                                [
                                    new("3.1") { ExceptWhenInsideWord = true },
                                    new("3.2") { ExceptWhenInsideWord = true },
                                    new("3.3") { ExceptWhenInsideWord = true },
                                    new("3.4") { ExceptWhenInsideWord = true },
                                ]
                            },
                            new()
                            {
                                Name = "TextWithoutNumber",
                                Remove = [
                                    new("3.1") { ColumnMustStartWith = true },
                                    new("3.2") { ColumnMustStartWith = true },
                                    new("3.3") { ColumnMustStartWith = true },
                                    new("3.4") { ColumnMustStartWith = true }
                                ],
                                MultipleMatchBehaviour = MultipleMatchBehaviour.FindSingleInstanceOfLabelWithASingleValueButMultipleLines,
                                Position = LabelPosition.ApplicableToMost,
                                Format = "Text",
                                PreviousLinesToFetch = 0,
                                NextLinesToFetch = 0
                            },
                            new()
                            {
                                Name = "MeanPointTable",
                                Position =  LabelPosition.TextToFindIsBetweenLabels,
                                TextStart = [
                                    new("Abstraction Point Depth (metres) Diameter (millimetres)"),
                                    new("Abstraction Point Depth")
                                ],
                                TextEnd = [
                                    new("4.[END_OF_LINE]"),
                                    new("[END_OF_BLOCK]")
                                ],
                                Remove = [
                                    new("Abstraction Point Depth (metres) Diameter (millimetres)")
                                ],
                                PreviousLinesToFetch = 0,
                                NextLinesToFetch = 10
                            }
                        ]
                    }
                ]
            }
        ];
    }

    private static List<LabelToMatch> GetAbstractionLimitsLabels()
    {
        // TODO Verify the purposes against the NALD data
        
        return
        [
            new LabelToMatch
            {
                Name = "DocumentAbstractionLimitsSection",
                TextStart =
                [
                    new("MAXIMUM QUANTITY OF WATER TO BE ABSTRACTED DURING THE SPECIFIED PERIOD(S)") { IfMultiplePreferLongest = true },
                    new("MAXIMUM QUANTITY OF WATER TO BE ABSTRACTED DURING THE SPECIFIED PERIODS") { IfMultiplePreferLongest = true },
                    new("MAXIMUM QUANTITY OF WATER TO BE ABSTRACTED DURING THE SPECIFIED PERIOD") { IfMultiplePreferLongest = true },                    
                    new("MAXIMUM QUANTITY OF WATER TO BE ABSTRACTED DURING THE") { IfMultiplePreferLongest = true },
                    new("MAXIMUM QUANTITY OF WATER TO BE ABSTRACTED DURING") { IfMultiplePreferLongest = true },                   
                    new("MAXIMUM QUANTITY OF WATER TO BE ABSTRACTED") { IfMultiplePreferLongest = true },
                    new("MAXIMUM QUANTITIES") { ColumnMustStartWith = true },
                    new("Quantity(ies) of Water Authorised to be Abstracted During a Period or Periods Specified"),                    
                    new("Quantity(ies) of water authorised to be abstracted during a period"),
                    new("QUANTITY OF WATER AUTHORISED TO BE ABSTRACTED NOT EXCEEDING"),
                    new("QUANTITY OF WATER AUTHORISED TO BE ABSTRACTED DURING THE PERIOD"),
                    new("G. QUANTITY OF WATER AUTHORISED TO BE"), // TODO hack
                    new("QUANTITY OF WATER TO BE ABSTRACTED DURING THE SPECIFIED"),
                    new("QUANTITY OF WATER AUTHORISED TO BE ABSTRACTED[END_OF_LINE]") { ColumnMustStartWith = true },
                    new("The quantity of water authorised to be abstracted shall be") { IfMultiplePreferLast = true }
                ],
                TextEnd =
                [
                    new("7. ") { LineMustStartWith = true },
                    new("MEANS OF MEASUREMENT OR ASSESSMENT OF WATER ABSTRACTED"),
                    new("MEANS OF MEASUREMENT OR ASSESSMENT OF WATER"), //" ABSTRACTED", -- Its cut off this way in a document, over 2 pages
                    new("MEANS OF MEASUREMENT OF WATER ABSTRACTED"),
                    new("Authorised means of abstraction"),
                    new("MEANS OF ABSTRACTION"),
                    new("MEANS TO BE USED FOR MEASURING"),
                    new("PERIOD(s) DURING WHICH WATER IS AUTHORIZED TO BE USED"),
                    new("Means of measurement or assessment"),
                    new("8. MEANS OF ASSESSMENT OF WATER ABSTRACTED"),
                    //new("5. ") { LineMustStartWith = true },
                    new("CONDITIONS[END_OF_LINE]") { LineMustStartWith = true},
                    new("[END_OF_BLOCK]")
                ],
                MustContain =
                [
                    "cubic metres",
                    "cubic meters", // Some files have this US spelling
                    " m per", // This is wrong but its how it gets read in some files
                    "m\u00b3", // m3
                    "gallons",
                    "litres"
                ],
                Remove =
                [
                    PageNumberPattern,
                    LicenceNumberInHeaderPattern
                ],
                CanGoOverPageBoundary = true,
                Position = LabelPosition.TextToFindIsBetweenLabels,
                MultipleMatchBehaviour = MultipleMatchBehaviour.FindSingleInstanceOfLabelWithMultipleValues,
                MultipleServiceMatchBehaviour =
                    MultipleServiceMatchBehaviour.UseMostSubResultsUseLastServiceResultIfEqual,
                PreviousLinesToFetch = 3,
                NextLinesToFetch = 200,
                MinimumSubMatches = 1,
                IncludeStartLabelText = true,
                SubLabels = new List<LabelToMatch>
                {
                    new()
                    {
                        Name = "AbstractionLimitPoint",
                        TextStart = [
                            new("6.10") { LineMustStartWith = true },
                            new("6.1 0") { LineMustStartWith = true }, // TODO should fix underlying cause                            
                            new("6.1") { LineMustStartWith = true },
                            new("6.2") { LineMustStartWith = true },
                            new("6.3") { LineMustStartWith = true },
                            new("6.4") { LineMustStartWith = true },
                            new("6.5") { LineMustStartWith = true },
                            new("6.6") { LineMustStartWith = true },
                            new("6.7") { LineMustStartWith = true },
                            new("6.8") { LineMustStartWith = true },
                            new("6.9") { LineMustStartWith = true },
                            new("From borehole (1)") { LineMustStartWith = true }, // Specificity matters here else you can start and being on same line (e.g. between text starts 'From borehole' and ends straight away with '(1)')
                            new("From borehole (2)") { LineMustStartWith = true },
                            new("From borehole") { LineMustStartWith = true },
                            new("(1)") { LineMustStartWith = true },
                            new("(2)") { LineMustStartWith = true },
                            new("(3)") { LineMustStartWith = true },
                            new("(4)") { LineMustStartWith = true },
                            new("*For Purpose") { LineMustStartWith = true },
                            new("The aggregate quantity of water authorised to be abstracted under this licence shall not") { ColumnMustStartWith = true },
                            new("[START_OF_BLOCK]")
                        ],
                        TextEnd = [
                            new("6.2") { LineMustStartWith = true },
                            new("6.3") { LineMustStartWith = true },
                            new("6.4") { LineMustStartWith = true },
                            new("6.5") { LineMustStartWith = true },
                            new("6.6") { LineMustStartWith = true },
                            new("6.7") { LineMustStartWith = true },
                            new("6.8") { LineMustStartWith = true },
                            new("6.9") { LineMustStartWith = true },
                            new("6.10") { LineMustStartWith = true },
                            new("6.1 0") { LineMustStartWith = true }, // TODO should fix underlying cause
                            new("From borehole (2)") { LineMustStartWith = true },
                            new("From borehole") { LineMustStartWith = true },
                            new("(1)") { LineMustStartWith = true},
                            new("(2)") { LineMustStartWith = true},
                            new("(3)") { LineMustStartWith = true},
                            new("(4)") { LineMustStartWith = true},
                            new("(5)") { LineMustStartWith = true},
                            new("*For Purpose") { LineMustStartWith = true },
                            new("*In aggregate") { LineMustStartWith = true },
                            new("The aggregate quantity of water authorised to be abstracted under this licence shall not") { ColumnMustStartWith = true },
                            new("[END_OF_BLOCK]")
                        ],
                        Remove = [
                            new("MAXIMUM QUANTITY OF WATER TO BE ABSTRACTED DURING THE SPECIFIED PERIOD(S)"),
                            new("MAXIMUM QUANTITY OF WATER TO BE ABSTRACTED DURING THE SPECIFIED PERIODS"),
                            new("MAXIMUM QUANTITY OF WATER TO BE ABSTRACTED DURING THE SPECIFIED PERIOD"),
                            new("MAXIMUM QUANTITY OF WATER TO BE ABSTRACTED DURING THE SPECIFIED"),
                            new("MAXIMUM QUANTITY OF WATER TO BE ABSTRACTED DURING THE"),
                            new("MAXIMUM QUANTITY OF WATER TO BE ABSTRACTED DURING"),
                            new("MAXIMUM QUANTITY OF WATER TO BE ABSTRACTED"),
                            new("SPECIFIED PERIOD") // TODO going to have to specify this starts and finishes a line

                        ],
                        RemoveStartOfBlockSectionsWhenMultiple = false,
                        DeDuplicateResults = true,
                        IncludeStartLabelText = true,
                        Position = LabelPosition.TextToFindIsBetweenLabels,
                        Format = "Text",
                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithMultipleValuesPerLabel,
                        PreviousLinesToFetch = 3,
                        NextLinesToFetch = 20,
                        MinimumSubMatches = 1,
                        SubLabels = GetLimitLineSubLabels(6)
                    }
                }
            }
        ];
    }

    private static List<LabelToMatch> GetLimitLineSubLabels(int? documentIdentifierPrefix)
    {
        return
        [
            new()
            {
                Name = "AbstractionLimitPointSub",
                Text = [
                    new("and licence"),
                    new("so that no more")                    
                ],
                Position = LabelPosition.SplitAtLabel,
                MultipleMatchBehaviour = MultipleMatchBehaviour.FindSingleInstanceOfLabelWithMultipleValues,
                PreviousLinesToFetch = 20,
                MinimumSubMatches = 2,
                IncludeStartLabelText = true,
                DoNotTrimLines = true,
                SubLabels = new List<LabelToMatch>
                {
                    new()
                    {
                        Name = "DocumentIdentifier",
                        Possibilities = documentIdentifierPrefix != null ? [
                            new($"{documentIdentifierPrefix}.1"),
                            new($"{documentIdentifierPrefix}.2"),
                            new($"{documentIdentifierPrefix}.3"),
                            new($"{documentIdentifierPrefix}.4"),
                            new($"{documentIdentifierPrefix}.5"),
                            new($"{documentIdentifierPrefix}.6"),
                            new($"{documentIdentifierPrefix}.7"),
                            new($"{documentIdentifierPrefix}.8"),
                            new("1. ") { LineMustStartWith = true },
                            new("2. ") { LineMustStartWith = true },
                            new("3. ") { LineMustStartWith = true },
                            new("4. ") { LineMustStartWith = true },
                            new("5. ") { LineMustStartWith = true },
                            new("6. ") { LineMustStartWith = true },
                            new("7. ") { LineMustStartWith = true },
                            new("8. ") { LineMustStartWith = true },
                            new("9. ") { LineMustStartWith = true }
                        ] : [],
                        Position = LabelPosition.ApplicableToMost,
                        Format = "Number",
                        PreviousLinesToFetch = 0,
                        NextLinesToFetch = 0
                    },
                    new()
                    {
                        Name = "DateOnly",
                        Text =
                        [
                            new("Up to and including "),
                            new("From "),
                            new("Until "),                            
                            new("aggregate quantity of water authorised")
                        ],
                        IgnoreBlockIfContains =
                        [
                            "Note:"
                        ],
                        Remove = [
                            new ($"{documentIdentifierPrefix}.1"),
                            new ($"{documentIdentifierPrefix}.2"),
                            new ($"{documentIdentifierPrefix}.3"),
                            new ($"{documentIdentifierPrefix}.4"),
                            new ($"{documentIdentifierPrefix}.5"),
                            new ($"{documentIdentifierPrefix}.6"),
                            new ($"{documentIdentifierPrefix}.7"),
                            new ($"{documentIdentifierPrefix}.8")
                        ],
                        Position = LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore,
                        Format = "Date",
                        IncludeStartLabelText = true,
                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindSingleInstanceOfLabelWithMultipleValues
                    },
                    new()
                    {
                        Name = "DatePurposeRough",
                        Format = "Text",
                        TextStart =
                        [
                            new("January") { LineMustStartWith = true },
                            new("February") { LineMustStartWith = true },
                            new("March") { LineMustStartWith = true },
                            new("April") { LineMustStartWith = true },
                            new("May") { LineMustStartWith = true },
                            new("June") { LineMustStartWith = true },
                            new("July") { LineMustStartWith = true },
                            new("August") { LineMustStartWith = true },
                            new("September") { LineMustStartWith = true },
                            new("October") { LineMustStartWith = true },
                            new("November") { LineMustStartWith = true }
                        ],
                        TextEnd =
                        [
                            new("February[END_OF_COLUMN]"),
                            new("March[END_OF_COLUMN]"),
                            new("April[END_OF_COLUMN]"),
                            new("May[END_OF_COLUMN]"),
                            new("June[END_OF_COLUMN]"),
                            new("July[END_OF_COLUMN]"),
                            new("August[END_OF_COLUMN]"),
                            new("September[END_OF_COLUMN]"),
                            new("October[END_OF_COLUMN]"),
                            new("November[END_OF_COLUMN]"),
                            new("December[END_OF_COLUMN]")
                        ],
                        Remove = [
                            new ($"{documentIdentifierPrefix}.1"),
                            new ($"{documentIdentifierPrefix}.2"),
                            new ($"{documentIdentifierPrefix}.3"),
                            new ($"{documentIdentifierPrefix}.4"),
                            new ($"{documentIdentifierPrefix}.5"),
                            new ($"{documentIdentifierPrefix}.6"),
                            new ($"{documentIdentifierPrefix}.7"),
                            new ($"{documentIdentifierPrefix}.8")
                        ],
                        PreviousLinesToFetch = 0,
                        NextLinesToFetch = 0,
                        Position = LabelPosition.TextToFindIsBetweenLabels,
                        IncludeStartLabelText = true,
                        IncludeEndLabelText = true,
                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithMultipleValuesPerLabel
                    },
                    new()
                    {
                        Name = "PurposeCondition",
                        Text =
                        [
                            new("condition "),
                            new("conditions "),
                            new("purposes specified in "),
                            new("purposes of "),
                            new("purpose of "),
                            new("for purpose ")
                        ],
                        TextEnd =
                        [
                            new("shall not exceed"),
                            new(":")
                        ],
                        Position = LabelPosition.TextToFindIsBetweenLabels,
                        Format = "Text",
                        Remove =
                        [
                            new("(above)"),
                            new("numbers"),
                            new("conditions"),
                            new("condition"),
                            new("for purpose"),
                            new("purpose")
                        ],
                        MustContain =
                        [
                            "4.1",
                            "4.2",
                            "4.3",
                            "4.4",
                            "4.5",
                            "4.6",
                            "4.7",
                            "4.8",
                            "4.9",
                            "(1)",
                            "(2)",
                            "(3)",
                            "(4)",
                            "(a)",
                            "(b)",
                            "(c)",
                            "(d)",
                            "spray irrigation", // TODO add more types here
                            "trickle irrigation",
                            "mineral washing",
                            "groundwater augmentation"
                        ],
                        SubLabels =
                        [
                            new()
                            {
                                Name = "PurposeConditionSub",
                                Text = [new("and ")],
                                Position = LabelPosition.SplitAtLabel,
                                MultipleMatchBehaviour = MultipleMatchBehaviour.FindSingleInstanceOfLabelWithMultipleValues
                            }
                        ]
                    },
                    new()
                    {
                        Name = "PurposeConditionSingleLine",
                        Text =
                        [
                            new("*For Purpose ")
                            {
                                LineMustStartWith = true
                            },
                            new("For Purpose ")
                            {
                                LineMustStartWith = true
                            }
                        ],
                        TextEnd =
                        [
                            new(PositionConstants.EndOfLineMarker)
                        ],
                        Position = LabelPosition.TextToFindIsBetweenLabels,
                        Format = "Text",
                        PreviousLinesToFetch = 0,
                        NextLinesToFetch = 0,
                        Possibilities = 
                        [
                            new("(a)"),
                            new("(b)"),
                            new("(c)"),
                            new("(d)")
                        ],
                        SubLabels =
                        [
                            new()
                            {
                                Name = "PurposeConditionSingleLineSub",
                                Text = [new("and ")],
                                Position = LabelPosition.SplitAtLabel,
                                MultipleMatchBehaviour = MultipleMatchBehaviour.FindSingleInstanceOfLabelWithMultipleValues
                            }
                        ]
                    },
                    new()
                    {
                        Name = "PointCondition",
                        Text =
                        [
                            new("Abstraction Point A"),
                            new("Abstraction Point B"),
                            new("Abstraction Point C"),
                            new("Abstraction Point D"),
                            new("Abstraction Point E"),
                            new("Abstraction Point F"),
                            new("Abstraction Point 'A'"),
                            new("Abstraction Point 'B'"),
                            new("Abstraction Point 'C'"),
                            new("Abstraction Point 'D'"),
                            new("Abstraction Point 'E'"),
                            new("Abstraction Point 'F'"),
                            new("condition "),
                            new("conditions "),
                            new("(1)"),
                            new("(2)"),
                            new("(3)"),
                            new("(4)")
                        ],
                        TextEnd =
                        [
                            new("Abstraction Point B"),
                            new("Abstraction Point C"),
                            new("Abstraction Point D"),
                            new("Abstraction Point E"),
                            new("Abstraction Point F"),
                            new("Abstraction Point G"),
                            new("Abstraction Point 'B'"),
                            new("Abstraction Point 'C'"),
                            new("Abstraction Point 'D'"),
                            new("Abstraction Point 'E'"),
                            new("Abstraction Point 'F'"),
                            new("Abstraction Point 'G'"),
                            new("shall not exceed"),
                            new(":"),
                            new("(2)"),
                            new("(3)"),
                            new("(4)"),
                            new("[END_OF_BLOCK]")
                        ],
                        Position = LabelPosition.TextToFindIsBetweenLabels,
                        IncludeStartLabelText = true,
                        Format = "Text",
                        Possibilities =
                        [
                            new("Abstraction Point A"),
                            new("Abstraction Point B"),
                            new("Abstraction Point C"),
                            new("Abstraction Point D"),
                            new("Abstraction Point E"),
                            new("Abstraction Point F"),
                            new("Abstraction Point 'A'"),
                            new("Abstraction Point 'B'"),
                            new("Abstraction Point 'C'"),
                            new("Abstraction Point 'D'"),
                            new("Abstraction Point 'E'"),
                            new("Abstraction Point 'F'"),
                            new("2.1"),
                            new("2.2"),
                            new("2.3"),
                            new("2.4"),
                            new("2.5"),
                            new("2.6"),
                            new("2.7"),
                            new("2.8"),
                            new("2.9"),
                            new("(1)"),
                            new("(2)"),
                            new("(3)"),
                            new("(4)")
                        ],
                        MustContain =
                        [
                            "Abstraction Point A",
                            "Abstraction Point B",
                            "Abstraction Point C",
                            "Abstraction Point D",
                            "Abstraction Point E",
                            "Abstraction Point F",
                            "Abstraction Point 'A'",
                            "Abstraction Point 'B'",
                            "Abstraction Point 'C'",
                            "Abstraction Point 'D'",
                            "Abstraction Point 'E'",
                            "Abstraction Point 'F'",                            
                            "2.1",
                            "2.2",
                            "2.3",
                            "2.4",
                            "2.5",
                            "2.6",
                            "2.7",
                            "2.8",
                            "2.9",
                            "(1)",
                            "(2)",
                            "(3)",
                            "(4)"
                        ],
                        Remove =
                        [
                            new("number ")
                        ],
                        SubLabels =
                        [
                            new()
                            {
                                Name = "PointConditionSub",
                                Text = [new("and ")],
                                Position = LabelPosition.SplitAtLabel,
                                MultipleMatchBehaviour = MultipleMatchBehaviour.FindSingleInstanceOfLabelWithMultipleValues
                            }
                        ]
                    },
                    new()
                    {
                        Name = "PurposeCondition",
                        Text =
                        [
                            new("(1)"),
                            new("(2)"),
                            new("(3)"),
                            new("(4)")
                        ],
                        TextEnd =
                        [
                            new("(2)"),
                            new("(3)"),
                            new("(4)"),
                            new("[END_OF_BLOCK]")
                        ],
                        Position = LabelPosition.TextToFindIsBetweenLabels,
                        IncludeStartLabelText = true,
                        Format = "Text",
                        Possibilities =
                        [
                            new("(1)"),
                            new("(2)"),
                            new("(3)"),
                            new("(4)")
                        ],
                        MustContain =
                        [
                            "(1)",
                            "(2)",
                            "(3)",
                            "(4)"
                        ],
                        SubLabels =
                        [
                            new()
                            {
                                Name = "PurposeConditionSub",
                                Text = [new("and ")],
                                Position = LabelPosition.SplitAtLabel,
                                MultipleMatchBehaviour = MultipleMatchBehaviour.FindSingleInstanceOfLabelWithMultipleValues
                            }
                        ]
                    },
                    new()
                    {
                        Name = "PointConditionSingleLine",
                        Text =
                        [
                            new("From borehole ")
                            {
                                LineMustStartWith = true
                            }
                        ],
                        TextEnd =
                        [
                            new(PositionConstants.EndOfLineMarker)
                        ],
                        Position = LabelPosition.TextToFindIsBetweenLabels,
                        Format = "Text",
                        PreviousLinesToFetch = 0,
                        NextLinesToFetch = 0,
                        Possibilities =
                        [
                            new("(1)"),
                            new("(2)"),
                            new("(3)"),
                            new("(4)")
                        ],
                        SubLabels =
                        [
                            new()
                            {
                                Name = "PointConditionSingleLineSub",
                                Text = [new("and ")],
                                Position = LabelPosition.SplitAtLabel,
                                MultipleMatchBehaviour = MultipleMatchBehaviour.FindSingleInstanceOfLabelWithMultipleValues
                            }
                        ]
                    },                    
                    GetLinkedLicenceNumber("LinkedLicenceNumber"),
                    new()
                    {
                        Name = "LinkedLicenceFilename",
                        Text =
                        [
                            new("licence number "),
                            new("licence serial number "),
                            new("licence serial numbers "),
                            new("under this licence and licence"),
                            new("and licence "),
                            new("and under licence "),
                            new("and under license ") // spelling mistake in licence                                    
                        ],
                        Position = LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore,
                        Format = "LicenceNumberFilename"
                    },
                    new()
                    {
                        Name = "LinkedLicence",
                        RelatedName = "LinkedLicenceNumber",
                        Format = LinkedLicenceDontInline.Constant
                    },
                    new()
                    {
                        Name = "PerHourUnits",
                        CategoryName = "PerUnits",
                        Text = [new("per hour")],
                        Position = LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter,
                        Format = "Units",
                        PreviousLinesToFetch = 1,
                        NextLinesToFetch = 1,
                        Possibilities = new List<TextToMatch>
                        {
                            new("megalitres"),
                            new("litres"),
                            new("thousand cubic metres"),
                            new("cubic metres"),
                            new("cubic meters"),
                            new("cubic metre"),
                            new("cubic meter"),
                            new("m\u00b3"), // m3
                            new("megagallons"),
                            new("thousand gallons"),
                            new("million gallons"),
                            new("gallons")
                        },
                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel,
                        FindMultipleOnSingleLine = true
                    },
                    new()
                    {
                        Name = "PerDayUnits",
                        CategoryName = "PerUnits",
                        Text = [new("per day")],
                        Position = LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter,
                        Format = "Units",
                        PreviousLinesToFetch = 1,
                        NextLinesToFetch = 1,
                        Possibilities = new List<TextToMatch>
                        {
                            // This is actually (unintentionally) the order of preference when on the same line
                            new("megalitres"),
                            new("litres"),
                            new("thousand cubic metres"),
                            new("cubic metres"),
                            new("cubic meters"),
                            new("cubic metre"),
                            new("cubic meter"),
                            new("m\u00b3"), // m3
                            new("megagallons"),
                            new("thousand gallons"),
                            new( "million gallons"),
                            new("gallons")
                        },
                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel,
                        FindMultipleOnSingleLine = true
                    },
                    new()
                    {
                        Name = "PerMonthUnits",
                        CategoryName = "PerUnits",
                        Text = [new("per month")],
                        Position = LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter,
                        Format = "Units",
                        PreviousLinesToFetch = 1,
                        NextLinesToFetch = 1,
                        Possibilities = new List<TextToMatch>
                        {
                            new("megalitres"),
                            new("litres"),
                            new("thousand cubic metres"),
                            new("cubic metres"),
                            new("cubic meters"),
                            new("cubic metre"),
                            new("cubic meter"),
                            new("m\u00b3"), // m3
                            new("megagallons"),
                            new("thousand gallons"),
                            new("million gallons"),
                            new("gallons")
                        },
                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel,
                        FindMultipleOnSingleLine = true
                    },
                    new()
                    {
                        Name = "PerYearUnits",
                        CategoryName = "PerUnits",
                        Text =
                        [
                            new("per year"),
                            new("per annum")
                        ],
                        Position = LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter,
                        Format = "Units",
                        PreviousLinesToFetch = 1,
                        NextLinesToFetch = 1,
                        Possibilities = new List<TextToMatch>
                        {
                            new("megalitres"),
                            new("litres"),
                            new("thousand cubic metres"),
                            new("cubic metres"),
                            new("cubic meters"),
                            new("cubic metre"),
                            new("cubic meter"),
                            new("m\u00b3"), // m3
                            new("megagallons"),
                            new("thousand gallons"),
                            new("million gallons"),
                            new("gallons")
                        },
                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel,
                        FindMultipleOnSingleLine = true
                    },
                    new()
                    {
                        Name = "Per5YearUnits",
                        CategoryName = "PerUnits",
                        Text =
                        [
                            new("consecutive five year"),
                            new("five consecutive years"),
                            new("over any 5-year period")
                        ],
                        Position = LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter,
                        Format = "Units",
                        PreviousLinesToFetch = 1,
                        NextLinesToFetch = 1,
                        Possibilities = new List<TextToMatch>
                        {
                            new("megalitres"),
                            new("litres"),
                            new("thousand cubic metres"),
                            new("cubic metres"),
                            new("cubic meters"),
                            new("cubic metre"),
                            new("cubic meter"),
                            new("m\u00b3"), // m3
                            new("megagallons"),
                            new("thousand gallons"),
                            new("million gallons"),
                            new("gallons")
                        },
                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel,
                        FindMultipleOnSingleLine = true
                    },                    
                    new()
                    {
                        Name = "PerSecondUnits",
                        CategoryName = "PerUnits",
                        Text = [new("per second")],
                        Position = LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter,
                        Format = "Units",
                        PreviousLinesToFetch = 1,
                        NextLinesToFetch = 1,
                        Possibilities = new List<TextToMatch>
                        {
                            new("megalitres"),
                            new("litres"),
                            new("thousand cubic metres"),
                            new("cubic metres"),
                            new("cubic meters"),
                            new("cubic metre"),
                            new("cubic meter"),
                            new("m\u00b3"), // m3
                            new("megagallons"),
                            new("thousand gallons"),
                            new("million gallons"),
                            new("gallons")
                        },
                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel,
                        FindMultipleOnSingleLine = true
                    },
                    new()
                    {
                        Name = "InTotalUnits",
                        CategoryName = "PerUnits",
                        Text = [new("in total")],
                        Position = LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter,
                        Format = "Units",
                        PreviousLinesToFetch = 1,
                        NextLinesToFetch = 1,
                        Possibilities = new List<TextToMatch>
                        {
                            new("megalitres"),
                            new("litres"),
                            new("thousand cubic metres"),
                            new("cubic metres"),
                            new("cubic meters"),
                            new("cubic metre"),
                            new("cubic meter"),
                            new("m\u00b3"), // m3
                            new("megagallons"),
                            new("thousand gallons"),
                            new("million gallons"),
                            new("gallons")
                        },
                        SkipLineWhenContains =
                        [
                            "abstracted in total"
                        ],
                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel,
                        FindMultipleOnSingleLine = true
                    },
                    new()
                    {
                        Name = "PerHourValue",
                        CategoryName = "PerValue",
                        Text = [new("per hour")],
                        Position = LabelPosition.RelatedCategoryPosition,
                        RelatedCategoryName = "PerUnits",
                        RelatedName = "PerHourUnits",
                        Format = "Number",
                        IgnoreMatchIfContains =
                        [
                            "(1)",
                            "(11)",
                            "(111)"
                        ],
                        Remove =
                        [
                            new("6.1") { ExceptWhenInsideWord = true },
                            new("6.2") { ExceptWhenInsideWord = true },
                            new("6.3") { ExceptWhenInsideWord = true },
                            new("1 ")
                            {
                                LineMustStartWith = true,
                                ColumnMustHave2SequentialNumbers = true
                            },
                            new("2 ")
                            {
                                LineMustStartWith = true,
                                ColumnMustHave2SequentialNumbers = true
                            },
                            new("(1)"),
                            new("(2)"),
                            new("(3)"),
                            new("(4)")
                        ],
                        PreviousLinesToFetch = 1,
                        NextLinesToFetch = 1,
                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel,
                        FindMultipleOnSingleLine = true
                    },
                    new()
                    {
                        Name = "PerDayValue",
                        CategoryName = "PerValue",
                        Text = [new("per day")],
                        Position = LabelPosition.RelatedCategoryPosition,
                        RelatedCategoryName = "PerUnits",
                        RelatedName = "PerDayUnits",
                        Format = "Number",
                        IgnoreMatchIfContains =
                        [
                            "(1)",
                            "(11)",
                            "(111)"
                        ],
                        Remove =
                        [
                            new("6.1") { ExceptWhenInsideWord = true },
                            new("6.2") { ExceptWhenInsideWord = true },
                            new("6.3") { ExceptWhenInsideWord = true },
                            new("(1)"),
                            new("(2)"),
                            new("(3)"),
                            new("(4)")
                        ],
                        PreviousLinesToFetch = 1,
                        NextLinesToFetch = 1,
                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel,
                        FindMultipleOnSingleLine = true
                    },
                    new()
                    {
                        Name = "PerMonthValue",
                        CategoryName = "PerValue",
                        Text = [new("per month")],
                        Position = LabelPosition.RelatedCategoryPosition,
                        RelatedCategoryName = "PerUnits",
                        RelatedName = "PerMonthUnits",
                        Format = "Number",
                        Remove =
                        [
                            new("6.1") { ExceptWhenInsideWord = true },
                            new("6.2") { ExceptWhenInsideWord = true },
                            new("6.3") { ExceptWhenInsideWord = true },
                            new("(1)"),
                            new("(2)"),
                            new("(3)"),
                            new("(4)")
                        ],
                        IgnoreMatchIfContains =
                        [
                            "(1)",
                            "(11)",
                            "(111)"
                        ],
                        PreviousLinesToFetch = 1,
                        NextLinesToFetch = 1,
                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel,
                        FindMultipleOnSingleLine = true
                    },
                    new()
                    {
                        Name = "PerYearValue",
                        CategoryName = "PerValue",
                        Text =
                        [
                            new("per year"),
                            new("per annum")
                        ],
                        Position = LabelPosition.RelatedCategoryPosition,
                        RelatedCategoryName = "PerUnits",
                        RelatedName = "PerYearUnits",
                        Format = "Number",
                        Remove =
                        [
                            new("6.1") { ExceptWhenInsideWord = true },
                            new("6.2") { ExceptWhenInsideWord = true },
                            new("6.3") { ExceptWhenInsideWord = true },
                            new("(1)"),
                            new("(2)"),
                            new("(3)"),
                            new("(4)")
                        ],
                        IgnoreMatchIfContains =
                        [
                            "(1)",
                            "(11)",
                            "(111)"
                        ],
                        PreviousLinesToFetch = 1,
                        NextLinesToFetch = 1,
                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel,
                        FindMultipleOnSingleLine = true
                    },
                    new()
                    {
                        Name = "Per5YearValue",
                        CategoryName = "PerValue",
                        Text =
                        [
                            new("consecutive five year"),
                            new("five consecutive years"),
                            new("over any 5-year period")
                        ],
                        Position = LabelPosition.RelatedCategoryPosition,
                        RelatedCategoryName = "PerUnits",
                        RelatedName = "Per5YearUnits",
                        Format = "Number",
                        Remove =
                        [
                            new("6.1") { ExceptWhenInsideWord = true },
                            new("6.2") { ExceptWhenInsideWord = true },
                            new("6.3") { ExceptWhenInsideWord = true },
                            new("(1)"),
                            new("(2)"),
                            new("(3)"),
                            new("(4)")
                        ],
                        IgnoreMatchIfContains =
                        [
                            "(1)",
                            "(11)",
                            "(111)"
                        ],
                        PreviousLinesToFetch = 1,
                        NextLinesToFetch = 1,
                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel,
                        FindMultipleOnSingleLine = true
                    },
                    new()
                    {
                        Name = "PerSecondValue",
                        CategoryName = "PerValue",
                        Text = [new("per second")],
                        Position = LabelPosition.RelatedCategoryPosition,
                        RelatedCategoryName = "PerUnits",
                        RelatedName = "PerSecondUnits",
                        Format = "Number",
                        Remove =
                        [
                            new("6.1") { ExceptWhenInsideWord = true },
                            new("6.2") { ExceptWhenInsideWord = true },
                            new("6.3") { ExceptWhenInsideWord = true },
                            new("(1)"),
                            new("(2)"),
                            new("(3)"),
                            new("(4)")
                        ],
                        IgnoreMatchIfContains =
                        [
                            "(1)",
                            "(11)",
                            "(111)"
                        ],
                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel,
                        FindMultipleOnSingleLine = true,
                        PreviousLinesToFetch = 1
                        // Not setting NextLinesToFetch to 1 as it breaks some existing tests
                    },
                    new()
                    {
                        Name = "InTotalValue",
                        CategoryName = "PerValue",
                        Text = [
                            new("in total"),
                            new("total annual quantity")
                        ],
                        Position = LabelPosition.RelatedCategoryPosition,
                        RelatedCategoryName = "PerUnits",
                        RelatedName = "InTotalUnits",
                        Format = "Number", // TODO add date extraction,
                        SkipLineWhenContains =
                        [
                            "abstracted in total"
                        ],
                        Remove =
                        [
                            new("6.1") { ExceptWhenInsideWord = true },
                            new("6.2") { ExceptWhenInsideWord = true },
                            new("6.3") { ExceptWhenInsideWord = true },
                            new("(1)"),
                            new("(2)"),
                            new("(3)"),
                            new("(4)")
                        ],
                        IgnoreMatchIfContains =
                        [
                            "(1)",
                            "(11)",
                            "(111)"
                        ],
                        PreviousLinesToFetch = 1,
                        NextLinesToFetch = 3,
                        MultipleMatchBehaviour = MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel,
                        FindMultipleOnSingleLine = true
                    },
                    new()
                    {
                        Name = "AYearDefinitionLine",
                        TextStart = [new("beginning on")],
                        TextEnd = [new(".")],
                        Position = LabelPosition.TextToFindIsBetweenLabels,
                        PreviousLinesToFetch = 0,
                        NextLinesToFetch = 1,
                        Format = "Text",
                        SubLabels =
                        [
                            new()
                            {
                                Name = "AYearDates",
                                Position = LabelPosition.SplitAtLabel,
                                Text = [new("and")],
                                Remove = [new("ending on")],
                                Format = "DateOrPurpose",
                                MultipleMatchBehaviour = MultipleMatchBehaviour.FindSingleInstanceOfLabelWithMultipleValues
                            }
                        ]
                    },
                    new()
                    {
                        Name = "LimitPointTable",
                        Position =  LabelPosition.TextToFindIsBetweenLabels,
                        TextStart = [
                            new("Abstraction Hourly Daily quantity Yearly quantity Instantaneous rate"),
                            new("Abstraction Point Hourly")
                        ],
                        TextEnd = [
                            new("6.2"),
                            new("[END_OF_BLOCK]")
                        ],
                        Remove = [
                            new("Point quantity (cubic metres) (cubic metres) not exceeding (litres"),
                            new("(cubic per second)"),
                            new("metres)")
                        ],
                        PreviousLinesToFetch = 0,
                        NextLinesToFetch = 10
                    }
                }
            }
        ];
    }
    
    private static LabelToMatch GetLinkedLicenceNumber(string labelName)
    {
        return new LabelToMatch
        {
            Name = labelName,
            Text =
            [
                new(AbstractionLicenceNumber.YorkshireRegexPatten)
                {
                    Regex = AbstractionLicenceNumber.LicenceNumbersRegex()
                }
            ],
            Format = LicenceNumber.Constant,
            Position = LabelPosition.LabelIsActuallyResult,
            PreviousLinesToFetch = 1,
            NextLinesToFetch = 1,
            MultipleMatchBehaviour = MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel,
            MultipleServiceMatchBehaviour = MultipleServiceMatchBehaviour.UseAllUnique,
            SkipLineNumbers = [0],
            Remove =
            [
                PageNumberPattern,
                EnvironmentAgencyTelephone1Pattern,
                EnvironmentAgencyTelephone2Pattern,
                EnvironmentAgencyTelephone3Pattern,
                EnvironmentAgencyTelephone4Pattern,
                new("condition 9.2.1"),
                new("9.2.1") { ColumnMustStartWith = true },
                new("condition 9.2.2"),
                new("9.2.2") { ColumnMustStartWith = true },
                new("0 0 0 0"), // Don't understand what this means, but it appears in some map
                new("2 8 2 8"), // Don't understand what this means, but it appears in some map
                new("4 2 4 2"), // Don't understand what this means, but it appears in some map
                new("7 0 7 0"), // Don't understand what this means, but it appears in some map
                new("0 250 500"), // Doubling scale
                new("0 250 500 1"), // Doubling scale
                new("0 125 250 M"), // Doubling scale
                new("0 125 250"), // Doubling scale
                new("0 170 340 M"), // Doubling scale
                new("0 170 340"), // Doubling scale
                new("0 150 300 M"), // Doubling scale
                new("0 150 300"), // Doubling scale
                new("0 425 850 M"), // Doubling scale
                new("0 425 850"), // Doubling scale
                new("0 100 200") // Doubling scale
            ],
            SkipLineWhenContains = NoneLicenceNumberSkips
        };
    }

    private static readonly string[] NoneLicenceNumberSkips =
    [
        LicenceNumberHeaderLine,
        "discharge permit",
        "discharge number",
        "discharge consent",
        "drawing no.",
        "Date of Issue",
        "Date effective",
        "Date of expiry",
        "Date of original issue"
    ];
    
    private const string LicenceNumberHeaderLine = "Licence Serial No: ";
    private static readonly TextToMatch PageNumberPattern =
        new(string.Empty)
        {
            Regex = PageXOfYRegex()
        };
    private static readonly TextToMatch EnvironmentAgencyTelephone1Pattern =
        new("708 506 506"); // Only this bit matches the pattern (excludes first number)
    private static readonly TextToMatch EnvironmentAgencyTelephone2Pattern =
        new("800 80 70 60"); // Only this bit matches the pattern (excludes first number)
    private static readonly TextToMatch EnvironmentAgencyTelephone3Pattern =
        new("345 988 1188"); // Only this bit matches the pattern (excludes first number)
    private static readonly TextToMatch EnvironmentAgencyTelephone4Pattern =
        new("845 988 1188"); // Only this bit matches the pattern (excludes first number)
    private static readonly TextToMatch LicenceNumberInHeaderPattern =
        new(string.Empty)
        {
            Regex = LicenceNumberInHeaderRegex()
        };
    
    [GeneratedRegex(@"Page \d* of \d*", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex PageXOfYRegex();
    
    [GeneratedRegex($"/^{LicenceNumberHeaderLine}[0-9GSABR*&/. ]{{1,15}}^/", RegexOptions.None, "en-GB")]
    private static partial Regex LicenceNumberInHeaderRegex();
}