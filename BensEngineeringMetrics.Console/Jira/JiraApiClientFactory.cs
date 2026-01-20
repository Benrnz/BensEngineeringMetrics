namespace BensEngineeringMetrics.Jira;

internal class JiraApiClientFactory : IApiClientFactory
{
    private readonly IOutputter outputter;
    private readonly bool record;

    public JiraApiClientFactory(IOutputter outputter)
    {
        this.record = false;
        this.outputter = outputter;
    }

    public JiraApiClientFactory(IOutputter outputter, bool record)
    {
        this.record = record;
        this.outputter = outputter;
    }

    public JiraApiClient CreateJiraApiClient()
    {
        return new JiraApiClient(this.outputter, this.record);
    }
}
