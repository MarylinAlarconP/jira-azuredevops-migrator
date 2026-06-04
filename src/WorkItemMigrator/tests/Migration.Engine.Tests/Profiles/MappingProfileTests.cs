using NUnit.Framework;
using Migration.Engine.Profiles;

namespace Migration.Engine.Tests.Profiles;

[TestFixture]
public class MappingProfileTests
{
    [Test]
    public void TryMapType_ReturnsTarget_WhenSourcePresent()
    {
        var p = new MappingProfile();
        p.TypeMap["Story"] = "User Story";
        Assert.That(p.TryMapType("Story", out var target), Is.True);
        Assert.That(target, Is.EqualTo("User Story"));
    }

    [Test]
    public void TryMapType_False_WhenSourceMissing()
    {
        var p = new MappingProfile();
        Assert.That(p.TryMapType("Epic", out _), Is.False);
    }

    [Test]
    public void MapValue_UsesValueTable_PerTargetField()
    {
        var p = new MappingProfile();
        p.ValueTables["System.State"] = new() { ["Done"] = "Closed" };
        Assert.That(p.MapValue("System.State", "Done"), Is.EqualTo("Closed"));
        Assert.That(p.MapValue("System.State", "Unknown"), Is.EqualTo("Unknown"));
    }
}
