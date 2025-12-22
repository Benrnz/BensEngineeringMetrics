using BensEngineeringMetrics.Jira;

namespace BensEngineeringMetrics.Test.TestHarnesses;

public class JiraApiClientTestHarness : JiraApiClient
{
    public override Task<string> PostSearchJqlAsync(string jql, string[] fields, string? nextPageToken = null)
    {
        // TODO
        return base.PostSearchJqlAsync(jql, fields, nextPageToken);
    }
}
