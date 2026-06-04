using System;
using System.Collections.Generic;
using Migration.Engine.Contracts;

namespace Migration.Engine.Tests.Fakes;

public sealed class SyncProgress : IProgress<MigrationEvent>
{
    private readonly List<MigrationEvent> _sink;
    public SyncProgress(List<MigrationEvent> sink) => _sink = sink;
    public void Report(MigrationEvent value) => _sink.Add(value);
}
