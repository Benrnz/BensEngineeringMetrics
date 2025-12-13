namespace BensEngineeringMetrics.Jira;

public record BasicJiraInitiative(string Key, string Summary, string Status, bool RequiredForGoLive, string[] PmPlanKeys);

public record BasicJiraPmPlan(string Key, string Summary, string Status, bool RequiredForGoLive, string[] ChildrenTicketKeys);
