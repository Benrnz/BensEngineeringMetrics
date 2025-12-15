using BensEngineeringMetrics.Jira;

namespace BensEngineeringMetrics;

/// <summary>
///     The premise of this repository is that getting the full hierarchy of PMPLANs right down to epics and leaf tickets is difficult and expensive.
///     Leaf story tickets are not aware of PMPLANs, they may be linked to a parent epic, or not.  Only PMPLANs are aware of links to their children.
///     This repository downloads ALL open PMPLAN Initiatives and then enumerates and downloads their children.
///     The previous approach was using (issue in (linkedIssues(\"{0}\")) OR parent in (linkedIssues(\"{0}\"))) which generates many API calls, one per PMPLAN.
/// </summary>
public interface IJiraIssueRepository
{
    /// <summary>
    ///     Retrieve all OPEN (not done, not cancelled) PMPLAN Initiatives from Jira. These are cached so repeated calls will return from cached data.
    /// </summary>
    Task<IReadOnlyList<BasicJiraInitiative>> OpenInitiatives();

    /// <summary>
    ///     Retrieve all OPEN (not done, not cancelled) PMPLAN Ideas from Jira. These are cached so repeated calls will return from cached data.
    /// </summary>
    Task<(IReadOnlyList<BasicJiraInitiative> mappedInitiatives, IReadOnlyList<BasicJiraPmPlan> pmPlans)> OpenPmPlans();
}
