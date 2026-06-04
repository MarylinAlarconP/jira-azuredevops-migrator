using System.Collections.Generic;
using System.IO;
using Migration.Engine.Reading;

namespace Migration.Engine.Storage;

public interface IStageStore
{
    void SaveRawIssue(RawIssue issue);
    IEnumerable<RawIssue> EnumerateRawIssues();

    void SaveAttachment(string attachmentId, byte[] bytes, string fileName);
    string AttachmentPath(string attachmentId);

    JobState LoadState();
    void SaveState(JobState state);
}
