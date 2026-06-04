using System.Collections.Generic;
using System.Threading;
using Migration.Engine.Contracts;
using Migration.Engine.Profiles;
using Migration.Engine.Reading;
using Migration.Engine.Writing;

namespace Migration.Engine.Linking;

public sealed class Linker : ILinker
{
    private readonly IAdoClient _ado;
    public Linker(IAdoClient ado) => _ado = ado;

    public LinkReport Link(IEnumerable<RawIssue> issues, MappingProfile profile, CancellationToken ct)
    {
        var report = new LinkReport();
        foreach (var issue in issues)
        {
            foreach (var link in issue.Links)
            {
                ct.ThrowIfCancellationRequested();
                var label = $"{issue.Key} -[{link.JiraLinkType}]-> {link.TargetKey}";

                if (!profile.LinkMap.TryGetValue(link.JiraLinkType, out var rel))
                    continue; // unmapped link type: ignored in v1

                var src = _ado.FindByLegacyId(issue.Key);
                var tgt = _ado.FindByLegacyId(link.TargetKey);

                if (src.Count == 0 || tgt.Count == 0) { report.MissingEndpoint.Add(label); continue; }
                if (src.Count > 1 || tgt.Count > 1) { report.Ambiguous.Add(label); continue; }

                if (_ado.AddRelation(src[0], tgt[0], rel)) report.Created++;
                else report.SkippedExisting++;
            }
        }
        return report;
    }
}
