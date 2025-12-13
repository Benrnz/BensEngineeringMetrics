namespace BensEngineeringMetrics.Jira;

public interface IJiraKeyedIssue
{
    string IssueType { get; }
    string Key { get; }
}
