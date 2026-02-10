namespace BensEngineeringMetrics.Jira;

public record TeamConfig(string TeamName, string TeamId, int BoardId, double MaxCapacity, string JiraProject);

public static class JiraConfig
{
    public static readonly TeamConfig[] Teams =
    [
        new("Superclass", Constants.TeamSuperclass, 419, 40, Constants.JavPmJiraProjectKey),
        new("Phantom", Constants.TeamPhantom, 1176, 10, Constants.JavPmJiraProjectKey),
        new("RubyDucks", Constants.TeamRubyDucks, 420, 50, Constants.JavPmJiraProjectKey),
        new("Spearhead", Constants.TeamSpearhead, 418, 70, Constants.JavPmJiraProjectKey),
        new("Officetech", Constants.TeamOfficetech, 483, 45, Constants.OtPmJiraProjectKey),
        new("Integration", Constants.TeamIntegration, 450, 50, Constants.JavPmJiraProjectKey)
    ];
}
