namespace WALE.ProcessFile.Core.Enums;

public enum MultipleServiceMatchBehaviour
{
    UseLastServiceResult,
    UseFirstServiceResult,    
    UseLongestUseLastServiceResultIfEqual,
    UseFullestDateUseLastServiceResultIfMultipleFull,
    UseFullestDateUseHighestOcrConfidenceIfMultipleFull,    
    UseBestLicenceNumberUseLastServiceResultIfEqual,
    UseMostSubResultsUseLastServiceResultIfEqual,
    UseAllUnique,
    UseHighestOcrConfidence
}