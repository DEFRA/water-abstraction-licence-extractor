using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.ProcessRunLicenceDisplay;
using WALE.ProcessFile.Core.Models.ProcessRunLicenceDisplay.DTOs;

namespace WALE.ProcessFile.Core.Interfaces;

using System.Globalization;
using System.Text.Json;

public interface ILicenceListItemModelService
{
    UpsertLicenceListItem ConvertToUpsertLicenceListItem(OutputListDataItem source);

    IReadOnlyList<UpsertLicenceListItem> ConvertToUpsertLicenceListItems(
        IEnumerable<OutputListDataItem> source);
    
    IReadOnlyList<OutputListDataItem> ConvertToOutputListDataItems(
        IEnumerable<LicenceListItemAggregate> source);
}