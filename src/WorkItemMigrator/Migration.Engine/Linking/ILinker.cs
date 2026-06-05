using System.Collections.Generic;
using System.Threading;
using Migration.Engine.Contracts;
using Migration.Engine.Profiles;
using Migration.Engine.Reading;

namespace Migration.Engine.Linking;

public interface ILinker
{
    LinkReport Link(IEnumerable<RawIssue> issues, MappingProfile profile, CancellationToken ct);
}
