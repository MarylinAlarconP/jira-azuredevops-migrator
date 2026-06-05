using Migration.Engine.Contracts;
using Migration.Engine.Linking;
using Migration.Engine.Profiles;
using Migration.Engine.Reading;
using Migration.Engine.Storage;
using Migration.Engine.Transforming;
using Migration.Engine.Writing;

namespace Migration.Engine.Cli;

public static class EngineFactory
{
    public static MigrationEngine Create(JobRequest request)
    {
        var ado = new WitAdoClient(request.AdoOrgUrl, request.AdoProject, request.AdoPat);
        var jira = new AtlassianJiraClient(request.JiraBaseUrl, request.JiraEmail, request.JiraApiToken);
        return new MigrationEngine(
            storeFactory: (root, id) => new FileSystemStageStore(root, id),
            reader: new JiraReader(jira),
            transformer: new Transformer(),
            writer: new AdoWriter(ado),
            linker: new Linker(ado),
            ado: ado,
            profileLoader: ProfileLoader.Load);
    }
}
