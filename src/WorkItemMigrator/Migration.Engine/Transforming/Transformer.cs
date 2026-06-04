using System.Linq;
using Migration.Engine.Contracts;
using Migration.Engine.Profiles;
using Migration.Engine.Reading;
using Migration.WIContract;

namespace Migration.Engine.Transforming;

public sealed class Transformer : ITransformer
{
    public const string LegacyIdField = "Custom.LegacyID";

    public TransformResult Transform(RawIssue issue, MappingProfile profile, JobRequest request)
    {
        if (!profile.TryMapType(issue.IssueType, out var adoType))
            return new TransformResult { Skipped = true, SkipReason = "unmapped-type" };

        var rev = new WiRevision { Index = 0, Time = issue.Created };
        rev.Fields.Add(new WiField { ReferenceName = LegacyIdField, Value = issue.Key });
        rev.Fields.Add(new WiField { ReferenceName = WiFieldReference.AreaPath, Value = request.BaseAreaPath });
        rev.Fields.Add(new WiField { ReferenceName = WiFieldReference.IterationPath, Value = request.BaseIterationPath });

        var titleRule = profile.Fields.FirstOrDefault(f => f.Mapper == "MapTitle");
        if (titleRule != null && issue.Fields.TryGetValue(titleRule.SourceName, out var title))
            rev.Fields.Add(new WiField { ReferenceName = WiFieldReference.Title, Value = title });

        var item = new WiItem { Type = adoType, OriginId = issue.Key };
        item.Revisions = new System.Collections.Generic.List<WiRevision> { rev };
        return new TransformResult { Item = item };
    }
}
