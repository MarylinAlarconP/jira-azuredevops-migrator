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

        // generic fields (skip MapTitle handled above and user/comment mappers handled elsewhere)
        foreach (var rule in profile.Fields)
        {
            if (rule.Mapper is "MapTitle" or "MapUser" or "MapToComments") continue;
            if (!issue.Fields.TryGetValue(rule.SourceName, out var raw)) continue;
            var mapped = profile.MapValue(rule.Target, raw);
            rev.Fields.Add(new WiField { ReferenceName = rule.Target, Value = mapped });
        }

        // author + created date (created-by resolved against the user map)
        var resolved = ResolveUser(issue.ReporterEmail, profile, out var matched);
        rev.Author = resolved;
        rev.Fields.Add(new WiField { ReferenceName = WiFieldReference.CreatedBy, Value = resolved });
        rev.Fields.Add(new WiField { ReferenceName = WiFieldReference.CreatedDate, Value = issue.Created });
        var unmatched = matched ? null : issue.ReporterEmail;

        foreach (var c in issue.Comments)
            rev.Fields.Add(new WiField
            {
                ReferenceName = WiFieldReference.History,
                Value = $"<b>{c.Author} ({c.Created:u}):</b> {c.Body}"
            });

        foreach (var a in issue.Attachments)
            rev.Attachments.Add(new WiAttachment
            {
                Change = ReferenceChangeType.Added,
                AttOriginId = a.Id,
                FilePath = a.FileName,
                Comment = ""
            });

        var item = new WiItem { Type = adoType, OriginId = issue.Key };
        item.Revisions = new System.Collections.Generic.List<WiRevision> { rev };

        var result = new TransformResult { Item = item };
        if (unmatched != null) result.UnmatchedUsers.Add(unmatched);
        return result;
    }

    private static string ResolveUser(string email, MappingProfile profile, out bool matched)
    {
        if (!string.IsNullOrWhiteSpace(email) && profile.Users.EmailToAdo.TryGetValue(email, out var ado))
        {
            matched = true;
            return ado;
        }
        matched = false;
        return profile.Users.Fallback;
    }
}
