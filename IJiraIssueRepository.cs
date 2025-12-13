using BensEngineeringMetrics.Jira;

namespace BensEngineeringMetrics;

public interface IJiraIssueRepository
{
    void MapJiraIssuesToPmPlans(IReadOnlyList<IJiraKeyedIssue> issues);

    Task MapPmPlanIdeasToInitiatives();

    Task<IReadOnlyList<BasicJiraInitiative>> OpenInitiatives();

    Task<IReadOnlyList<BasicJiraPmPlan>> OpenPmPlans();
}
