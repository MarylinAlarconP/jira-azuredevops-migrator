using System.Collections.Generic;
using Migration.Engine.Contracts;
using Migration.Engine.Profiles;
using Migration.Engine.Reading;
using Migration.WIContract;

namespace Migration.Engine.Transforming;

public sealed class TransformResult
{
    public WiItem? Item { get; set; }
    public bool Skipped { get; set; }
    public string? SkipReason { get; set; }
    public List<string> UnmatchedUsers { get; set; } = new();
}

public interface ITransformer
{
    TransformResult Transform(RawIssue issue, MappingProfile profile, JobRequest request);
}
