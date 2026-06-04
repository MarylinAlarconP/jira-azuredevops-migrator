using System.Collections.Generic;
using System.Text;
using Migration.Engine.Reading;

namespace Migration.Engine.Tests.Fakes;

public sealed class FakeJiraClient : IJiraClient
{
    public List<RawIssue> Issues { get; } = new();
    public IEnumerable<RawIssue> Search(string jql) => Issues;
    public byte[] DownloadAttachment(RawAttachment a) => Encoding.UTF8.GetBytes("file:" + a.Id);
}
