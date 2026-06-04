namespace Migration.Engine.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            System.Console.WriteLine("usage: migration-engine <migrate|seed-profiles> [options]");
            return 1;
        }
        System.Console.WriteLine($"verb: {args[0]} (not yet implemented)");
        return 0;
    }
}
