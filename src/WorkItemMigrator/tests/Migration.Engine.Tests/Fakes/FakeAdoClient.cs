using System.Collections.Generic;
using System.Linq;
using Migration.Engine.Writing;

namespace Migration.Engine.Tests.Fakes;

public sealed class FakeAdoClient : IAdoClient
{
    private int _nextId = 1000;
    public List<AdoCreateRequest> Created { get; } = new();
    public Dictionary<string, List<int>> Existing { get; } = new();
    public HashSet<string> RejectLegacyIds { get; } = new();

    public IReadOnlyList<int> FindByLegacyId(string legacyId) =>
        Existing.TryGetValue(legacyId, out var ids) ? ids : new List<int>();

    public int CreateWorkItem(AdoCreateRequest request)
    {
        if (RejectLegacyIds.Contains(request.LegacyId))
            throw new AdoRejectedException($"rejected {request.LegacyId}");
        var id = _nextId++;
        Created.Add(request);
        if (!Existing.TryGetValue(request.LegacyId, out var list))
            Existing[request.LegacyId] = list = new List<int>();
        list.Add(id);
        return id;
    }

    public int CountByLegacyIds(IEnumerable<string> legacyIds) =>
        legacyIds.Sum(k => Existing.TryGetValue(k, out var l) ? l.Count : 0);

    public List<(int Source, int Target, string Rel)> Relations { get; } = new();

    public bool AddRelation(int sourceId, int targetId, string adoRelationReferenceName)
    {
        if (Relations.Any(r => r.Source == sourceId && r.Target == targetId && r.Rel == adoRelationReferenceName))
            return false;
        Relations.Add((sourceId, targetId, adoRelationReferenceName));
        return true;
    }
}
