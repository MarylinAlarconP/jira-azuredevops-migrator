using System;
using System.Collections.Generic;

namespace Migration.Engine.Reading;

public sealed class RawComment
{
    public string Author { get; set; } = "";
    public DateTime Created { get; set; }
    public string Body { get; set; } = "";
}

public sealed class RawAttachment
{
    public string Id { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentUrl { get; set; } = "";
}

public sealed class RawLink
{
    public string TargetKey { get; set; } = "";
    public string JiraLinkType { get; set; } = "";   // e.g. "relates to", "blocks"
    public bool IsParent { get; set; }
}

public sealed class RawIssue
{
    public string Key { get; set; } = "";
    public string IssueType { get; set; } = "";
    public string ReporterEmail { get; set; } = "";
    public DateTime Created { get; set; }

    public Dictionary<string, string> Fields { get; set; } = new();
    public List<RawComment> Comments { get; set; } = new();
    public List<RawAttachment> Attachments { get; set; } = new();
    public List<RawLink> Links { get; set; } = new();
}
