using System;
using System.Collections.Generic;
using System.Linq;
using Atlassian.Jira;
using Migration.Common;
using Newtonsoft.Json.Linq;
using RestSharp;

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
        // Atlassian.SDK's Attachment type exposes no content URL, so resolve the
        // absolute download URL through the REST attachment/{id} endpoint (mirrors
        // legacy JiraProvider.GetAttachmentInfo reading $.content), then fetch the
        // bytes via the same low-level RestClient.DownloadData the exporter uses.
        // API version 2 works on both Jira Cloud and Server/DC.
        var response = _jira.RestClient
            .ExecuteRequestAsync(Method.GET, $"rest/api/2/attachment/{attachment.Id}")
            .GetAwaiter().GetResult();
        var contentUrl = ((JObject)response).ExValue<string>("$.content");
        return _jira.RestClient.DownloadData(contentUrl);
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

        // Standard system fields. Only add when the SDK value is non-empty, and never
        // overwrite a custom field already captured under the same canonical key.
        void AddSystemField(string key, string? value)
        {
            if (!string.IsNullOrEmpty(value) && !raw.Fields.ContainsKey(key))
                raw.Fields[key] = value;
        }

        AddSystemField("summary", issue.Summary);
        AddSystemField("description", issue.Description);
        AddSystemField("assignee", issue.Assignee);
        AddSystemField("priority", issue.Priority?.Name);
        AddSystemField("status", issue.Status?.Name);
        AddSystemField("labels", issue.Labels != null ? string.Join(", ", issue.Labels) : null);
        AddSystemField("timeestimate", issue.TimeTrackingData?.RemainingEstimate);

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
