using System.Collections.Generic;
using Migration.Engine.Contracts;
using Migration.Engine.Reading;

namespace Migration.Engine.Tests.Fakes;

public sealed class RecordingProgress : IProgressReporter
{
    public List<MigrationEvent> Events { get; } = new();
    public void Report(MigrationStage stage, string? itemKey, string message) =>
        Events.Add(new MigrationEvent(stage, itemKey, message));
}
