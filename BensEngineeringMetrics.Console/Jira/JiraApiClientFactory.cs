namespace BensEngineeringMetrics.Jira;

internal class JiraApiClientFactory : IApiClientFactory
{
    public JiraApiClient CreateJiraApiClient(bool recordMode = false)
    {
        return new JiraApiClient(recordMode);
    }
}
