using WALE.ProcessFile.Core.Models;

namespace WRADI.DocumentType.AbstractionLicence.Configuration;

public static class LicenceReaderConfiguration
{
    public static List<(string LabelGroupName, List<LabelToMatch> Labels)> GetLabels()
    {
        return
        [
            ("LicenceNumber", SharedLabels.GetLicenceNumberLabels()),
            ("DateOfIssue", SharedLabels.GetDateOfIssueLabels()),
            ..TemplateHeaderLabels.GetLabelGroups()
        ];
    }
}
