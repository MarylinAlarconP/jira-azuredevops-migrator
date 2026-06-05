using NUnit.Framework;
using Migration.Engine.Storage;

namespace Migration.Engine.Tests.Storage;

[TestFixture]
public class JobStateTests
{
    [Test]
    public void RecordWritten_AddsLegacyIdToAdoIdMapping()
    {
        var s = new JobState();
        s.RecordWritten("PROJ-1", 42);
        s.RecordWritten("PROJ-1", 43); // non-unique LegacyID (clone) -> both ids kept
        Assert.That(s.WrittenAdoIds("PROJ-1"), Is.EquivalentTo(new[] { 42, 43 }));
        Assert.That(s.IsWritten("PROJ-1"), Is.True);
        Assert.That(s.IsWritten("PROJ-2"), Is.False);
    }
}
