using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Helpers;

public class GeneralChecks
{
    public static bool ContainsForbiddenText(DocumentLine? line, LabelToMatch label)
    {
        return label.MustNotContain?
            .Any(mustNotContainText =>
                line?.Text.Contains(mustNotContainText, StringComparison.InvariantCultureIgnoreCase) == true) == true;
    }
}