using BensEngineeringMetrics.Jira;

namespace BensEngineeringMetrics.Test.TestHarnesses;

public class JiraApiClientFactoryTestHarness : IApiClientFactory
{
    private readonly JiraApiClientTestHarness scopedSingletonClient = new JiraApiClientTestHarness();

    public JiraApiClient CreateJiraApiClient(bool recordMode = false)
    {
        return this.scopedSingletonClient;
    }
}
