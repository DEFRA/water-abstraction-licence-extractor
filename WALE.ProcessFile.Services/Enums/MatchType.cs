namespace WALE.ProcessFile.Services.Enums;

public enum MatchType
{
    NotApplicable = -100,
    NotFound = -99,
    
    SameLineIsCompany1Line = 9900,
    SameLineIsCompany2Lines = 7500,
    SameLineSingleWord = 9903,
    
    NearPreviousLineIsCompany = 8005,
    NearNextLineIsCompany = 9901,
    
    Between = 9503
}