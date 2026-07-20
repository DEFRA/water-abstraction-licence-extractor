using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.ProcessRunLicenceDisplay;

namespace WALE.ProcessFile.Core.Interfaces;

using System.Globalization;
using System.Text.Json;

public interface ILicenceListItemModelService
{
    LicenceListItemAggregate Create(OutputListDataItem source);

    IReadOnlyList<LicenceListItemAggregate> CreateMany(
        IEnumerable<OutputListDataItem> source);
}