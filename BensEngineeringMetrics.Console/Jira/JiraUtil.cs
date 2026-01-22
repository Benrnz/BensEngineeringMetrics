namespace BensEngineeringMetrics.Jira;

public static class JiraUtil
{
    /// <summary>
    ///     Accepts a Jira Key string and returns a Google Sheet formula that will hyperlink to the ticket. Note that UserMode is required when using <see cref="IWorkSheetUpdater" />
    /// </summary>
    public static string HyperlinkDiscoTicket(string key)
    {
        return $"""=HYPERLINK("https://javlnsupport.atlassian.net/jira/polaris/projects/PMPLAN/ideas/view/6464278?selectedIssue={key}&issueViewSection=deliver", "{key}")""";
    }

    /// <summary>
    ///     Accepts a Jira Key string and returns a Google Sheet formula that will hyperlink to the ticket. Note that UserMode is required when using <see cref="IWorkSheetUpdater" />
    /// </summary>
    public static string HyperlinkTicket(string ticketKey)
    {
        return $"=HYPERLINK(\"https://javlnsupport.atlassian.net/browse/{ticketKey}\", \"{ticketKey}\")";
    }
}
