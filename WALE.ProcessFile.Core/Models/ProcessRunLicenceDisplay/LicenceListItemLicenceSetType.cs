using WALE.ProcessFile.Core.Enums.OutputSchema;

namespace WALE.ProcessFile.Core.Models.ProcessRunLicenceDisplay;

public sealed class LicenceListItemLicenceSetType
{
    public long LicenceListItemLicenceSetId { get; set; }

    public LicenceSetType LicenceSetType { get; set; }
}