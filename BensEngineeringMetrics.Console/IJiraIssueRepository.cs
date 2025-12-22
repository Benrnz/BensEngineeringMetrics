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
    (string? initiativeKey, IJiraKeyedIssue? foundTicket) FindTicketByKey(string key);

    /// <summary>
    ///     Retrieve all OPEN (not done, not cancelled) PMPLAN Initiatives from Jira. These are cached so repeated calls will return from cached data.
    /// </summary>
    /// <param name="monthsOfClosedInitiativesToFetch">
    ///     Defaults to 0, meaning only open initiatives will be fetched and included. Otherwise specify the number of months to go back and search
    ///     for recently closed initiatives.
    /// </param>
    Task<IReadOnlyList<BasicJiraInitiative>> GetInitiatives(int monthsOfClosedInitiativesToFetch = 0);

    /// <summary>
    ///     Retrieve all OPEN (not done, not cancelled) PMPLAN Ideas from Jira. These are cached so repeated calls will return from cached data.
    /// </summary>
    /// <param name="monthsOfClosedIdeasToFetch">
    ///     Defaults to 0, meaning only open PmPlan Ideas will be fetched and included. Otherwise specify the number of months to go back and search
    ///     for recently closed PmPlans Ideas.
    /// </param>
    Task<(IReadOnlyList<BasicJiraInitiative> mappedInitiatives, IReadOnlyList<BasicJiraPmPlan> pmPlans)> GetPmPlans(int monthsOfClosedIdeasToFetch = 0);

    IReadOnlyDictionary<string, string> LeafTicketToInitiativeMap();
}
