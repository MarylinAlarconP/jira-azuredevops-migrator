using System.Threading;
using Migration.Engine.Contracts;
using Migration.Engine.Storage;

namespace Migration.Engine.Reading;

public interface IJiraReader
{
    // Reads all issues for the request's JQL into the store, returns count read.
    int Read(JobRequest request, IStageStore store, IProgressReporter progress, CancellationToken ct);
}
