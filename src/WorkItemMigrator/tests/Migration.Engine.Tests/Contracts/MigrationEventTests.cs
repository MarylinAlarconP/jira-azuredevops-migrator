using NUnit.Framework;
using Migration.Engine.Contracts;

namespace Migration.Engine.Tests.Contracts;

[TestFixture]
public class MigrationEventTests
{
    [Test]
    public void MigrationEvent_CarriesStageItemAndMessage()
    {
        var e = new MigrationEvent(MigrationStage.Writing, "PROJ-1", "created");
        Assert.That(e.Stage, Is.EqualTo(MigrationStage.Writing));
        Assert.That(e.ItemKey, Is.EqualTo("PROJ-1"));
        Assert.That(e.Message, Is.EqualTo("created"));
    }
}
