using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Migration.Engine;
using Migration.Engine.Contracts;
using Migration.Engine.Linking;
using Migration.Engine.Profiles;
using Migration.Engine.Reading;
using Migration.Engine.Transforming;
using Migration.Engine.Writing;
using Migration.Engine.Preflight;
using Migration.Engine.Reporting;
using Migration.Engine.Storage;
using Migration.Engine.Tests.Fakes;

namespace Migration.Engine.Tests;

[TestFixture]
public class MigrationEngineTests
{
    private static MappingProfile Profile()
    {
        var p = new MappingProfile();
        p.TypeMap["Story"] = "User Story";
        p.Fields.Add(new FieldRule { SourceName = "summary", Target = WIContract.WiFieldReference.Title, Mapper = "MapTitle" });
        p.LinkMap["relates to"] = "System.LinkTypes.Related";
        return p;
    }

    private static (MigrationEngine engine, FakeAdoClient ado, string root) Build(FakeJiraClient jira)
    {
        var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var ado = new FakeAdoClient();
        ado.AreaPaths.Add(""); ado.IterationPaths.Add("");
        var engine = new MigrationEngine(
            storeFactory: (rt, id) => new FileSystemStageStore(rt, id),
            reader: new JiraReader(jira),
            transformer: new Transformer(),
            writer: new AdoWriter(ado),
            linker: new Linker(ado),
            ado: ado,
            profileLoader: _ => Profile());
        return (engine, ado, root);
    }

    [Test]
    public void RunMigration_CreatesItems_AndReportsReconciliation()
    {
        var jira = new FakeJiraClient();
        jira.Issues.Add(new RawIssue { Key = "PROJ-1", IssueType = "Story", Fields = { ["summary"] = "A" },
            Links = { new RawLink { TargetKey = "PROJ-2", JiraLinkType = "relates to" } } });
        jira.Issues.Add(new RawIssue { Key = "PROJ-2", IssueType = "Story", Fields = { ["summary"] = "B" } });

        var (engine, ado, root) = Build(jira);
        var request = new JobRequest { JobId = "job1", WorkspaceRoot = root, Jql = "x", ProfilePath = "p" };
        var events = new List<MigrationEvent>();

        var report = engine.RunMigration(request, new SyncProgress(events), CancellationToken.None);

        Assert.That(report.Created, Is.EqualTo(2));
        Assert.That(report.AdoCountForLegacySet, Is.EqualTo(2));
        Assert.That(report.ReconciliationMismatch, Is.False);
        Assert.That(report.Links.Created, Is.EqualTo(1));
        Assert.That(events, Is.Not.Empty);
        Directory.Delete(root, true);
    }

    [Test]
    public void RunMigration_DryRun_WritesNothingToAdo()
    {
        var jira = new FakeJiraClient();
        jira.Issues.Add(new RawIssue { Key = "PROJ-1", IssueType = "Story", Fields = { ["summary"] = "A" } });

        var (engine, ado, root) = Build(jira);
        var request = new JobRequest { JobId = "job1", WorkspaceRoot = root, Jql = "x", ProfilePath = "p", DryRun = true };

        var report = engine.RunMigration(request, null, CancellationToken.None);

        Assert.That(ado.Created, Is.Empty);
        Assert.That(report.DryRun, Is.True);
        Directory.Delete(root, true);
    }

    [Test]
    public void RunMigration_FailsFast_WhenPreflightProblem()
    {
        var jira = new FakeJiraClient();
        var (engine, ado, root) = Build(jira);
        ado.Fields.Remove("Custom.LegacyID"); // break pre-flight
        var request = new JobRequest { JobId = "job1", WorkspaceRoot = root, Jql = "x", ProfilePath = "p" };

        var ex = Assert.Throws<PreflightFailedException>(() =>
            engine.RunMigration(request, null, CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("Custom.LegacyID"));
        Directory.Delete(root, true);
    }
}
