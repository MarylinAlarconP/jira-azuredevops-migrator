using Migration.Engine.Contracts;

namespace Migration.Engine.Reading;

public interface IProgressReporter
{
    void Report(MigrationStage stage, string? itemKey, string message);
}
