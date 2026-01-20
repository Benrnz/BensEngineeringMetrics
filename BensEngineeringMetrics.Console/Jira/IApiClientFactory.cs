namespace BensEngineeringMetrics.Jira;

internal interface IApiClientFactory
{
    JiraApiClient CreateJiraApiClient();
}
