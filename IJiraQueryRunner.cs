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
    ///     Retrieve all open Product Initiatives from Jira.  A Product Initiative is a top level object, that
    ///     can have many children PMPLANs. Only Initiatives that are not Cancelled or Done are returned.
    /// </summary>
    Task<IEnumerable<JiraInitiative>> GetOpenInitiatives();

    Task<AgileSprint?> GetSprintById(int sprintId);

    Task<IReadOnlyList<dynamic>> SearchJiraIssuesWithJqlAsync(string jql, IFieldMapping[] fields);
}
