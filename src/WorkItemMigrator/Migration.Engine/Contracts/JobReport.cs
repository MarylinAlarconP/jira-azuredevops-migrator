using System.Collections.Generic;

namespace Migration.Engine.Contracts;

public sealed class ItemFailure
{
    public string ItemKey { get; set; } = "";
    public MigrationStage Stage { get; set; }
    public string Reason { get; set; } = "";
}

public sealed class LinkReport
{
    public int Created { get; set; }
    public int SkippedExisting { get; set; }
    public List<string> Ambiguous { get; set; } = new();
    public List<string> MissingEndpoint { get; set; } = new();
}

public sealed class JobReport
{
    public string JobId { get; set; } = "";
    public bool DryRun { get; set; }

    public int TotalInQuery { get; set; }
    public int Created { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public List<string> UnmappedType { get; set; } = new();

    public List<ItemFailure> Failures { get; set; } = new();
    public LinkReport Links { get; set; } = new();
    public List<string> UnmatchedUsers { get; set; } = new();

    public int AdoCountForLegacySet { get; set; }

    // present = items that should exist in ADO after this run
    public int PresentCount => Created + Skipped;
    public bool ReconciliationMismatch => PresentCount != AdoCountForLegacySet;
}
