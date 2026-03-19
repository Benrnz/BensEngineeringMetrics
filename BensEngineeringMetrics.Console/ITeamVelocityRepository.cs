namespace BensEngineeringMetrics;

/// <summary>
///     A repository to store and get team velocity information.  This is intentionally read, rather than calculated each time, as this allows manual control over the velocity target.
/// </summary>
public interface ITeamVelocityRepository
{
    /// <summary>
    ///     Get velocity by team id.
    /// </summary>
    /// <param name="teamId">For example: '1a05d236-1562-4e58-ae88-1ffc6c5edb32'</param>
    /// <returns>A double number to represent story points per sprint.  Or Null if the team id is not found.</returns>
    Task<double?> LookUpTeamVelocityById(string teamId);

    /// <summary>
    ///     Get velocity by team name.
    /// </summary>
    /// <param name="teamName">For example: 'Superclass'</param>
    /// <returns>A double number to represent story points per sprint.  Or Null if the team name is not found.</returns>
    Task<double?> LookUpTeamVelocityByName(string teamName);
}
