using System.Linq;
using NUnit.Framework;
using Migration.Engine.Contracts;
using Migration.Engine.Preflight;
using Migration.Engine.Profiles;
using Migration.Engine.Tests.Fakes;

namespace Migration.Engine.Tests.Preflight;

[TestFixture]
public class ReadinessPreflightTests
{
    private static MappingProfile Profile()
    {
        var p = new MappingProfile();
        p.TypeMap["Story"] = "User Story";
        return p;
    }

    private static JobRequest Request() => new()
    {
        BaseAreaPath = "Proj\\Area", BaseIterationPath = "Proj\\Iter"
    };

    [Test]
    public void Check_Ok_WhenFieldTypeAndPathsExist()
    {
        var ado = new FakeAdoClient();
        ado.AreaPaths.Add("Proj\\Area");
        ado.IterationPaths.Add("Proj\\Iter");

        var result = new ReadinessPreflight(ado).Check(Request(), Profile());
        Assert.That(result.Ok, Is.True);
        Assert.That(result.Problems, Is.Empty);
    }

    [Test]
    public void Check_ReportsMissingLegacyIdFieldTypeAndPaths()
    {
        var ado = new FakeAdoClient();
        ado.Fields.Remove("Custom.LegacyID");
        ado.Types.Remove("User Story");
        // paths intentionally not added

        var result = new ReadinessPreflight(ado).Check(Request(), Profile());

        Assert.That(result.Ok, Is.False);
        Assert.That(result.Problems.Any(p => p.Contains("Custom.LegacyID")), Is.True);
        Assert.That(result.Problems.Any(p => p.Contains("User Story")), Is.True);
        Assert.That(result.Problems.Any(p => p.Contains("Proj\\Area")), Is.True);
        Assert.That(result.Problems.Any(p => p.Contains("Proj\\Iter")), Is.True);
    }
}
