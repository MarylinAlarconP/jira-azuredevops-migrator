using System.Threading;
using Migration.Engine.Contracts;
using Migration.Engine.Storage;

namespace Migration.Engine.Reading;

public sealed class JiraReader : IJiraReader
{
    private readonly IJiraClient _client;
    public JiraReader(IJiraClient client) => _client = client;

    public int Read(JobRequest request, IStageStore store, IProgressReporter progress, CancellationToken ct)
    {
        var count = 0;
        foreach (var issue in _client.Search(request.Jql))
        {
            ct.ThrowIfCancellationRequested();
            foreach (var att in issue.Attachments)
            {
                try
                {
                    var bytes = _client.DownloadAttachment(att);
                    store.SaveAttachment(att.Id, bytes, att.FileName);
                }
                catch (System.Exception ex)
                {
                    progress.Report(MigrationStage.Reading, issue.Key,
                        $"skipped attachment {att.Id}: {ex.Message}");
                }
            }
            store.SaveRawIssue(issue);
            count++;
            progress.Report(MigrationStage.Reading, issue.Key, "read");
        }
        return count;
    }
}
