using System.IO;
using System.Linq;
using NUnit.Framework;
using Migration.Engine.Reading;
using Migration.Engine.Storage;

namespace Migration.Engine.Tests.Storage;

[TestFixture]
public class FileSystemStageStoreTests
{
    private string _root = "";

    [SetUp]
    public void SetUp() => _root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Test]
    public void SaveAndEnumerateRawIssues_RoundTrips()
    {
        var store = new FileSystemStageStore(_root, "job1");
        store.SaveRawIssue(new RawIssue { Key = "PROJ-1" });
        store.SaveRawIssue(new RawIssue { Key = "PROJ-2" });

        var keys = store.EnumerateRawIssues().Select(i => i.Key).OrderBy(k => k).ToArray();
        Assert.That(keys, Is.EqualTo(new[] { "PROJ-1", "PROJ-2" }));
    }

    [Test]
    public void SaveState_ThenLoadState_RoundTrips()
    {
        var store = new FileSystemStageStore(_root, "job1");
        var state = store.LoadState();
        state.RecordWritten("PROJ-1", 5);
        store.SaveState(state);

        var reloaded = new FileSystemStageStore(_root, "job1").LoadState();
        Assert.That(reloaded.IsWritten("PROJ-1"), Is.True);
        Assert.That(reloaded.WrittenAdoIds("PROJ-1"), Is.EquivalentTo(new[] { 5 }));
    }
}
