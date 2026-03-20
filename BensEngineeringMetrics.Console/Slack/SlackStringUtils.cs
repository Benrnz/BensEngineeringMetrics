namespace BensEngineeringMetrics.Slack;

public static class SlackStringUtils
{
    public static string? RemoveSlackSpecialCharacters(string? input)
    {
        if (input is null)
        {
            return null;
        }

        return input.Replace("```", string.Empty).Replace(":javln:", string.Empty);
    }
}
