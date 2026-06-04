using System.IO;
using System.Linq;
using System.Threading;
using Migration.Engine.Contracts;
using Migration.Engine.Storage;
using Migration.Engine.Transforming;
using Migration.WIContract;

namespace Migration.Engine.Writing;

public sealed class AdoWriter : IAdoWriter
{
    private readonly IAdoClient _ado;
    public AdoWriter(IAdoClient ado) => _ado = ado;

    public void Write(WiItem item, IStageStore store, JobState state, WriteOutcome outcome, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var rev = item.Revisions[0];
        var legacyId = rev.Fields.First(f => f.ReferenceName == Transformer.LegacyIdField).Value?.ToString() ?? "";

        if (_ado.FindByLegacyId(legacyId).Count > 0 || state.IsWritten(legacyId))
        {
            outcome.Skipped++;
            return;
        }

        var request = new AdoCreateRequest
        {
            Type = item.Type,
            LegacyId = legacyId,
            CreatedBy = ValueOf(rev, WiFieldReference.CreatedBy),
            CreatedDate = DateOf(rev, WiFieldReference.CreatedDate)
        };
        foreach (var f in rev.Fields)
        {
            if (f.ReferenceName == WiFieldReference.History) { request.CommentHtml.Add(f.Value?.ToString() ?? ""); continue; }
            if (f.Value != null) request.Fields[f.ReferenceName] = f.Value;
        }
        foreach (var att in rev.Attachments)
        {
            var path = Path.Combine(store.AttachmentPath(att.AttOriginId), att.FileName);
            request.AttachmentPaths.Add(path);
        }

        try
        {
            var id = _ado.CreateWorkItem(request);
            state.RecordWritten(legacyId, id);
            outcome.Created++;
        }
        catch (AdoRejectedException ex)
        {
            outcome.Failed++;
            outcome.Failures.Add(new ItemFailure { ItemKey = legacyId, Stage = MigrationStage.Writing, Reason = ex.Message });
            state.ItemStatus[legacyId] = "failed";
        }
    }

    private static string ValueOf(WiRevision rev, string field) =>
        rev.Fields.FirstOrDefault(f => f.ReferenceName == field)?.Value?.ToString() ?? "";

    private static System.DateTime DateOf(WiRevision rev, string field)
    {
        var v = rev.Fields.FirstOrDefault(f => f.ReferenceName == field)?.Value;
        return v is System.DateTime dt ? dt : System.DateTime.UtcNow;
    }
}
