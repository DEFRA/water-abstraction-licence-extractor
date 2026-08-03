using WRADI.Core.AbstractionLicence.Models;
using WRADI.Core.AbstractionLicence.Models.ProcessRunLicenceDisplay;
using WRADI.Core.AbstractionLicence.Models.ProcessRunLicenceDisplay.DTOs;

namespace WRADI.Core.AbstractionLicence.Interfaces;

public interface ILicenceListItemModelService
{
    UpsertLicenceListItem ConvertToUpsertLicenceListItem(OutputListDataItem source);

    IReadOnlyList<UpsertLicenceListItem> ConvertToUpsertLicenceListItems(
        IEnumerable<OutputListDataItem> source);
    
    IReadOnlyList<OutputListDataItem> ConvertToOutputListDataItems(
        IEnumerable<LicenceListItemAggregate> source);
}