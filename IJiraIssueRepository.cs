using BensEngineeringMetrics.Jira;

namespace BensEngineeringMetrics;

public interface IJiraIssueRepository
{
    Task<IReadOnlyList<BasicJiraInitiative>> OpenInitiatives();

    Task<IReadOnlyList<BasicJiraPmPlan>> OpenPmPlans();
}
