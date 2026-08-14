using System.Text.Json;
using WALE.ProcessFile.Core.Helpers;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.Core.AbstractionLicence.Models.ProcessRunLicenceDisplay;
using WRADI.Core.AbstractionLicence.Models.ProcessRunLicenceDisplay.DTOs;

namespace WALE.Api.Services;

public class LicenceListItemModelService
    : ILicenceListItemModelService
{
    private static readonly string[] SupportedDateFormats =
    [
        "yyyy-MM-dd",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFF",
        "yyyy-MM-ddTHH:mm:ssK",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFFK"
    ];

    public IReadOnlyList<UpsertLicenceListItem> ConvertToUpsertLicenceListItems(
        IEnumerable<OutputListDataItem> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source
            .Select(ConvertToUpsertLicenceListItem)
            .ToArray();
    }

    public IReadOnlyList<OutputListDataItem> ConvertToOutputListDataItems(IEnumerable<LicenceListItemAggregate> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source
            .Select(ConvertFromAggregate)
            .ToArray();
    }

    public UpsertLicenceListItem ConvertToUpsertLicenceListItem(
        OutputListDataItem source)
    {
        ArgumentNullException.ThrowIfNull(source);
        
        return new UpsertLicenceListItem
        {
            ProcessRunId = source.processRunId!.Value,
            FileId = source.fileId,
            Filename = source.filename ?? string.Empty,
            LicenceNumber = source.licenceNumber,
            LicenceHolder = source.licenceHolder,
            NaldAggregate = source.naldHasAggregateCondition ?? false,

            Purposes = source.purposes?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .ToArray() ?? [],

            Points = source.points?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .ToArray() ?? [],

            LimitsCount = source.limitsCount,
            AggregatesCount = source.aggregatesCount,
            Ocr = source.ocr,
            IssueDate = ParseDateOnly(source.issueDate),
            Issuer = source.issuer,
            MeansFound = source.meansFound,
            Status = source.status,

            LinkedLicences = source.linkedLicences?
                .Select(ToUpsertLinkedLicence)
                .ToArray() ?? [],

            LicenceSets = source.licenceSets?
                .OfType<OutputListDataItemLicenceSet>()
                .Select(ToUpsertLicenceSet)
                .ToArray() ?? [],

            LicenceSectionVerifications = source.licenceSectionVerifications?.Any() == true
                ? source.licenceSectionVerifications?.ToArray() ?? []
                : [],
        };
    }

    private static OutputListDataItem ConvertFromAggregate(LicenceListItemAggregate source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var licence = source.Licence;

        return new OutputListDataItem
        {
            processRunId = licence.ProcessRunId,
            fileId = licence.FileId,
            filename = licence.Filename,
            licenceNumber = licence.LicenceNumber,
            licenceHolder = licence.LicenceHolder,

            purposes = licence.Purposes,
            points = licence.Points,

            limitsCount = licence.LimitsCount,
            aggregatesCount = licence.AggregatesCount,
            naldHasAggregateCondition = licence.NaldAggregate,
            ocr = licence.Ocr,
            issueDate = licence.IssueDate?.ToString("yyyy-MM-dd"),
            issuer = licence.Issuer,
            meansFound = licence.MeansFound,
            status = licence.Status,

            linkedLicences = source.LinkedLicences
                .Select(ToOutputLinkedLicence)
                .ToArray(),

            licenceSets = source.LicenceSets
                .Select(ToOutputLicenceSet)
                .ToArray(),
            
            licenceSectionVerifications = source.VerificationSections.ToArray()
            
        };
    }

    private static LinkedLicence ToOutputLinkedLicence(LicenceListItemLinkedLicence source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new LinkedLicence
        {
            LicenceNumber = source.LicenceNumber,
            RawScrapedLicenceNumber = source.RawScrapedLicenceNumber,
            DmsPermitNumber = source.DmsPermitNumber,
            DmsPath = source.DmsPath,
            DmsFileId = source.DmsFileId,
            Filename = source.Filename,
            RegionId = source.RegionId,
            NaldStatus = ParseEnum<NaldLicenceStatus>(source.NaldStatus),
            LicenceType = ParseEnum<LicenceType>(source.LicenceType),
            IsBecauseOfAggregate = source.IsBecauseOfAggregate,
            LicenceVersion = new LicenceVersion
            {
                EffectiveDate = source.EffectiveDate?.ToDateTime(TimeOnly.MinValue),
                Issuer = source.Issuer,
                NaldStatus = source.LicenceVersionNaldStatus,
                IssueDate = source.IssueDate?.ToDateTime(TimeOnly.MinValue),
                OriginalIssueDate = source.OriginalIssueDate?.ToDateTime(TimeOnly.MinValue),
                ExpiryDate = source.ExpiryDate?.ToDateTime(TimeOnly.MinValue),
                NaldExpiryDate = source.NaldExpiryDate,
                NaldEffectiveStartDate = source.NaldEffectiveStartDate,
                NaldEffectiveEndDate = source.NaldEffectiveEndDate,
                NaldOrigEffectiveDate = source.NaldOrigEffectiveDate,
                NaldIncrementNumber = source.NaldIncrementNumber,
                NaldIssueNumber = source.NaldIssueNumber,
                NaldOrigSignatureDate = source.NaldOrigSignatureDate,
                NaldRevocationDate = source.NaldRevocationDate,
                NaldSignatureDate = source.NaldSignatureDate,
                NaldUpdateReason = source.NaldUpdateReason,
                DmsFileIdStatus =  source.DmsFileIdStatus,
                DmsFileIdStatusDateUtc = source.DmsFileIdStatusDateUtc
            },
            ContainedIn = source.Locations
                .Select(GetContainedInformation)
                .ToArray()
        };
    }

    private static ContainedInInformation GetContainedInformation(LicenceListItemLinkLocation source)
    {
        ArgumentNullException.ThrowIfNull(source);

        Dictionary<string, string?>? sourceFields = null;

        if (!string.IsNullOrEmpty(source.SourceFields))
        {
            sourceFields = JsonSerializer.Deserialize<Dictionary<string, string?>>(source.SourceFields);
        }
        
        return new ContainedInInformation
        {
            LinkReason = source.LinkReason,
            LineNumber = source.LineNumber,
            PageNumber = source.PageNumber,
            SectionName = source.SectionName,
            Source = ParseEnum<InformationSource>(source.Source),
            Direction = ParseEnum<InformationDirection>(source.Direction),
            AcinCode = source.AcinCode,
            SourceFields = sourceFields
        };
    }
    
    private static TEnum ParseEnum<TEnum>(string? value) where TEnum : struct, Enum
    {
        if (string.IsNullOrEmpty(value))
        {
            return default(TEnum);
        }
        
        if (value != null && Enum.TryParse<TEnum>(value, ignoreCase: true, out var result))
        {
            return result;
        }

        throw new InvalidOperationException($"Unrecognized {typeof(TEnum).Name} value: '{value}'");
    }
    
    private static OutputListDataItemLicenceSet ToOutputLicenceSet(LicenceListItemLicenceSet source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new OutputListDataItemLicenceSet
        {
            LicenceSetId =  source.LicenceSetId,
            LicenceSetType = source.LicenceSetType,
            ShortLicenceSetId =  source.ShortLicenceSetId,
            LicenceSetTypes =  source.LicenceSetTypes
                .Select(x => x.LicenceSetType)
                .ToArray(),
        };
    }
    
    private static UpsertLicenceSetItem ToUpsertLicenceSet(
        OutputListDataItemLicenceSet source)
    {
        return new UpsertLicenceSetItem
        {
            LicenceSetType = source.LicenceSetType.ToString(),
            LicenceSetTypes = source.LicenceSetTypes.Select(x => x.ToString()).ToArray(),
           LicenceSetId  = source.LicenceSetId,
            ShortLicenceSetId = source.ShortLicenceSetId
        };
    }

    private static UpsertLinkedLicenceItem ToUpsertLinkedLicence(
        LinkedLicence source)
    {
        return new UpsertLinkedLicenceItem
        {
            LicenceNumber = source.LicenceNumber,
            RawScrapedLicenceNumber = source.RawScrapedLicenceNumber,
            DmsPermitNumber = source.DmsPermitNumber,
            DmsPath = source.DmsPath,
            DmsFileId = source.DmsFileId,
            Filename = source.Filename,
            LicenceVersionId =
                source.LicenceVersion?.LicenceVersionId
                ?? LicenceVersion.UnknownVersion,
            EffectiveDate = ToDateOnly(
                source.LicenceVersion?.EffectiveDate),
            ExpiryDate = ToDateOnly(
                source.LicenceVersion?.ExpiryDate),
            IssueDate = ToDateOnly(
                source.LicenceVersion?.IssueDate),
            OriginalIssueDate = ToDateOnly(
                source.LicenceVersion?.OriginalIssueDate),
            NaldExpiryDate = source.LicenceVersion?.NaldExpiryDate,
            NaldEffectiveStartDate = source.LicenceVersion?.NaldEffectiveStartDate,
            NaldOrigEffectiveDate = source.LicenceVersion?.NaldOrigEffectiveDate,
            NaldOrigSignatureDate = source.LicenceVersion?.NaldOrigSignatureDate,
            NaldEffectiveEndDate = source.LicenceVersion?.NaldEffectiveEndDate,
            NaldRevocationDate = source.LicenceVersion?.NaldRevocationDate,
            NaldSignatureDate = source.LicenceVersion?.NaldSignatureDate,
            NaldIncrementNumber = source.LicenceVersion?.NaldIncrementNumber,
            NaldIssueNumber = source.LicenceVersion?.NaldIssueNumber,
            NaldUpdateReason = source.LicenceVersion?.NaldUpdateReason,
            Issuer = source.LicenceVersion?.Issuer,
            DmsFileIdStatus = source.LicenceVersion?.DmsFileIdStatus,
            DmsFileIdStatusDateUtc = source.LicenceVersion?.DmsFileIdStatusDateUtc,
            NaldStatus = source.NaldStatus.ToString(),
            LicenceVersionNaldStatus = source.LicenceVersion?.NaldStatus,
            LicenceType = source.LicenceType.ToString(),
            RegionId = source.RegionId,
            ContainedIn = source.ContainedIn?
                .Select(ToUpsertContainedInInformation)
                .ToArray() ?? [],
            ConditionData = source.Condition is null
                ? null
                : JsonSerializer.Serialize(
                    source.Condition,
                    JsonHelper.GetSerializerOptions()),
            SourceData = JsonSerializer.Serialize(
                source,
                JsonHelper.GetSerializerOptions()),
            IsBecauseOfAggregate = source.IsBecauseOfAggregate
        };
    }

    private static UpsertContainedInInformation
        ToUpsertContainedInInformation(
            ContainedInInformation source)
    {
        return new UpsertContainedInInformation
        {
            Source = source.Source.ToString(),
            Direction = source.Direction?.ToString() ?? string.Empty,
            SectionName = source.SectionName,
            LinkReason = source.LinkReason,
            LineNumber = source.LineNumber,
            PageNumber = source.PageNumber,
            AcinCode = source.AcinCode,
            SourceFields = source.SourceFields != null
                ? JsonSerializer.Serialize(source.SourceFields, JsonHelper.GetSerializerOptions())
                : null
        };
    }

    private static DateOnly? ToDateOnly(DateTime? value)
    {
        return value.HasValue
            ? DateOnly.FromDateTime(value.Value)
            : null;
    }

    private static DateOnly? ParseDateOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateOnly.TryParse(value, out var result))
        {
            return result;
        }

        if (DateTime.TryParse(value, out var dateTime))
        {
            return DateOnly.FromDateTime(dateTime);
        }

        return null;
    }
}