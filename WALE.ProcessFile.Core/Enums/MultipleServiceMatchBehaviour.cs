namespace WALE.ProcessFile.Core.Enums;

public enum MultipleServiceMatchBehaviour
{
    UseLastServiceResult,
    UseFirstServiceResult,    
    UseLongestUseLastServiceResultIfEqual,
    UseFullestDateUseLastServiceResultIfMultipleFull,
    UseBestLicenceNumberUseLastServiceResultIfEqual,
    UseMostSubResultsUseLastServiceResultIfEqual,
    UseAllUnique
}