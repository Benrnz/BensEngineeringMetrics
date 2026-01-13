using BensEngineeringMetrics.Jira;

namespace BensEngineeringMetrics;

public interface IJiraQueryRunner
{
    /// <summary>
    ///     Gets all sprint numbers for a given board ID.
    /// </summary>
    /// <param name="boardId">The Jira Agile board ID</param>
    /// <returns>A list of sprint numbers (IDs)</returns>
    Task<IReadOnlyList<AgileSprint>> GetAllSprints(int boardId);

    Task<AgileSprint?> GetCurrentSprintForBoard(int boardId);

    /// <summary>
    ///     Get all children (select many) for each epic key provided.
    /// </summary>
    Task<IEnumerable<BasicJiraTicketWithParent>> GetEpicChildren(string[] epicKeys);

    /// <summary>
    ///     Retrieve all open Product Ideas from Jira.  A Product Idea is a grouping parent object that is a direct child of a Product Initiative.
    ///     A PmPlan Idea can have many Jira tickets as children that can be epics, stories, bugs, etc. These are not specific to BMS or Officetech.
    /// </summary>
    /// <param name="monthsOfClosedIdeasToFetch">
    ///     Defaults to 0, meaning only open initiatives will be fetched and included. Otherwise specify the number of months to go back and search
    ///     for recently closed initiatives.
    /// </param>
    Task<IEnumerable<BasicJiraPmPlan>> GetOpenIdeas(string optionalAdditionalJql = "", IFieldMapping[]? fields = null, int monthsOfClosedIdeasToFetch = 0);

    /// <summary>
    ///     Retrieve all open Product Initiatives from Jira.  A Product Initiative is a top level object, that
    ///     can have many children PMPLANs. Only Initiatives that are not Cancelled or Done are returned. These are not specific to BMS or Officetech.
    /// </summary>
    /// <param name="monthsOfClosedInitiativesToFetch">
    ///     Defaults to 0, meaning only open PmPlan Ideas will be fetched and included. Otherwise specify the number of months to go back and search
    ///     for recently closed PmPlans Ideas.
    /// </param>
    Task<IEnumerable<BasicJiraInitiative>> GetInitiatives(int monthsOfClosedInitiativesToFetch = 0);

    Task<AgileSprint?> GetSprintById(int sprintId);

    Task<IReadOnlyList<dynamic>> SearchJiraIssuesWithJqlAsync(string jql, IFieldMapping[] fields);
}
