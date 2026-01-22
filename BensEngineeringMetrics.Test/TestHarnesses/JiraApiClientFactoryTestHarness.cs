using BensEngineeringMetrics.Jira;

namespace BensEngineeringMetrics.Test.TestHarnesses;

public class JiraApiClientFactoryTestHarness(string testLogName, IOutputter outputter) : IApiClientFactory
{
    private readonly JiraApiClientTestHarness scopedSingletonClient = new(outputter, testLogName);

    public JiraApiClient CreateJiraApiClient()
    {
        return this.scopedSingletonClient;
    }
}
