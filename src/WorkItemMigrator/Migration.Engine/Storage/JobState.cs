using System.Collections.Generic;
using System.Linq;

namespace Migration.Engine.Storage;

public sealed class JobState
{
    public string JobId { get; set; } = "";
    public string LastCompletedStage { get; set; } = "";

    // LegacyID -> list of ADO ids created for it (non-unique allowed)
    public Dictionary<string, List<int>> LegacyToAdoIds { get; set; } = new();

    // per-item status keyed by Jira key
    public Dictionary<string, string> ItemStatus { get; set; } = new();

    public void RecordWritten(string legacyId, int adoId)
    {
        if (!LegacyToAdoIds.TryGetValue(legacyId, out var list))
        {
            list = new List<int>();
            LegacyToAdoIds[legacyId] = list;
        }
        if (!list.Contains(adoId)) list.Add(adoId);
        ItemStatus[legacyId] = "written";
    }

    public bool IsWritten(string legacyId) =>
        LegacyToAdoIds.ContainsKey(legacyId);

    public IReadOnlyList<int> WrittenAdoIds(string legacyId) =>
        LegacyToAdoIds.TryGetValue(legacyId, out var list) ? list : new List<int>();
}
