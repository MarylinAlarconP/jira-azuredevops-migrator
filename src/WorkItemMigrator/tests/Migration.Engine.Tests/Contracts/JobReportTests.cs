using NUnit.Framework;
using Migration.Engine.Contracts;

namespace Migration.Engine.Tests.Contracts;

[TestFixture]
public class JobReportTests
{
    [Test]
    public void JobReport_CountsMismatch_FlaggedWhenSourceNeqAdo()
    {
        var report = new JobReport
        {
            TotalInQuery = 10,
            Created = 7,
            Skipped = 2,
            Failed = 1,
            AdoCountForLegacySet = 8
        };
        Assert.That(report.ReconciliationMismatch, Is.True);
    }

    [Test]
    public void JobReport_NoMismatch_WhenPresentEqualsAdo()
    {
        var report = new JobReport
        {
            TotalInQuery = 5, Created = 3, Skipped = 2, Failed = 0,
            AdoCountForLegacySet = 5
        };
        Assert.That(report.ReconciliationMismatch, Is.False);
    }
}
