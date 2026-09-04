using System.Text.Json;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.Core.AbstractionLicence.Models.ProcessRunLicenceDisplay;
using WRADI.Core.AbstractionLicence.Models.ProcessRunLicenceDisplay.DTOs;
using WRADI.Core.AbstractionLicence.Strategies;

namespace WRADI.Services.Output.AbstractionLicence;

public class DatabaseAbstractionLicenceOutputService(
    IDatabaseReadService ogDatabaseReadService,
    IAbstractionLicenceDatabaseReadService databaseReadService,
    IAbstractionLicenceDatabaseWriteService databaseWriteService,
    IDatabaseWriteService ogDatabaseWriteService) : IAbstractionLicenceOutputService, ILicenceListRepository
{
    public string? OutputFolder { get; set; } = null;
    
    public async Task SaveLicenceSetsAsync(Dictionary<string, LicenceSet> licenceSets, Guid? fileId, int processRunId)
    {
        foreach (var licenceSetKvp in licenceSets)
        {
            await SaveLicenceSetAsync(licenceSetKvp.Value, fileId, processRunId);
        }
    }

    public async Task SaveLicenceSetAsync(LicenceSet licenceSet, Guid? fileId, int processRunId)
    {
        var existingList = await databaseReadService.GetLicenceSetsSimpleAsync(processRunId);

        if (existingList.Any(x =>
                x.SchemaLicenceSetId == licenceSet.LicenceSetId && x.ShortLicenceSetId == licenceSet.ShortLicenceSetId))
        {
            return;
        }

        var licenceSetId = await databaseWriteService.SaveLicenceSetAsync(
            licenceSet.LicenceSetId,
            licenceSet.ShortLicenceSetId,
            processRunId);

        foreach (var licence in licenceSet.Licences)
        {
            var licenceId = licence.NoneSchemaData.TryGetValue("licenceId", out var licenceIdOut)
                ? (int?)licenceIdOut
                : null;

            if (string.IsNullOrEmpty(licence.LicenceNumber?.Value) && licenceId == null)
            {
                // TODO log
                continue;
            }

            await databaseWriteService.InsertLicenceSetLicenceAsync(
                licenceSetId,
                licenceId,
                licence.LicenceNumber?.Value,
                licence.LicenceVersion.LicenceVersionId,
                processRunId);
        }

        foreach (var licenceSetType in licenceSet.LicenceSetTypes)
        {
            await databaseWriteService.SaveLicenceSetTypeAsync(
                licenceSetId,
                (int)licenceSetType,
                processRunId);
        }

        if (licenceSet.AggregateSets == null)
        {
            return;
        }

        foreach (var aggregateSet in licenceSet.AggregateSets)
        {
            await databaseWriteService.SaveAggregateSetAsync(
                licenceSetId,
                aggregateSet.AggregateSetId,
                JsonSerializer.Serialize(aggregateSet.Aggregates, JsonHelper.GetSerializerOptions()),
                processRunId);
        }
    }

    public Task UpdateLicenceAsync(Licence licence, int licenceId, int processRunId)
    {
        var licenceStr = JsonSerializer.Serialize(licence, JsonHelper.GetSerializerOptions());

        return databaseWriteService.UpdateLicenceAsync(
            licenceId,
            licenceStr,
            licence.DmsFileId!.Value,
            processRunId,
            licence.Status.ToString());
    }

    public Task<int> SaveLicenceAsync(Licence licence, int processRunId)
    {
        var licenceStr = JsonSerializer.Serialize(licence, JsonHelper.GetSerializerOptions());

        return databaseWriteService.SaveLicenceAsync(
            licence.LicenceNumber?.Value,
            licence.Filename,
            licence.Status.ToString(),
            licenceStr,
            licence.DmsFileId,
            licence.DmsPermitNumber,
            processRunId);
    }
    
    public Task SaveListDataAsync(List<OutputListDataItem> listData, int processRunId)
    {
        // Don't need to, as it can just get it on the fly
        return Task.CompletedTask;
    }

    public async Task FinishProcessRunAsync(ProcessRun processRun)
    {
        // Fix up missing LicenceIds in LicenceSetList
        var licenceSetLicences = await databaseReadService.GetLicenceSetLicencesAsync(
            processRun.ProcessRunId);

        var missingLicenceIds = licenceSetLicences.Where(lsl => lsl.LicenceId == null);

        foreach (var missingLicenceId in missingLicenceIds)
        {
            if (missingLicenceId.LicenceNumber == null)
            {
                continue;
            }

            var licenceTransformed = FormattingHelper.FormatLicenceNumber(
                missingLicenceId.LicenceNumber,
                GeneralConstants.UnsetRegionCode)!; // Used as not known real region code

            var licence =
                await databaseReadService.GetLicenceAsync(
                    licenceTransformed,
                    processRun.ProcessRunId);

            if (licence == null)
            {
                // TODO log - shouldn't happen
                continue;
            }

            missingLicenceId.LicenceId = (int)licence.NoneSchemaData["licenceId"]!;
            await databaseWriteService.UpdateLicenceSetLicenceAsync(missingLicenceId);
        }

        await ogDatabaseWriteService.UpdateProcessRunAsync(processRun);
    }

    public Task UpdateProcessRunByLicenceNumbersAsync(int processRunId, string[] licenceNumbers)
    {
        throw new NotImplementedException();
    }

    public Task UpdateLicenceListProcessRunAsync(int processRunId)
    {
        throw new NotImplementedException();
    }

    public Task<List<DocumentNaldPurposeMap>> GetDocumentNaldPurposeMapAsync()
    {
        return databaseReadService.GetDocumentNaldPurposeMapAsync();
    }

    public Task AddDocumentNaldPurposeMapAsync(string documentDescription, NaldPurposeData naldPurpose, string matchType)
    {
        return databaseWriteService.AddDocumentNaldPurposeMapAsync(documentDescription, naldPurpose, matchType);
    }

    public Task AddDocumentNaldPurposeMatchAsync(
        string licNo,
        string documentDescription,
        NaldPurposeData naldPurpose,
        string matchType)
    {
        return databaseWriteService.AddDocumentNaldPurposeMatchAsync(licNo, documentDescription, naldPurpose, matchType);
    }

    public async Task<Licence?> GetLicenceAsync(Guid fileId, int processRunId, bool applyVerifications = false)
    {
        var licence = await databaseReadService.GetLicenceAsync(fileId, processRunId);

        return licence == null || !applyVerifications
            ? licence
            : await ApplyVerificationsAsync(licence, licence.DmsFileId ?? fileId, processRunId);
    }

    public async Task<Licence?> GetLicenceAsync(string licenceNumber, int processRunId, bool applyVerifications = false)
    {
        var licence = await databaseReadService.GetLicenceAsync(licenceNumber, processRunId);

        return licence == null || !applyVerifications || licence.DmsFileId is null
            ? licence
            : await ApplyVerificationsAsync(licence, licence.DmsFileId.Value, processRunId);
    }

    private async Task<Licence> ApplyVerificationsAsync(Licence licence, Guid fileId, int processRunId)
    {
        var verificationLicenceMergeStrategies =
            new List<IVerificationLicenceMergeStrategy>
            {
                new LinkedLicencesVerificationLicenceMergeStrategy(),
                new AggregatesVerificationLicenceMergeStrategy()
            }.ToDictionary(s => s.SectionName);

        var verificationsBySection = await GetVerificationLookupsBySectionNameAsync(processRunId);
        var fileIdToLicenceNumberMapping = await GetLicenceFileIdsAsync(processRunId);

        foreach (var (sectionName, strategy) in verificationLicenceMergeStrategies)
        {
            if (verificationsBySection.TryGetValue(sectionName, out var sectionVerificationLookups))
            {
                licence = strategy.ApplyVerifications(licence, sectionVerificationLookups, fileId, fileIdToLicenceNumberMapping);
            }
        }

        return licence;
    }

    public async Task<LinkedLicence[]?> GetLinkedLicencesAsync(string permitNumber)
    {
        var licence = await databaseReadService.GetNewestLicenceAsync(permitNumber);
        return licence?.LinkedLicences;
    }

    public Task<IEnumerable<LicenceSectionVerification>> GetLicenceSectionVerificationsAsync(Guid licenceFileId)
    {
        return databaseReadService.GetLicenceSectionVerificationsAsync(licenceFileId);
    }

    public Task<IEnumerable<LicenceSectionVerification>> GetAllVerificationsAsync(int maxProcessRunId)
    {
        return databaseReadService.GetAllVerificationsAsync(maxProcessRunId);
    }

    public async Task<Dictionary<string, LicenceVerificationLookups>> GetVerificationLookupsBySectionNameAsync(int maxProcessRunId)
    {
        var all = (await GetAllVerificationsAsync(maxProcessRunId)).ToList();

        return all
                .Where(v => !string.IsNullOrEmpty(v.LicenceSectionName))
                .GroupBy(v => v.LicenceSectionName)
                .ToDictionary(g => g.Key!, g =>
                    new LicenceVerificationLookups
                    {
                        ByFileId = g.GroupBy(v => v.LicenceFileId)
                            .ToDictionary(gf => gf.Key, gf => gf.ToList()),
                        ByItemId = g.GroupBy(v => v.LicenceSectionItemId)
                            .Where(gi => gi.Key != null)
                            .ToDictionary(gi => gi.Key!, gi => gi.ToList())
                    });
    }

    public Task<int> SaveLicenceSectionVerificationAsync(LicenceSectionVerification verification)
    {
        return databaseWriteService.SaveLicenceSectionVerificationAsync(verification);
    }

    public Task<int> DeleteLicenceSectionVerificationAsync(int licenceSectionVerificationId)
    {
        return databaseWriteService.DeleteLicenceSectionVerificationAsync(licenceSectionVerificationId);
    }

    public async Task<int> GetTotalLicenceCountAsync(int processRunId, ProcessRunQuery processRunQuery)
    {
        return await databaseReadService.GetTotalLicenceCountAsync(processRunId, processRunQuery);
    }

    public async Task<List<string>> GetDistinctIssuersAsync(int processRunId)
    {
        return await databaseReadService.GetDistinctIssuersAsync(processRunId);
    }

    public async Task<List<string>> GetDistinctIssueDatesAsync(int processRunId)
    {
        return await databaseReadService.GetDistinctIssueDatesAsync(processRunId);
    }

    public async Task<List<Licence>> GetLicencesAsync(int processRunId, int skip, int take)
    {
        var licences = await databaseReadService.GetLicencesAsync(processRunId, skip, take);

        foreach (var licence in licences)
        {
            licence.NoneSchemaData = JsonHelper.MakeJsonElementDictionaryNative(
                licence.NoneSchemaData);
        }

        return licences;
    }

    public Task<Dictionary<string, LicenceSet>> GetProcessRunLicenceSetsAsync(int processRunId)
    {
        throw new NotImplementedException();
    }

    public async Task<List<Licence>> GetLicencesSearchAsync(int processRunId, ProcessRunQuery processRunQuery)
    {
        return await databaseReadService.GetLicencesSearchAsync(processRunId, processRunQuery);
    }

    public async Task<Dictionary<string, LicenceSet>> GetLicenceSetsAsync(int processRunId, List<Licence> allLicences)
    {
        var licenceSetsTask = databaseReadService.GetLicenceSetsSimpleAsync(processRunId);
        var allLicenceSetLicencesTask = databaseReadService.GetLicenceSetLicencesAsync(processRunId);
        var allLicenceSetTypesTask = databaseReadService.GetLicenceSetTypesForProcessRun(processRunId);
        var allAggregateSetsTask = databaseReadService.GetAggregateSetsForProcessRun(processRunId);

        var licenceSets = await licenceSetsTask;
        var allLicenceSetLicences = await allLicenceSetLicencesTask;
        var allLicenceSetTypes = await allLicenceSetTypesTask;
        var allAggregateSets = await allAggregateSetsTask;

        var returnList = new Dictionary<string, LicenceSet>();

        foreach (var licenceSetSimple in licenceSets)
        {
            var licenceSet = new LicenceSet();

            var licenceSetLicenceIds = allLicenceSetLicences
                .Where(lsl => lsl.LicenceSetId == licenceSetSimple.LicenceSetId);

            var licences = new List<Licence>();

            foreach (var licenceSetLicence in licenceSetLicenceIds)
            {
                var licence = allLicences.FirstOrDefault(l =>
                {
                    if (!l.NoneSchemaData.TryGetValue("licenceId", out var licenceIdObj))
                    {
                        return false;
                    }

                    int licenceId;
                    if (licenceIdObj is JsonElement jsonElement)
                    {
                        licenceId = jsonElement.GetInt32();
                    }
                    else
                    {
                        licenceId = (int)licenceIdObj!;
                    }
                    
                    return licenceId == licenceSetLicence.LicenceId
                        ||
                           l.LicenceNumber?.Value == licenceSetLicence.LicenceNumber;
                });

                if (licence == null)
                {
                    continue;
                }

                licence.LicenceVersion.SetExplicitLicenceVersionId(licenceSetLicence.LicenceVersionId!);
                licences.Add(licence);
            }

            licenceSet.Licences = licences
                .ToArray();

            licenceSet.LicenceSetTypes = allLicenceSetTypes
                .Where(lst => lst.LicenceSetId == licenceSetSimple.LicenceSetId)
                .Select(lst => lst.Type)
                .ToArray();

            licenceSet.AggregateSets = allAggregateSets
                .Where(lst => lst.LicenceSetId == licenceSetSimple.LicenceSetId)
                .Select(lst => lst.AggregateSet)
                .ToArray();

            returnList.TryAdd(licenceSet.LicenceSetId, licenceSet);
        }

        return returnList;
    }

    public async Task<List<LicenceSet>> GetLicenceSetsAsync(Guid fileId)
    {
        var processRun = (await ogDatabaseReadService.GetMostRecentProcessRunAsync(fileId))!;

        var licenceSets = await databaseReadService.GetLicenceSetsSimpleAsync(
            fileId,
            processRun.ProcessRunId);

        var returnList = new List<LicenceSet>();

        foreach (var licenceSetSimple in licenceSets)
        {
            var licenceSet = new LicenceSet();

            var licenceSetLicenceIds =
                await databaseReadService.GetLicenceSetLicencesAsync(licenceSetSimple.LicenceSetId,
                    processRun.ProcessRunId);

            var licences = new List<Licence>();

            foreach (var licenceSetLicence in licenceSetLicenceIds)
            {
                var licence = new Licence
                {
                    LicenceNumber = !string.IsNullOrEmpty(licenceSetLicence.LicenceNumber)
                        ? new ValueWithConfidence<string>(
                            licenceSetLicence.LicenceNumber,
                            -1, // TODO
                            -1) // TODO
                        : null
                };

                licence.LicenceVersion.SetExplicitLicenceVersionId(licenceSetLicence.LicenceVersionId!);
                licences.Add(licence);
            }

            licenceSet.Licences = licences.ToArray();
            licenceSet.LicenceSetTypes = await databaseReadService.GetLicenceSetTypes(licenceSetSimple.LicenceSetId);
            licenceSet.AggregateSets = await databaseReadService.GetAggregateSets(licenceSetSimple.LicenceSetId);

            returnList.Add(licenceSet);
        }

        return returnList;
    }

    public Task<Dictionary<Guid, string>> GetLicenceFileIdsAsync(int processRunId)
    {
        return databaseReadService.GetLicenceFileIdsAsync(processRunId);
    }
    
    public async Task<long> UpsertLicenceListItemAsync(
        UpsertLicenceListItem item,
        CancellationToken cancellationToken = default)
    {
        return await databaseWriteService.UpsertLicenceListItemAsync(item, cancellationToken);
    }

    public async Task UpsertLicenceListItemManyAsync(
        IReadOnlyCollection<UpsertLicenceListItem> items,
        CancellationToken cancellationToken = default)
    {
        await databaseWriteService.UpsertLicenceListItemManyAsync(items, cancellationToken);
    }

    public async Task<List<LicenceListItemAggregate>> GetLicencesListSearchAsync(int processRunId,
        ProcessRunQuery query)
    {
        return await databaseReadService.GetLicencesListSearchAsync(processRunId, query);
    }

    public async Task<int> GetLicencesListSearchCountAsync(int processRunId, ProcessRunQuery query)
    {
        return await databaseReadService.GetLicencesListSearchCountAsync(processRunId, query);
    }

    public async Task<List<string>> GetLicenceListIssuersAsync(int processRunId)
    {
        return await databaseReadService.GetLicenceListDistinctIssuersAsync(processRunId);
    }

    public async Task<List<string>> GetLicenceListLicenceSetIdsAsync(int processRunId)
    {
        return await databaseReadService.GetLicenceListLicenceSetIdsAsync(processRunId);
    }

    public async Task<List<string>> GetLicenceListIssueYearsAsync(int processRunId)
    {
        return await databaseReadService.GetLicenceListIssueYearsAsync(processRunId);
    }
}