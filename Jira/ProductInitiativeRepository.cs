namespace BensEngineeringMetrics.Jira;

public record JiraInitiative(string Key, string Summary, string Status, bool RequiredForGoLive, string[] PmPlanKeys)
{
}

public record JiraPmPlan(string Key, string Summary, string Status, bool RequiredForGoLive, string[] ChildrenTicketKeys);

public interface IJiraIssueRepository
{
    Task<IReadOnlyList<JiraInitiative>> OpenInitiatives();
}

public class JiraIssueRepository(IJiraQueryRunner runner) : IJiraIssueRepository
{
    private static readonly IFieldMapping[] InitiativeFields =
    [
        JiraFields.Summary,
        JiraFields.Status,
        JiraFields.IsReqdForGoLive
    ];

    private readonly List<JiraInitiative> initiatives = new();

    public async Task<IReadOnlyList<JiraInitiative>> OpenInitiatives()
    {
        if (this.initiatives.Any())
        {
            return this.initiatives;
        }

        this.initiatives.AddRange(await runner.GetOpenInitiatives());

        return this.initiatives;
    }
}
