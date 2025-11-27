namespace WALE.ProcessFile.Core.Enums;

public enum MultipleServiceMatchBehaviour
{
    UseLastServiceResult,
    UseLongestUseLastServiceResultIfEqual,
    UseFullestDateUseLastServiceResultIfMultipleFull,
    UseBestLicenceNumberUseLastServiceResultIfEqual
}