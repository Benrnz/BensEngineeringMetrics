namespace BensEngineeringMetrics.Jira;

/// <summary>
///     A team structure to contain information about teams for reporting.
/// </summary>
/// <param name="TeamName">A short string text name of the team as you'd like it to appear on the report.</param>
/// <param name="JiraName">The exact team name as it appears in Jira so queries can be written with exact matching.</param>
/// <param name="TeamId">The Jira Team id value.  Use <see cref="Constants" />.</param>
/// <param name="BoardId">The Jira Board id to find the Scrum board.</param>
/// <param name="MaxCapacity">The maximum theoretical capacity of the team. Number of people x hours in the sprint.</param>
/// <param name="JiraProject">The Jira project key.</param>
/// <param name="StartDate">A start date for the team's sprint. Can be any date in the past that indicates which day of the week sprints start and end.</param>
/// <param name="UsesStoryPoints">If the team uses an integer-based story points field or a timespan-based field (like OriginalEstimate).</param>
public record TeamConfig(
    string TeamName,
    string JiraName,
    string TeamId,
    int BoardId,
    double MaxCapacity,
    string JiraProject,
    DateOnly StartDate,
    bool UsesStoryPoints = false,
    string? SlackChannel = null);

public static class JiraTeamConfig
{
    public static readonly TeamConfig[] Teams =
    [
        new("Superclass", "Superclass Team", Constants.TeamSuperclass, 419, 40, Constants.JavPmJiraProjectKey, new DateOnly(2026, 1, 12), SlackChannel: "eng-team-superclass"),
        new("Phantom", "Phantom Team", Constants.TeamPhantom, 1211, 10, Constants.JavPmJiraProjectKey, new DateOnly(2026, 1, 12), true, "eng-team-superclass"),
        new("Wizards", "The Wizards Team", Constants.TeamWizards, 420, 50, Constants.JavPmJiraProjectKey, new DateOnly(2026, 1, 12), SlackChannel: "eng-team-ruby-ducks"),
        new("Spearhead", "Spearhead Team", Constants.TeamSpearhead, 418, 70, Constants.JavPmJiraProjectKey, new DateOnly(2026, 1, 12), SlackChannel: "eng-team-spearhead"),
        new("Officetech", "Officetech Team", Constants.TeamOfficetech, 483, 45, Constants.OtPmJiraProjectKey, new DateOnly(2026, 1, 12), true, "eng-team-officetech"),
        new("Integration", "Integration Team", Constants.TeamIntegration, 450, 50, Constants.JavPmJiraProjectKey, new DateOnly(2026, 1, 12))
    ];
}
