using System;
using Migration.Engine.Contracts;
using Migration.Engine.Profiles;

namespace Migration.Engine.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("usage: migration-engine <migrate|seed-profiles> [options]");
            return 1;
        }

        return args[0] switch
        {
            "migrate" => Migrate(args),
            "seed-profiles" => Seed(args),
            _ => Unknown(args[0])
        };
    }

    private static int Unknown(string verb)
    {
        Console.Error.WriteLine($"unknown verb: {verb}");
        return 1;
    }

    private static int Seed(string[] args)
    {
        // migration-engine seed-profiles <configPath> <level> <outPath>
        if (args.Length < 4) { Console.Error.WriteLine("seed-profiles <configPath> <level> <outPath>"); return 1; }
        var level = Enum.Parse<MigrationLevel>(args[2], ignoreCase: true);
        ProfileSeeder.SeedToFile(args[1], level, args[3]);
        Console.WriteLine($"wrote profile {args[3]}");
        return 0;
    }

    private static int Migrate(string[] args)
    {
        // migration-engine migrate <jobRequestJsonPath>
        if (args.Length < 2) { Console.Error.WriteLine("migrate <jobRequestJsonPath>"); return 1; }
        var request = Newtonsoft.Json.JsonConvert.DeserializeObject<JobRequest>(
            System.IO.File.ReadAllText(args[1]))!;

        var progress = new Progress<MigrationEvent>(e =>
            Console.WriteLine($"[{e.Stage}] {e.ItemKey} {e.Message}"));

        var report = EngineFactory.Create(request)
            .RunMigration(request, progress, System.Threading.CancellationToken.None);

        Console.WriteLine(Migration.Engine.Reporting.ReportBuilder.Summarize(report));
        return report.Failed > 0 || report.ReconciliationMismatch ? 2 : 0;
    }
}
