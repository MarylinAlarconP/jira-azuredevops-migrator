using System.Threading;
using Migration.Engine.Contracts;
using Migration.Engine.Storage;
using Migration.WIContract;

namespace Migration.Engine.Writing;

public interface IAdoWriter
{
    // writes one transformed item; updates state and report counters via the passed callbacks
    void Write(WiItem item, IStageStore store, JobState state, WriteOutcome outcome, CancellationToken ct);
}

public sealed class WriteOutcome
{
    public int Created;
    public int Skipped;
    public int Failed;
    public System.Collections.Generic.List<ItemFailure> Failures { get; } = new();
}
