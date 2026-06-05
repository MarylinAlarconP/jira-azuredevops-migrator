using NUnit.Framework;
using Migration.Engine.Contracts;

namespace Migration.Engine.Tests.Contracts;

[TestFixture]
public class JobRequestTests
{
    [Test]
    public void JobRequest_DefaultsDryRunFalse_AndTransientRetryFalse()
    {
        var r = new JobRequest();
        Assert.That(r.DryRun, Is.False);
        Assert.That(r.TransientRetry, Is.False);
    }

    [Test]
    public void MigrationLevel_HasThreeLevels()
    {
        Assert.That(System.Enum.GetNames(typeof(MigrationLevel)).Length, Is.EqualTo(3));
    }
}
