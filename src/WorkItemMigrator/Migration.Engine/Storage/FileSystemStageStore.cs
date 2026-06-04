using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Migration.Engine.Reading;

namespace Migration.Engine.Storage;

public sealed class FileSystemStageStore : IStageStore
{
    private readonly string _jobDir;
    private readonly string _rawDir;
    private readonly string _attachDir;
    private readonly string _statePath;

    public FileSystemStageStore(string root, string jobId)
    {
        _jobDir = Path.Combine(root, "jobs", jobId);
        _rawDir = Path.Combine(_jobDir, "raw");
        _attachDir = Path.Combine(_jobDir, "attachments");
        _statePath = Path.Combine(_jobDir, "state.json");
        Directory.CreateDirectory(_rawDir);
        Directory.CreateDirectory(_attachDir);
    }

    public void SaveRawIssue(RawIssue issue)
    {
        var path = Path.Combine(_rawDir, SafeName(issue.Key) + ".json");
        File.WriteAllText(path, JsonConvert.SerializeObject(issue, Formatting.Indented));
    }

    public IEnumerable<RawIssue> EnumerateRawIssues()
    {
        foreach (var path in Directory.EnumerateFiles(_rawDir, "*.json"))
        {
            var issue = JsonConvert.DeserializeObject<RawIssue>(File.ReadAllText(path));
            if (issue != null) yield return issue;
        }
    }

    public void SaveAttachment(string attachmentId, byte[] bytes, string fileName)
    {
        var dir = Path.Combine(_attachDir, SafeName(attachmentId));
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, fileName), bytes);
    }

    public string AttachmentPath(string attachmentId) =>
        Path.Combine(_attachDir, SafeName(attachmentId));

    public JobState LoadState()
    {
        if (!File.Exists(_statePath)) return new JobState();
        return JsonConvert.DeserializeObject<JobState>(File.ReadAllText(_statePath)) ?? new JobState();
    }

    public void SaveState(JobState state)
    {
        File.WriteAllText(_statePath, JsonConvert.SerializeObject(state, Formatting.Indented));
    }

    private static string SafeName(string key)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            key = key.Replace(c, '_');
        return key;
    }
}
