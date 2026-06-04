using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;

namespace Migration.Engine.Writing;

public sealed class WitAdoClient : IAdoClient
{
    private readonly WorkItemTrackingHttpClient _wit;
    private readonly string _project;

    public WitAdoClient(string orgUrl, string project, string pat)
    {
        var conn = new VssConnection(new Uri(orgUrl), new VssBasicCredential(string.Empty, pat));
        _wit = conn.GetClient<WorkItemTrackingHttpClient>();
        _project = project;
    }

    public IReadOnlyList<int> FindByLegacyId(string legacyId)
    {
        var wiql = new Wiql
        {
            Query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{_project}' " +
                    $"AND [Custom.LegacyID] = '{legacyId.Replace("'", "''")}'"
        };
        var result = _wit.QueryByWiqlAsync(wiql, _project).GetAwaiter().GetResult();
        return result.WorkItems.Select(w => w.Id).ToList();
    }

    public int CreateWorkItem(AdoCreateRequest request)
    {
        var doc = new JsonPatchDocument();
        foreach (var f in request.Fields)
            doc.Add(new JsonPatchOperation { Operation = Operation.Add, Path = $"/fields/{f.Key}", Value = f.Value });
        doc.Add(new JsonPatchOperation { Operation = Operation.Add, Path = "/fields/System.CreatedBy", Value = request.CreatedBy });
        doc.Add(new JsonPatchOperation { Operation = Operation.Add, Path = "/fields/System.CreatedDate", Value = request.CreatedDate });
        doc.Add(new JsonPatchOperation { Operation = Operation.Add, Path = "/fields/System.ChangedBy", Value = request.CreatedBy });
        doc.Add(new JsonPatchOperation { Operation = Operation.Add, Path = "/fields/System.ChangedDate", Value = request.CreatedDate });
        foreach (var html in request.CommentHtml)
            doc.Add(new JsonPatchOperation { Operation = Operation.Add, Path = "/fields/System.History", Value = html });

        try
        {
            var created = _wit.CreateWorkItemAsync(
                document: doc,
                project: _project,
                type: request.Type,
                bypassRules: true,
                suppressNotifications: true).GetAwaiter().GetResult();
            var newId = created.Id ?? -1;
            if (newId <= 0)
                throw new AdoRejectedException($"ADO returned no work-item id for {request.LegacyId}");
            if (request.AttachmentPaths.Count > 0)
                UploadAttachments(newId, request.AttachmentPaths);
            return newId;
        }
        catch (Exception ex)
        {
            throw new AdoRejectedException(ex.Message);
        }
    }

    private void UploadAttachments(int workItemId, IEnumerable<string> paths)
    {
        var patch = new JsonPatchDocument();
        foreach (var path in paths)
        {
            if (!System.IO.File.Exists(path)) continue;
            using var stream = System.IO.File.OpenRead(path);
            var reference = _wit.CreateAttachmentAsync(stream, System.IO.Path.GetFileName(path), null, null, null, new CancellationToken())
                .GetAwaiter().GetResult();
            patch.Add(new JsonPatchOperation
            {
                Operation = Operation.Add,
                Path = "/relations/-",
                Value = new { rel = "AttachedFile", url = reference.Url }
            });
        }
        if (patch.Count > 0)
            _wit.UpdateWorkItemAsync(patch, workItemId, bypassRules: true).GetAwaiter().GetResult();
    }

    public int CountByLegacyIds(IEnumerable<string> legacyIds) =>
        legacyIds.Distinct().Sum(k => FindByLegacyId(k).Count);
}
