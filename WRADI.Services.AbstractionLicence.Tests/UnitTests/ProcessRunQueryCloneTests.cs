using System.Reflection;
using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

public class ProcessRunQueryCloneTests
{
    [Fact]
    public void WhenCloningAnEstateQuery_ThenTheSubclassAndItsOwnFieldsSurvive()
    {
        var query = new LicenceEstateQuery
        {
            ProjectedStatus = "Dead",
            ProcessRunId = 175,
            Issuer = "Environment Agency",
            Skip = 10,
            Take = 50
        };

        var clone = Assert.IsType<LicenceEstateQuery>(query.Clone());

        Assert.NotSame(query, clone);
        Assert.Equal("Dead", clone.ProjectedStatus);
        Assert.Equal(175, clone.ProcessRunId);
        Assert.Equal("Environment Agency", clone.Issuer);
        Assert.Equal(10, clone.Skip);
        Assert.Equal(50, clone.Take);
    }

    [Fact]
    public void WhenCloningAQuery_ThenEverySettablePropertyIsCopied()
    {
        var settable = typeof(ProcessRunQuery)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanWrite)
            .ToArray();

        var query = new ProcessRunQuery();

        foreach (var property in settable)
        {
            property.SetValue(query, DistinctiveValueFor(property));
        }

        var clone = query.Clone();

        Assert.NotSame(query, clone);

        foreach (var property in settable)
        {
            Assert.Equal(
                property.GetValue(query),
                property.GetValue(clone));
        }
    }

    private static object DistinctiveValueFor(PropertyInfo property)
    {
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        if (type == typeof(string))
        {
            return property.Name;
        }

        if (type == typeof(string[]))
        {
            return new[] { property.Name };
        }

        if (type == typeof(int))
        {
            return 7;
        }

        if (type == typeof(bool))
        {
            return true;
        }

        throw new NotSupportedException(
            $"{property.Name} is a {type.Name} - add a distinctive value for it.");
    }
}
