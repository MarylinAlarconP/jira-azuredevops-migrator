using NUnit.Framework;

namespace Migration.Engine.Tests;

[TestFixture]
public class SmokeTests
{
    [Test]
    public void TestProject_Runs()
    {
        Assert.That(1 + 1, Is.EqualTo(2));
    }
}
