using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using Migration.Engine.Linking;
using Migration.Engine.Profiles;
using Migration.Engine.Reading;
using Migration.Engine.Tests.Fakes;

namespace Migration.Engine.Tests.Linking;

[TestFixture]
public class LinkerTests
{
    private static MappingProfile Profile()
    {
        var p = new MappingProfile();
        p.LinkMap["relates to"] = "System.LinkTypes.Related";
        return p;
    }

    [Test]
    public void Link_CreatesRelation_WhenBothEndpointsResolveUniquely()
    {
        var ado = new FakeAdoClient();
        ado.Existing["PROJ-1"] = new() { 10 };
        ado.Existing["PROJ-2"] = new() { 20 };
        var issues = new List<RawIssue>
        {
            new() { Key = "PROJ-1", Links = { new RawLink { TargetKey = "PROJ-2", JiraLinkType = "relates to" } } }
        };

        var report = new Linker(ado).Link(issues, Profile(), CancellationToken.None);

        Assert.That(report.Created, Is.EqualTo(1));
        Assert.That(ado.Relations, Has.Count.EqualTo(1));
        Assert.That(ado.Relations[0], Is.EqualTo((10, 20, "System.LinkTypes.Related")));
    }

    [Test]
    public void Link_ReportsMissingEndpoint_WhenTargetNotInAdo()
    {
        var ado = new FakeAdoClient();
        ado.Existing["PROJ-1"] = new() { 10 };
        var issues = new List<RawIssue>
        {
            new() { Key = "PROJ-1", Links = { new RawLink { TargetKey = "PROJ-2", JiraLinkType = "relates to" } } }
        };

        var report = new Linker(ado).Link(issues, Profile(), CancellationToken.None);

        Assert.That(report.Created, Is.EqualTo(0));
        Assert.That(report.MissingEndpoint, Has.Count.EqualTo(1));
    }

    [Test]
    public void Link_ReportsAmbiguous_WhenEndpointHasMultipleMatches()
    {
        var ado = new FakeAdoClient();
        ado.Existing["PROJ-1"] = new() { 10 };
        ado.Existing["PROJ-2"] = new() { 20, 21 };   // clone -> non-unique
        var issues = new List<RawIssue>
        {
            new() { Key = "PROJ-1", Links = { new RawLink { TargetKey = "PROJ-2", JiraLinkType = "relates to" } } }
        };

        var report = new Linker(ado).Link(issues, Profile(), CancellationToken.None);

        Assert.That(report.Ambiguous, Has.Count.EqualTo(1));
        Assert.That(ado.Relations, Is.Empty);
    }

    [Test]
    public void Link_SkipsExisting_WhenRelationAlreadyPresent()
    {
        var ado = new FakeAdoClient();
        ado.Existing["PROJ-1"] = new() { 10 };
        ado.Existing["PROJ-2"] = new() { 20 };
        ado.AddRelation(10, 20, "System.LinkTypes.Related"); // pre-existing
        var issues = new List<RawIssue>
        {
            new() { Key = "PROJ-1", Links = { new RawLink { TargetKey = "PROJ-2", JiraLinkType = "relates to" } } }
        };

        var report = new Linker(ado).Link(issues, Profile(), CancellationToken.None);

        Assert.That(report.SkippedExisting, Is.EqualTo(1));
        Assert.That(report.Created, Is.EqualTo(0));
    }
}
