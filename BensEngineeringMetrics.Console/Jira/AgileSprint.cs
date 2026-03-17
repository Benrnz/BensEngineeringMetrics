namespace BensEngineeringMetrics.Jira;

public record AgileSprint(
    int Id,
    string State,
    string Name,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    int BoardId,
    string Goal,
    DateTimeOffset CompleteDate)
{
    public static AgileSprint Default =>
        new(
            0,
            string.Empty,
            string.Empty,
            DateTimeOffset.MinValue,
            DateTimeOffset.MinValue,
            0,
            string.Empty,
            DateTimeOffset.MinValue);
}
