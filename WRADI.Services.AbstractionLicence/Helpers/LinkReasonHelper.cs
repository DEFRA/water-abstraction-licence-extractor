using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Models;

namespace WRADI.DocumentType.AbstractionLicence.Helpers;

/// <summary>
/// The phrase a licence's text has to carry for it to count as linked to another, and which
/// of those reasons is then dropped again.
/// </summary>
public static class LinkReasonHelper
{
    public static string? GetLinkReason(List<LabelGroupResult> sections, string? textToFind)
    {
        foreach (var section in sections)
        {
            var text = string.Join('\n', section.Text!.Select(t => t.Text));
            var result = GetLinkReason(text, textToFind);

            if (!string.IsNullOrEmpty(result))
            {
                return result;
            }
        }

        return null;
    }

    public static string? GetLinkReason(string? text, string? textToFind)
    {
        if (string.IsNullOrEmpty(text) || (!string.IsNullOrEmpty(textToFind) && !text.Contains(textToFind)))
        {
            return null;
        }

        if (text.Contains("continuous release of water", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.ReleaseOfWater;
        }
        
        if (text.Contains("quantity of water released", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.ReleaseOfWater;
        }
        
        if (text.Contains("rate of flow", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.RateOfFlow;
        }
        
        if (text.Contains("minimum value", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.MinimumValue;
        }

        if (text.Contains("lapsed licence", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.LapsedLicence;
        }

        if (text.Contains("discharge and re-abstraction", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.DischargeAndReabstractionCondition;
        }

        if (text.Contains("simultaneous discharge", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.SimultaneousDischargeCondition;
        }

        if (text.Contains("simultaneous abstraction", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.SimultaneousAbstractionCondition;
        }

        if (text.Contains("simultaneous compensatory discharge", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.SimultaneousCompensatoryDischargeCondition;
        }

        if (text.Contains("compensatory discharge", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.CompensatoryDischargeCondition;
        }
        
        if (text.Contains("compensation flow", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.CompensationFlow;
        }

        if (text.Contains("read in conjunction", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.ReadInConjunction;
        }

        if (text.Contains("The donor licence was", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.DonorLicence;
        }

        if (text.Contains("used in conjunction", StringComparison.OrdinalIgnoreCase)
            || text.Contains("use in conjunction", StringComparison.OrdinalIgnoreCase)) // misspelling
        {
            return LinkReason.UsedInConjunction;
        }

        if (text.Contains("revocation", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.Revocation;
        }
        
        if (text.Contains("when the flow", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.WhenTheFlow;
        }
        
        if (text.Contains("aggregate conditions", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.AggregateConditions;
        }

        if (text.Contains("emergency circumstances", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.EmergencyCircumstances;
        }

        if (text.Contains("Dewatering Discharge", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.DewateringDischargeCondition;
        }

        if (text.Contains("when added to", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.WhenAddedTo;
        }

        if (text.Contains("subsequent abstraction", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.SubsequentAbstraction;
        }

        if (text.Contains("re-abstraction", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.ReAbstraction;
        }

        if (text.Contains("readings", StringComparison.OrdinalIgnoreCase)
            && text.Contains("discharged", StringComparison.OrdinalIgnoreCase)
            && text.Contains("augmentation", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.ReadingsDischargedAugmentationCondition;
        }

        if (text.Contains("aggregate", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.AggregateCondition;
        }
        
        if (text.Contains("in total between", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.AggregateCondition;
        }

        if (text.Contains("in an emergency", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.InAnEmergency;
        }
        
        if (text.Contains("shall not exceed", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.ShallNotExceed;
        }

        if (text.Contains("supporting", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.Supporting;
        }

        if (text.Contains("original licence", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.OriginalLicence;
        }

        if (text.Contains("transferred to this", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.TransferredToThis;
        }

        if (text.Contains("coincident", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.Coincident;
        }

        if (text.Contains("shall be supported", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.ShallBeSupported;
        }

        if (text.Contains("residual flow", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.ResidualFlow;
        }
        
        if (text.Contains("authorised by", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.AuthorisedBy;
        }

        return null;
    }

    public static bool IsExcludedLinkReason(string? linkReason)
    {
        return linkReason is LinkReason.SimultaneousDischargeCondition
            or LinkReason.CompensatoryDischargeCondition
            or LinkReason.DewateringDischargeCondition
            or LinkReason.ReadingsDischargedAugmentationCondition
            or LinkReason.CompensationFlow
            or LinkReason.RateOfFlow
            or LinkReason.WhenTheFlow
            or LinkReason.MinimumValue
            or LinkReason.ReleaseOfWater;
    }
}
