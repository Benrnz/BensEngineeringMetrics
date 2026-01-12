namespace BensEngineeringMetrics.Jira;

public static class JiraUtil
{
    public static string HyperlinkTicket(string ticketKey)
    {
        return $"=HYPERLINK(\"https://javlnsupport.atlassian.net/browse/{ticketKey}\", \"{ticketKey}\")";
    }

    public static string HyperlinkDiscoTicket(string key)
    {
        return $"""=HYPERLINK("https://javlnsupport.atlassian.net/jira/polaris/projects/PMPLAN/ideas/view/6464278?selectedIssue={key}&issueViewSection=deliver", "{key}")""";
    }
}
