using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Models;

namespace WALE.Api.Interfaces;

public interface IUiProcessRunService
{
    Task<string> UpdateLicenceListProcessRunAsync(int processRunId);
    Task<string> UpdateProcessRunByLicenceNumbersAsync(int processRunId, string[] licenceNumbers);
    Task<IReadOnlyList<OutputListDataItem>> GetProcessRunRawDataList(int processRunId, ProcessRunQuery query);
} 