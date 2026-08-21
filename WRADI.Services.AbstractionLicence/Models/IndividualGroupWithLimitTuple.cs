using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.DocumentType.AbstractionLicence.Models;

public class IndividualGroupWithLimitTuple
{
    public AbstractionLimitGroup Group { get; set; }
        
    public AbstractionLimit Limit { get; set; }
}