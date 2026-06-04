using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Migration.Engine.Contracts;
using Migration.Engine.Reporting;
using Migration.Engine.Writing;
using Migration.Engine.Tests.Fakes;

namespace Migration.Engine.Tests.Reporting;

[TestFixture]
public class ReportBuilderTests
{
    [Test]
    public void Build_SetsCountsAndReconciliationFromAdo()
    {
        var ado = new FakeAdoClient();
        ado.Existing["PROJ-1"] = new() { 1 };
        ado.Existing["PROJ-2"] = new() { 2 };

        var outcome = new WriteOutcome { Created = 1, Skipped = 1, Failed = 0 };
        var builder = new ReportBuilder(ado);

        var report = builder.Build(
            jobId: "job1", dryRun: false, totalInQuery: 2,
            legacyIds: new[] { "PROJ-1", "PROJ-2" },
            outcome: outcome,
            links: new LinkReport { Created = 1 },
            unmatchedUsers: new List<string> { "x@org" },
            unmappedType: new List<string>());

        Assert.That(report.Created, Is.EqualTo(1));
        Assert.That(report.Skipped, Is.EqualTo(1));
        Assert.That(report.AdoCountForLegacySet, Is.EqualTo(2));
        Assert.That(report.ReconciliationMismatch, Is.False);
        Assert.That(report.UnmatchedUsers, Is.EquivalentTo(new[] { "x@org" }));
    }

    [Test]
    public void WriteToWorkspace_ProducesJsonAndSummaryFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        var report = new JobReport { JobId = "job1", TotalInQuery = 1, Created = 1, AdoCountForLegacySet = 1 };

        new ReportBuilder(new FakeAdoClient()).WriteToWorkspace(report, dir);

        Assert.That(File.Exists(Path.Combine(dir, "report.json")), Is.True);
        Assert.That(File.Exists(Path.Combine(dir, "report.txt")), Is.True);
        Directory.Delete(dir, true);
    }
}
