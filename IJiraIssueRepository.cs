using BensEngineeringMetrics.Jira;

namespace BensEngineeringMetrics;

public interface IJiraIssueRepository
{
    Task MapPmPlanIdeasToInitiatives();

    Task<IReadOnlyList<BasicJiraInitiative>> OpenInitiatives();

    Task<(IReadOnlyList<BasicJiraInitiative> mappedInitiatives, IReadOnlyList<BasicJiraPmPlan> pmPlans)> OpenPmPlans();
}
