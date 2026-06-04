using System.Collections.Generic;

namespace Migration.Engine.Reading;

// Thin seam over Atlassian.SDK so the reader is unit-testable with a fake.
public interface IJiraClient
{
    IEnumerable<RawIssue> Search(string jql);          // paged internally
    byte[] DownloadAttachment(RawAttachment attachment);
}
