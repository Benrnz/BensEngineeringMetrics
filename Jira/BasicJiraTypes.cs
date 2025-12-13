namespace BensEngineeringMetrics.Jira;

public record BasicJiraInitiative(string Key, string Summary, string Status, bool RequiredForGoLive, IJiraKeyedIssue[] PmPlanKeys, IReadOnlyList<BasicJiraPmPlan>? PmPlanIdeas = null);

public record BasicJiraPmPlan(string Key, string Summary, string Status, bool RequiredForGoLive, IJiraKeyedIssue[] ChildrenTicketKeys, IReadOnlyList<IJiraKeyedIssue>? ChildrenTickets = null);

public record BasicJiraTicket(string Key, string IssueType) : IJiraKeyedIssue;
