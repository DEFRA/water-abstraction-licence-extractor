using WALE.ProcessFile.Models;
using WALE.ProcessFile.Services.Interfaces;

namespace WALE.ProcessFile.Services.Configuration;

public class LookupConfiguration(
    List<(string LabelGroupName, List<LabelToMatch> Labels)> labels,
    Dictionary<string, string> licenceMapping,
    IOutputService outputService,
    ICacheService cacheService)
{
    public Dictionary<string, string> LicenceMapping { get; } = licenceMapping;

    public List<(string LabelGroupName, List<LabelToMatch> Labels)> Labels { get; } = labels;

    public IOutputService OutputService { get; } = outputService;

    public ICacheService CacheService { get; set; } = cacheService;
}