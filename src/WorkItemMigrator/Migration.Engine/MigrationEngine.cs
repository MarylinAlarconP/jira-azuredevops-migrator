using System;
using System.Collections.Generic;
using System.Threading;
using Migration.Engine.Contracts;
using Migration.Engine.Linking;
using Migration.Engine.Preflight;
using Migration.Engine.Profiles;
using Migration.Engine.Reading;
using Migration.Engine.Reporting;
using Migration.Engine.Storage;
using Migration.Engine.Transforming;
using Migration.Engine.Writing;

namespace Migration.Engine;

public sealed class PreflightFailedException : Exception
{
    public PreflightFailedException(string message) : base(message) { }
}

public sealed class MigrationEngine
{
    private readonly Func<string, string, IStageStore> _storeFactory;
    private readonly IJiraReader _reader;
    private readonly ITransformer _transformer;
    private readonly IAdoWriter _writer;
    private readonly ILinker _linker;
    private readonly IAdoClient _ado;
    private readonly Func<string, MappingProfile> _profileLoader;

    public MigrationEngine(
        Func<string, string, IStageStore> storeFactory,
        IJiraReader reader, ITransformer transformer, IAdoWriter writer,
        ILinker linker, IAdoClient ado, Func<string, MappingProfile> profileLoader)
    {
        _storeFactory = storeFactory;
        _reader = reader;
        _transformer = transformer;
        _writer = writer;
        _linker = linker;
        _ado = ado;
        _profileLoader = profileLoader;
    }

    public JobReport RunMigration(JobRequest request, IProgress<MigrationEvent>? progress, CancellationToken ct)
    {
        var reporter = new ProgressReporter(progress);
        var store = _storeFactory(request.WorkspaceRoot, request.JobId);
        var profile = _profileLoader(request.ProfilePath);

        // Pre-flight (fail fast)
        reporter.Report(MigrationStage.Preflight, null, "validating target");
        var pre = new ReadinessPreflight(_ado).Check(request, profile);
        if (!pre.Ok)
            throw new PreflightFailedException("Target not ready:\n - " + string.Join("\n - ", pre.Problems));

        // Read
        reporter.Report(MigrationStage.Reading, null, "reading from Jira");
        var total = _reader.Read(request, store, reporter, ct);

        // Transform + Write
        var state = store.LoadState();
        var outcome = new WriteOutcome();
        var unmatched = new List<string>();
        var unmappedType = new List<string>();
        var legacyIds = new List<string>();

        foreach (var raw in store.EnumerateRawIssues())
        {
            ct.ThrowIfCancellationRequested();
            legacyIds.Add(raw.Key);
            var tr = _transformer.Transform(raw, profile, request);
            unmatched.AddRange(tr.UnmatchedUsers);

            if (tr.Skipped)
            {
                if (tr.SkipReason == "unmapped-type") unmappedType.Add(raw.Key);
                reporter.Report(MigrationStage.Transforming, raw.Key, tr.SkipReason ?? "skipped");
                continue;
            }

            if (request.DryRun) { outcome.Created++; continue; }

            _writer.Write(tr.Item!, store, state, outcome, ct);
            reporter.Report(MigrationStage.Writing, raw.Key, "written");
        }

        store.SaveState(state);

        // Link
        var links = request.DryRun
            ? new LinkReport()
            : _linker.Link(store.EnumerateRawIssues(), profile, ct);

        // Report
        reporter.Report(MigrationStage.Reporting, null, "building report");
        var builder = new ReportBuilder(_ado);
        var report = builder.Build(request.JobId, request.DryRun, total,
            legacyIds, outcome, links, unmatched, unmappedType);
        builder.WriteToWorkspace(report, System.IO.Path.Combine(request.WorkspaceRoot, "jobs", request.JobId));
        return report;
    }
}
