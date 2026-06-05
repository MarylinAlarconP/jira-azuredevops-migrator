using System.IO;
using System.Threading;
using NUnit.Framework;
using Migration.Engine;
using Migration.Engine.Contracts;
using Migration.Engine.Linking;
using Migration.Engine.Profiles;
using Migration.Engine.Reading;
using Migration.Engine.Storage;
using Migration.Engine.Transforming;
using Migration.Engine.Writing;
using Migration.Engine.Tests.Fakes;
using Migration.WIContract;

namespace Migration.Engine.Tests;

[TestFixture]
public class EndToEndTests
{
    private static MappingProfile Profile()
    {
        var p = new MappingProfile();
        p.TypeMap["Story"] = "User Story";
        p.Fields.Add(new FieldRule { SourceName = "summary", Target = WiFieldReference.Title, Mapper = "MapTitle" });
        return p;
    }

    [Test]
    public void FullRun_ThenRerun_IsIdempotent_AndReportsUnmappedType()
    {
        var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var ado = new FakeAdoClient();
        ado.AreaPaths.Add(""); ado.IterationPaths.Add("");
        var jira = new FakeJiraClient();
        jira.Issues.Add(new RawIssue { Key = "PROJ-1", IssueType = "Story", Fields = { ["summary"] = "A" } });
        jira.Issues.Add(new RawIssue { Key = "PROJ-9", IssueType = "Epic" }); // unmapped at this level

        MigrationEngine Engine() => new(
            (rt, id) => new FileSystemStageStore(rt, id),
            new JiraReader(jira), new Transformer(), new AdoWriter(ado),
            new Linker(ado), ado, _ => Profile());

        var request = new JobRequest { JobId = "job1", WorkspaceRoot = root, Jql = "x", ProfilePath = "p" };

        var first = Engine().RunMigration(request, null, CancellationToken.None);
        Assert.That(first.Created, Is.EqualTo(1));
        Assert.That(first.UnmappedType, Is.EquivalentTo(new[] { "PROJ-9" }));

        var second = Engine().RunMigration(request, null, CancellationToken.None);
        Assert.That(second.Created, Is.EqualTo(0));
        Assert.That(second.Skipped, Is.EqualTo(1)); // PROJ-1 already present -> skipped
        Assert.That(ado.Created, Has.Count.EqualTo(1)); // still only one create across both runs

        Directory.Delete(root, true);
    }
}
