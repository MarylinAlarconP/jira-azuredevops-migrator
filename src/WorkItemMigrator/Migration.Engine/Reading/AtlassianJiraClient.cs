using System;
using System.Collections.Generic;
using System.Linq;
using Atlassian.Jira;

namespace Migration.Engine.Reading;

public sealed class AtlassianJiraClient : IJiraClient
{
    private readonly Jira _jira;

    public AtlassianJiraClient(string baseUrl, string email, string apiToken)
    {
        _jira = Jira.CreateRestClient(baseUrl, email, apiToken);
    }

    public IEnumerable<RawIssue> Search(string jql)
    {
        const int pageSize = 100;
        var start = 0;
        while (true)
        {
            var page = _jira.Issues.GetIssuesFromJqlAsync(jql, pageSize, start)
                .GetAwaiter().GetResult().ToList();
            if (page.Count == 0) yield break;

            foreach (var issue in page)
                yield return Map(issue);

            if (page.Count < pageSize) yield break;
            start += pageSize;
        }
    }

    public byte[] DownloadAttachment(RawAttachment attachment)
    {
        // Atlassian.SDK's Attachment type exposes no content URL, so Map leaves
        // RawAttachment.ContentUrl empty. The real download must go through the
        // REST endpoint attachment/{id} (as the legacy exporter does). Until that
        // is wired (harness phase), fail loud rather than throw an opaque empty-URI
        // error. See plan phase-13 deferred notes.
        throw new NotImplementedException(
            "AtlassianJiraClient.DownloadAttachment is not yet wired to the REST attachment endpoint.");
    }

    private static RawIssue Map(Issue issue)
    {
        var raw = new RawIssue
        {
            Key = issue.Key?.Value ?? "",
            IssueType = issue.Type?.Name ?? "",
            ReporterEmail = issue.Reporter ?? "",
            Created = issue.Created ?? DateTime.UtcNow
        };

        foreach (var field in issue.CustomFields)
            raw.Fields[field.Name] = field.Values != null ? string.Join(", ", field.Values) : "";

        foreach (var comment in issue.GetCommentsAsync().GetAwaiter().GetResult())
            raw.Comments.Add(new RawComment
            {
                Author = comment.Author ?? "",
                Created = comment.CreatedDate ?? DateTime.UtcNow,
                Body = comment.Body ?? ""
            });

        foreach (var att in issue.GetAttachmentsAsync().GetAwaiter().GetResult())
            raw.Attachments.Add(new RawAttachment
            {
                Id = att.Id ?? "",
                FileName = att.FileName ?? "",
                ContentUrl = ""   // Atlassian.SDK Attachment exposes no content URL
            });

        foreach (var link in issue.GetIssueLinksAsync().GetAwaiter().GetResult())
            raw.Links.Add(new RawLink
            {
                TargetKey = (link.OutwardIssue ?? link.InwardIssue)?.Key?.Value ?? "",
                JiraLinkType = link.LinkType?.Name ?? "",
                IsParent = false
            });

        return raw;
    }
}
