namespace BensEngineeringMetrics.Jira;

public interface IJiraIssueRepository
{
    Task<IReadOnlyList<BasicJiraInitiative>> OpenInitiatives();

    Task<IReadOnlyList<BasicJiraPmPlan>> OpenPmPlans();
}

public class JiraIssueRepository(IJiraQueryRunner runner) : IJiraIssueRepository
{
    private static readonly IFieldMapping[] InitiativeFields =
    [
        JiraFields.Summary,
        JiraFields.Status,
        JiraFields.IsReqdForGoLive
    ];

    private readonly List<BasicJiraInitiative> initiatives = new();
    private readonly List<BasicJiraPmPlan> pmPlans = new();

    public async Task<IReadOnlyList<BasicJiraInitiative>> OpenInitiatives()
    {
        if (this.initiatives.Any())
        {
            return this.initiatives;
        }

        this.initiatives.AddRange(await runner.GetOpenInitiatives());

        return this.initiatives;
    }

    public async Task<IReadOnlyList<BasicJiraPmPlan>> OpenPmPlans()
    {
        if (this.pmPlans.Any())
        {
            return this.pmPlans;
        }

        this.pmPlans.AddRange(await runner.GetOpenIdeas());

        return this.pmPlans;
    }
}
