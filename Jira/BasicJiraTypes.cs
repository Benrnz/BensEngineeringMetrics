namespace BensEngineeringMetrics.Jira;

// TODO can this be simplified to not have two children fields??
public record BasicJiraInitiative(string Key, string Summary, string Status, bool RequiredForGoLive, IJiraKeyedIssue[] PmPlanKeys, IReadOnlyList<BasicJiraPmPlan>? PmPlanIdeas = null);

public record BasicJiraPmPlan(string Key, string Summary, string Status, bool RequiredForGoLive, IReadOnlyList<IJiraKeyedIssue> ChildTickets);

public record BasicJiraTicket(string Key, string Summary, string Status, string IssueType) : IJiraKeyedIssue;

public record BasicJiraTicketWithParent(string Key, string Summary, string Status, string IssueType, string Parent) : BasicJiraTicket(Key, Summary, Status, IssueType);
