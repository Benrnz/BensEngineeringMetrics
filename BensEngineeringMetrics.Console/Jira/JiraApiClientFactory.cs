namespace BensEngineeringMetrics.Jira;

internal class JiraApiClientFactory : IApiClientFactory
{
    private readonly bool record;

    public JiraApiClientFactory()
    {
        this.record = false;
    }

    public JiraApiClientFactory(bool record)
    {
        this.record = record;
    }

    public JiraApiClient CreateJiraApiClient()
    {
        return new JiraApiClient(this.record);
    }
}
