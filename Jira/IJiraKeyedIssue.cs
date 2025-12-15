namespace BensEngineeringMetrics.Jira;

/// <summary>
///     An interface used primarily with Basic Jira Types <see cref="BasicJiraInitiative" /> <see cref="BasicJiraPmPlan" /> <see cref="BasicJiraTicket" />.
///     Simply represents any ticket by surfacing only its key and type.
/// </summary>
public interface IJiraKeyedIssue
{
    string IssueType { get; }
    string Key { get; }
}
