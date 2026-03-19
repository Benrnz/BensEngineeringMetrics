using BensEngineeringMetrics.Jira;

namespace BensEngineeringMetrics.Tasks;

/// <summary>
///     Reads team velocity information from a sheet source.  This is intentionally read, rather than calculated each time, as this allows manual control over the velocity target.
///     For example, if a team has recently changed in size. Velocities are read from:
///     https://docs.google.com/spreadsheets/d/1HuI-uYOtR66rs8B0qp8e3L39x13reFTaiOB3VN42vAQ/edit?gid=1708522897#gid=1708522897
/// </summary>
public class TeamVelocityRepository(IWorkSheetReader sheetReader) : ITeamVelocityRepository
{
    private const string SheetId = "1HuI-uYOtR66rs8B0qp8e3L39x13reFTaiOB3VN42vAQ";
    private const string TabName = "Summary";
    private readonly SemaphoreSlim initializationSemaphore = new(1, 1);
    private readonly Dictionary<string, double> teamVelocities = new();

    public async Task<double?> LookUpTeamVelocityById(string teamId)
    {
        return await LookUpTeamVelocityByName(JiraTeamConfig.Teams.Single(t => t.TeamId == teamId).TeamName);
    }

    public async Task<double?> LookUpTeamVelocityByName(string teamName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamName);

        if (this.teamVelocities.Count == 0)
        {
            await this.initializationSemaphore.WaitAsync();
            try
            {
                if (this.teamVelocities.Count == 0)
                {
                    await Initialize();
                }
            }
            finally
            {
                this.initializationSemaphore.Release();
            }
        }

        if (this.teamVelocities.TryGetValue(teamName, out var velocity))
        {
            return velocity;
        }

        return null;
    }

    private double GetVelocity(List<List<object>> data, int columnIndex)
    {
        // Velocity is expected to be in the next row down under the team name heading. It is expected to be two columns to the right.
        var velocityObj = data[1][columnIndex + 2];
        // Intentionally cautious about nulls here.  The Google API can return nulls for empty cells even though the API is defined with List<List<object>>.
        if (velocityObj == null || !double.TryParse(velocityObj.ToString(), out var velocity))
        {
            throw new InvalidDataException($"Velocity for team in column {columnIndex} is not a valid number. Sheet is not in the expected format.");
        }

        return velocity;
    }

    private async Task Initialize()
    {
        await sheetReader.Open(SheetId);
        var data = await sheetReader.ReadData($"{TabName}!A1:AD2");
        var columnIndex = 0;
        var teamColumnIndex = 0;
        string? teamName;
        do
        {
            if (columnIndex >= data[0].Count)
            {
                break;
            }

            // Intentionally cautious about nulls here.  The Google API can return nulls for empty cells even though the API is defined with List<List<object>>.
            teamName = data[0][columnIndex]?.ToString();
            if (string.IsNullOrWhiteSpace(teamName))
            {
                teamColumnIndex++;
                columnIndex++;
                if (columnIndex == 0)
                {
                    throw new InvalidDataException("There is meant to be a team name in the first column. Sheet is not in the expected format.");
                }

                continue;
            }

            teamColumnIndex = 0;
            var velocity = GetVelocity(data, columnIndex);
            this.teamVelocities.Add(teamName, velocity);
            if (teamName.Contains(' '))
            {
                this.teamVelocities.Add(teamName.Replace(" ", string.Empty), velocity);
            }

            columnIndex += 2;
            teamName = null;
        } while (string.IsNullOrWhiteSpace(teamName) && teamColumnIndex < 5);
    }
}
