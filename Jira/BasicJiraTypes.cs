namespace BensEngineeringMetrics.Jira;

public record BasicJiraInitiative(string Key, string Summary, string Status, bool RequiredForGoLive, IReadOnlyList<IJiraKeyedIssue> ChildPmPlans, string[] Customers);

public record BasicJiraPmPlan(string Key, string Summary, string Status, string IssueType, bool RequiredForGoLive, IReadOnlyList<IJiraKeyedIssue> ChildTickets, string[] Customers) : IJiraKeyedIssue;

public record BasicJiraTicket(string Key, string Summary, string Status, string IssueType)
    : IJiraKeyedIssue
{
    public string Project { get; init; } = Key.Split('-')[0];
}

public record BasicJiraTicketWithParent(string Key, string Summary, string Status, string IssueType, string Parent) : BasicJiraTicket(Key, Summary, Status, IssueType);
