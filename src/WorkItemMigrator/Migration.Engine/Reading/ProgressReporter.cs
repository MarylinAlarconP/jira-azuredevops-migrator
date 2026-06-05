using System;
using Migration.Engine.Contracts;

namespace Migration.Engine.Reading;

public sealed class ProgressReporter : IProgressReporter
{
    private readonly IProgress<MigrationEvent>? _progress;
    public ProgressReporter(IProgress<MigrationEvent>? progress) => _progress = progress;

    public void Report(MigrationStage stage, string? itemKey, string message) =>
        _progress?.Report(new MigrationEvent(stage, itemKey, message));
}
