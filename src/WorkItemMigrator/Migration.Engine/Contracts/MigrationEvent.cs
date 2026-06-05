namespace Migration.Engine.Contracts;

public enum MigrationStage
{
    Preflight,
    Reading,
    Transforming,
    Writing,
    Linking,
    Reporting
}

public sealed class MigrationEvent
{
    public MigrationEvent(MigrationStage stage, string? itemKey, string message)
    {
        Stage = stage;
        ItemKey = itemKey;
        Message = message;
    }

    public MigrationStage Stage { get; }
    public string? ItemKey { get; }
    public string Message { get; }
}
