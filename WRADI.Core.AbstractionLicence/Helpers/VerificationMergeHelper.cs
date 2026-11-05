using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Core.AbstractionLicence.Helpers;

public static class VerificationMergeHelper
{
    public const string MissingJsonFlagReason = "BC Missing JSON";

    public static string GetVerificationWithNotes(LicenceSectionVerification verification)
    {
        var typeWithProcessRun = $"{verification.VerificationType}::{verification.ProcessRunId}";
        return string.IsNullOrWhiteSpace(verification.Notes) ? typeWithProcessRun : $"{typeWithProcessRun}::{verification.Notes}";
    }
    
    public static bool IsBusinessReviewWithNotes(string? verificationTypeWithNotes)
        => verificationTypeWithNotes != null 
           && (verificationTypeWithNotes.Contains("RequestBusinessReview") || verificationTypeWithNotes.Contains("CompleteBusinessReview"));
    
    public static bool IsExistingVerificationType(string? verificationTypeWithNotes, string verificationType)
        => verificationTypeWithNotes != null 
           && (verificationTypeWithNotes.Contains(verificationType));
    
    public static bool IsBusinessReview(string? verificationType)
        => verificationType is "RequestBusinessReview" or "CompleteBusinessReview";

    // CompleteBusinessReview verifications saved before JSON was recorded for them can't be merged
    public static bool IsCompleteBusinessReviewMissingJson(LicenceSectionVerification verification)
        => verification.VerificationType is "CompleteBusinessReview"
           && string.IsNullOrEmpty(verification.LicenceSectionOverrideValue)
           && string.IsNullOrEmpty(verification.LicenceSectionSnapshotValue)
           && string.IsNullOrEmpty(verification.LicenceSectionScrapedValue);

    public static void AddNewVerificationType(LicenceSectionVerification verification,
        LicenceSectionItemSummary existingSummary)
    {
        existingSummary.VerificationTypes = existingSummary.VerificationTypes
            .Append(verification.VerificationType!)
            .ToArray();

        existingSummary.VerificationTypesWithNotes = existingSummary.VerificationTypesWithNotes
            .Append(VerificationMergeHelper.GetVerificationWithNotes(verification))
            .ToArray();
    }

    public static T? DeserializeVerificationValue<T>(string json, out JsonObject? node)
    {
        node = JsonNode.Parse(json) as JsonObject;
        if (node == null)
        {
            return default;
        }

        ReplaceNullsForNonNullableValueTypes(node, SerializerOptions.GetTypeInfo(typeof(T)));
        return node.Deserialize<T>(SerializerOptions);
    }

    // Properties added to the type after the verification was made are absent from its JSON, so keep the current run's value
    public static void ApplyCurrentRunValuesForMissingProperties<T>(T verificationValue, T? currentRunValue, JsonObject node)
    {
        if (verificationValue == null || currentRunValue == null)
        {
            return;
        }

        FillMissingProperties(node, verificationValue, currentRunValue, SerializerOptions.GetTypeInfo(typeof(T)));
    }

    private static readonly Lazy<JsonSerializerOptions> LazySerializerOptions = new(() =>
    {
        var options = new JsonSerializerOptions(WALE.ProcessFile.Core.Helpers.JsonHelper.GetSerializerOptions())
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };
        options.MakeReadOnly();
        return options;
    });

    private static JsonSerializerOptions SerializerOptions => LazySerializerOptions.Value;

    private static void ReplaceNullsForNonNullableValueTypes(JsonNode? node, JsonTypeInfo typeInfo)
    {
        // This traverses the JSON tree and replaces nulls with default values for non-nullable value types, so that deserialization doesn't throw an exception.
        // For example, if a property is of type int, but the JSON has null for that property, this will replace it with 0.
        switch (typeInfo.Kind)
        {
            case JsonTypeInfoKind.Object when node is JsonObject jsonObject:
                foreach (var property in typeInfo.Properties)
                {
                    if (!jsonObject.TryGetPropertyValue(property.Name, out var value))
                    {
                        continue;
                    }

                    if (value != null)
                    {
                        ReplaceNullsForNonNullableValueTypes(value, SerializerOptions.GetTypeInfo(property.PropertyType));
                    }
                    else if (property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) == null)
                    {
                        jsonObject[property.Name] = JsonSerializer.SerializeToNode(
                            Activator.CreateInstance(property.PropertyType), property.PropertyType, SerializerOptions);
                    }
                }

                break;
            case JsonTypeInfoKind.Enumerable when node is JsonArray jsonArray:
                var elementTypeInfo = SerializerOptions.GetTypeInfo(typeInfo.ElementType!);
                foreach (var element in jsonArray)
                {
                    ReplaceNullsForNonNullableValueTypes(element, elementTypeInfo);
                }

                break;
        }
    }

    private static void FillMissingProperties(JsonObject node, object target, object current, JsonTypeInfo typeInfo)
    {
        foreach (var property in typeInfo.Properties)
        {
            if (property.Get == null || property.Set == null)
            {
                continue;
            }

            // If the property is missing from the verification JSON, copy the value from the current run to the target object.
            if (!node.TryGetPropertyValue(property.Name, out var value))
            {
                property.Set(target, property.Get(current));
                continue;
            }

            var targetValue = property.Get(target);
            var currentValue = property.Get(current);
            if (targetValue == null || currentValue == null)
            {
                continue;
            }

            // Traverse into nested objects and arrays to fill in missing properties recursively.
            var propertyTypeInfo = SerializerOptions.GetTypeInfo(property.PropertyType);
            switch (propertyTypeInfo.Kind)
            {
                case JsonTypeInfoKind.Object when value is JsonObject jsonObject:
                    FillMissingProperties(jsonObject, targetValue, currentValue, propertyTypeInfo);
                    break;
                case JsonTypeInfoKind.Enumerable when value is JsonArray jsonArray:
                    FillMissingElementProperties(jsonArray, (IEnumerable)targetValue,
                        (IEnumerable)currentValue, SerializerOptions.GetTypeInfo(propertyTypeInfo.ElementType!));
                    break;
            }
        }
    }

    private static void FillMissingElementProperties(JsonArray nodes, IEnumerable targets,
        IEnumerable currents, JsonTypeInfo elementTypeInfo)
    {
        if (elementTypeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        var targetElements = targets.Cast<object?>().ToList();
        var currentElements = currents.Cast<object?>().ToList();

        for (var i = 0; i < nodes.Count && i < targetElements.Count; i++)
        {
            if (nodes[i] is not JsonObject elementNode || targetElements[i] is not { } targetElement)
            {
                continue;
            }

            var currentElement = FindMatchingElement(elementNode, targetElement, currentElements, elementTypeInfo);
            if (currentElement != null)
            {
                FillMissingProperties(elementNode, targetElement, currentElement, elementTypeInfo);
            }
        }
    }

    // Attempt to match array elements by comparing all existing scalar properties for equality.
    private static object? FindMatchingElement(JsonObject elementNode, object targetElement,
        List<object?> currentElements, JsonTypeInfo elementTypeInfo)
    {
        var presentScalarProperties = elementTypeInfo.Properties
            .Where(p => p.Get != null
                        && elementNode.ContainsKey(p.Name)
                        && SerializerOptions.GetTypeInfo(p.PropertyType).Kind == JsonTypeInfoKind.None)
            .ToList();

        if (presentScalarProperties.Count == 0)
        {
            return null;
        }

        return currentElements.FirstOrDefault(c => c != null
                                                   && presentScalarProperties.All(p =>
                                                       Equals(p.Get!(targetElement), p.Get(c))));
    }
}
