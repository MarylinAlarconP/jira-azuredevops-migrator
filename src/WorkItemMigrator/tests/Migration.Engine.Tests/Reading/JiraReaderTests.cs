using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Migration.Engine.Contracts;
using Migration.Engine.Reading;
using Migration.Engine.Storage;
using Migration.Engine.Tests.Fakes;

namespace Migration.Engine.Tests.Reading;

[TestFixture]
public class JiraReaderTests
{
    [Test]
    public void Read_PersistsIssuesAndDownloadsAttachments()
    {
        var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var store = new FileSystemStageStore(root, "job1");
        var client = new FakeJiraClient();
        client.Issues.Add(new RawIssue
        {
            Key = "PROJ-1",
            Attachments = { new RawAttachment { Id = "100", FileName = "a.png" } }
        });

        var reader = new JiraReader(client);
        var count = reader.Read(new JobRequest { Jql = "project=PROJ" }, store,
            new RecordingProgress(), CancellationToken.None);

        Assert.That(count, Is.EqualTo(1));
        Assert.That(store.EnumerateRawIssues().Single().Key, Is.EqualTo("PROJ-1"));
        Assert.That(File.Exists(Path.Combine(store.AttachmentPath("100"), "a.png")), Is.True);

        Directory.Delete(root, true);
    }
}
