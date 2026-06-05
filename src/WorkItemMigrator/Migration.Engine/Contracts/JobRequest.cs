namespace Migration.Engine.Contracts;

public sealed class JobRequest
{
    public string JobId { get; set; } = System.Guid.NewGuid().ToString("N");

    // Jira source
    public string JiraBaseUrl { get; set; } = "";
    public string JiraEmail { get; set; } = "";
    public string JiraApiToken { get; set; } = "";
    public string Jql { get; set; } = "";

    // ADO target
    public string AdoOrgUrl { get; set; } = "";
    public string AdoProject { get; set; } = "";
    public string AdoPat { get; set; } = "";
    public string BaseAreaPath { get; set; } = "";
    public string BaseIterationPath { get; set; } = "";

    // Mapping selection
    public MigrationLevel Level { get; set; } = MigrationLevel.StoriesEtc;
    public string ProfilePath { get; set; } = "";

    // Workspace + options
    public string WorkspaceRoot { get; set; } = "";
    public bool DryRun { get; set; } = false;
    public bool TransientRetry { get; set; } = false;
}
