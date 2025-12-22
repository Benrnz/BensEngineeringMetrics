namespace BensEngineeringMetrics.Jira;

internal interface IApiClientFactory
{
    JiraApiClient CreateJiraApiClient(bool recordMode = false);
}
