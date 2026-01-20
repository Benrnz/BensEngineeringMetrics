using BensEngineeringMetrics.Jira;

namespace BensEngineeringMetrics.Test.TestHarnesses;

public class JiraApiClientFactoryTestHarness(string testLogName) : IApiClientFactory
{
    private readonly JiraApiClientTestHarness scopedSingletonClient = new(testLogName);

    public JiraApiClient CreateJiraApiClient()
    {
        return this.scopedSingletonClient;
    }
}
