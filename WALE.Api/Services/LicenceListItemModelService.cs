using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Core.Models.ProcessRunLicenceDisplay;

namespace WALE.Api.Services;

using System.Globalization;
using System.Text.Json;

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

    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public LicenceListItemModelService(
        JsonSerializerOptions? jsonSerializerOptions = null)
    {
        _jsonSerializerOptions =
            jsonSerializerOptions
            ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }

    public IReadOnlyList<LicenceListItemAggregate> CreateMany(
        IEnumerable<OutputListDataItem> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source
            .Select(Create)
            .ToArray();
    }

    public LicenceListItemAggregate Create(
        OutputListDataItem source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var processRunId = source.processRunId
            ?? throw new InvalidOperationException(
                $"ProcessRunId is required for file '{source.filename}'.");

        if (source.fileId == Guid.Empty)
        {
            throw new InvalidOperationException(
                $"FileId is required for file '{source.filename}'.");
        }

        if (string.IsNullOrWhiteSpace(source.filename))
        {
            throw new InvalidOperationException(
                $"Filename is required for file ID '{source.fileId}'.");
        }

        var purposes = NormaliseStrings(source.purposes);
        var points = NormaliseStrings(source.points);

        var linkedLicenceSources =
            source.linkedLicences ?? [];

        var licenceSetSources =
            source.licenceSets?
                .Where(x => x is not null)
                .Select(x => x!)
                .ToArray()
            ?? [];

        var verificationSources =
            source.LicenceSectionItems ?? [];

        var issueDate = ParseDate(source.issueDate);

        var aggregate = new LicenceListItemAggregate
        {
            Licence = new LicenceListItem
            {
                ProcessRunId = processRunId,
                FileId = source.fileId,
                Filename = source.filename.Trim(),
                LicenceNumber = Normalise(source.licenceNumber),
                LicenceHolder = Normalise(source.licenceHolder),

                LimitsCount = source.limitsCount,
                AggregatesCount = source.aggregatesCount,
                Ocr = source.ocr,

                IssueDate = issueDate,
                IssueYear = issueDate is null
                    ? null
                    : checked((short)issueDate.Value.Year),

                Issuer = Normalise(source.issuer),
                MeansFound = source.meansFound,
                Status = Normalise(source.status),

                PurposesCount = purposes.Length,
                PointsCount = points.Length,
                LinkedLicencesCount = linkedLicenceSources.Length,
                LicenceSetsCount = licenceSetSources.Length,

                VerificationSectionsCount =
                    verificationSources.Count,

                VerificationItemsCount =
                    verificationSources.Sum(
                        x => x.LicenceSectionItems?.Length ?? 0),

                HasVerifications =
                    verificationSources.Any(
                        section =>
                            section.LicenceSectionItems is { Length: > 0 }),

                SearchText = BuildSearchText(source),

                SourceData = JsonSerializer.Serialize(
                    source,
                    _jsonSerializerOptions)
            }
        };

        aggregate.Purposes.AddRange(
            CreatePurposes(purposes));

        aggregate.Points.AddRange(
            CreatePoints(points));

        aggregate.LinkedLicences.AddRange(
            linkedLicenceSources.Select(
                CreateLinkedLicence));

        aggregate.LicenceSets.AddRange(
            licenceSetSources.Select(
                x => CreateLicenceSet(
                    processRunId,
                    x)));

        aggregate.VerificationSections.AddRange(
            verificationSources.Select(
                x => CreateVerificationSection(
                    processRunId,
                    x)));

        return aggregate;
    }

    private static IEnumerable<LicenceListItemPurpose> CreatePurposes(
        IReadOnlyList<string> purposes)
    {
        return purposes.Select(
            (purpose, index) =>
                new LicenceListItemPurpose
                {
                    Purpose = purpose,
                    SortOrder = index
                });
    }

    private static IEnumerable<LicenceListItemPoint> CreatePoints(
        IReadOnlyList<string> points)
    {
        return points.Select(
            (point, index) =>
                new LicenceListItemPoint
                {
                    Point = point,
                    SortOrder = index
                });
    }

    private LicenceListItemLinkedLicence CreateLinkedLicence(
        LinkedLicence source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var linkedLicence = new LicenceListItemLinkedLicence
        {
            LicenceNumber =
                Normalise(source.LicenceNumber),

            RawScrapedLicenceNumber =
                Normalise(source.RawScrapedLicenceNumber),

            DmsPermitNumber =
                Normalise(source.DmsPermitNumber),

            DmsFileId =
                ParseGuid(source.DmsFileId.ToString()),

            Filename =
                Normalise(source.Filename),

            DmsPath =
                Normalise(source.DmsPath),

            LicenceVersionId =
                Normalise(
                    source.LicenceVersion?.LicenceVersionId),

            EffectiveDate = source.LicenceVersion?.EffectiveDate,

            IssueDate = 
                    source.LicenceVersion?.IssueDate,
            
            Issuer =
                Normalise(
                    source.LicenceVersion?.Issuer),

            NaldStatus = source.NaldStatus.ToString(),

            LicenceType =
                Normalise(source.LicenceType.ToString()),

            RegionId =
                source.RegionId,

            SourceData = JsonSerializer.Serialize(
                source,
                _jsonSerializerOptions)
        };

        var locations = source.ContainedIn ?? [];

        linkedLicence.Locations.AddRange(
            locations.Select(
                location =>
                    new LicenceListItemLinkLocation
                    {
                        Source =
                            Normalise(location.Source.ToString()),

                        Direction =
                            Normalise(location.Direction.ToString()),

                        SectionName =
                            Normalise(location.SectionName),

                        LinkReason =
                            Normalise(location.LinkReason),

                        IsBecauseOfAggregate =
                            location.IsBecauseOfAggregate,

                        LineNumber =
                            location.LineNumber,

                        PageNumber =
                            location.PageNumber
                    }));

        return linkedLicence;
    }

    private static LicenceListItemLicenceSet CreateLicenceSet(
        int processRunId,
        OutputListDataItemLicenceSet source)
    {
        if (string.IsNullOrWhiteSpace(source.LicenceSetId))
        {
            throw new InvalidOperationException(
                "LicenceSetId is required.");
        }

        var licenceSet = new LicenceListItemLicenceSet
        {
            ProcessRunId = processRunId,

            LicenceSetId =
                source.LicenceSetId.Trim(),

            ShortLicenceSetId =
                Normalise(source.ShortLicenceSetId),

            LicenceSetType =
                source.LicenceSetType
        };

        var types = source.LicenceSetTypes?
                        .Select(x => x)
            .Distinct()
            .ToArray()
            ?? [];

        licenceSet.LicenceSetTypes.AddRange(
            types.Select(
                type =>
                    new LicenceListItemLicenceSetType
                    {
                        LicenceSetType = type
                    }));

        return licenceSet;
    }

    private static LicenceListItemVerificationSection
        CreateVerificationSection(
            int processRunId,
            LicenceSectionVerificationSummary source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (string.IsNullOrWhiteSpace(
                source.LicenceSectionName))
        {
            throw new InvalidOperationException(
                "LicenceSectionName is required.");
        }

        var section =
            new LicenceListItemVerificationSection
            {
                ProcessRunId = processRunId,

                LicenceSectionName =
                    source.LicenceSectionName.Trim()
            };

        var items =
            source.LicenceSectionItems ?? [];

        section.LicenceSectionItems.AddRange(
            items.Select(CreateVerificationItem));

        return section;
    }

    private static LicenceListItemVerificationItem
        CreateVerificationItem(
            LicenceSectionItemSummary source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (string.IsNullOrWhiteSpace(
                source.LicenceSectionItemId))
        {
            throw new InvalidOperationException(
                "LicenceSectionItemId is required.");
        }

        var item =
            new LicenceListItemVerificationItem
            {
                LicenceSectionItemId =
                    source.LicenceSectionItemId.Trim()
            };

        var verificationTypes =
            NormaliseStrings(source.VerificationTypes);

        item.VerificationTypes.AddRange(
            verificationTypes.Select(
                type =>
                    new LicenceListItemVerificationType
                    {
                        VerificationType = type
                    }));

        return item;
    }

    private static string BuildSearchText(
        OutputListDataItem source)
    {
        var linkedLicenceNumbers =
            source.linkedLicences?
                .Select(x => x.LicenceNumber)
            ?? [];

        return string.Join(
            " ",
            new[]
            {
                source.filename,
                source.licenceNumber,
                source.licenceHolder
            }
            .Concat(linkedLicenceNumbers)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string[] NormaliseStrings(
        IEnumerable<string?>? values)
    {
        return values?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? [];
    }

    private static string? Normalise(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static Guid? ParseGuid(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Guid.TryParse(value, out var result)
            ? result
            : null;
    }

    private static DateOnly? ParseDate(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateOnly.TryParseExact(
                value,
                SupportedDateFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var dateOnly))
        {
            return dateOnly;
        }

        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces
                | DateTimeStyles.AssumeUniversal,
                out var dateTimeOffset))
        {
            return DateOnly.FromDateTime(
                dateTimeOffset.UtcDateTime);
        }

        throw new FormatException(
            $"The date value '{value}' is invalid.");
    }

    private static int? ConvertLicenceSetType<T>(
        T? value)
        where T : struct
    {
        return value switch
        {
            null => null,
            Enum enumValue => Convert.ToInt32(
                enumValue,
                CultureInfo.InvariantCulture),
            _ => Convert.ToInt32(
                value,
                CultureInfo.InvariantCulture)
        };
    }
}