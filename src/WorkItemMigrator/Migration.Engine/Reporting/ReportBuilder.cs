using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Migration.Engine.Contracts;
using Migration.Engine.Writing;

namespace Migration.Engine.Reporting;

public sealed class ReportBuilder
{
    private readonly IAdoClient _ado;
    public ReportBuilder(IAdoClient ado) => _ado = ado;

    public JobReport Build(string jobId, bool dryRun, int totalInQuery,
        IEnumerable<string> legacyIds, WriteOutcome outcome, LinkReport links,
        List<string> unmatchedUsers, List<string> unmappedType)
    {
        var keys = legacyIds.Distinct().ToList();
        return new JobReport
        {
            JobId = jobId,
            DryRun = dryRun,
            TotalInQuery = totalInQuery,
            Created = outcome.Created,
            Skipped = outcome.Skipped,
            Failed = outcome.Failed,
            Failures = outcome.Failures,
            Links = links,
            UnmatchedUsers = unmatchedUsers.Distinct().ToList(),
            UnmappedType = unmappedType,
            AdoCountForLegacySet = dryRun ? outcome.Created + outcome.Skipped : _ado.CountByLegacyIds(keys)
        };
    }

    public void WriteToWorkspace(JobReport report, string jobDir)
    {
        Directory.CreateDirectory(jobDir);
        File.WriteAllText(Path.Combine(jobDir, "report.json"),
            JsonConvert.SerializeObject(report, Formatting.Indented));
        File.WriteAllText(Path.Combine(jobDir, "report.txt"), Summarize(report));
    }

    public static string Summarize(JobReport r)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Job {r.JobId}{(r.DryRun ? " (dry-run)" : "")}");
        sb.AppendLine($"  in query : {r.TotalInQuery}");
        sb.AppendLine($"  created  : {r.Created}");
        sb.AppendLine($"  skipped  : {r.Skipped}");
        sb.AppendLine($"  failed   : {r.Failed}");
        sb.AppendLine($"  unmapped-type : {r.UnmappedType.Count}");
        sb.AppendLine($"  links: created {r.Links.Created}, existing {r.Links.SkippedExisting}, " +
                      $"ambiguous {r.Links.Ambiguous.Count}, missing {r.Links.MissingEndpoint.Count}");
        sb.AppendLine($"  unmatched users : {r.UnmatchedUsers.Count}");
        sb.AppendLine($"  reconciliation: present {r.PresentCount} vs ADO {r.AdoCountForLegacySet} " +
                      $"{(r.ReconciliationMismatch ? "MISMATCH" : "ok")}");
        return sb.ToString();
    }
}
