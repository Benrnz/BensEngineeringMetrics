namespace BensEngineeringMetrics.Tasks;

public class ExportBugStatsTaskNzb(BugStatsWorkerNzb worker, IOutputter outputter) : IEngineeringMetricsTask
{
    // JAVPM Bug Analysis
    private const string JavPmGoogleSheetId = "1kAdXDPqn-avk21IZ7fLqKuqbs69UazKcns6M5VIol4I";
    private const string KeyString = "BUG_STATS_NZB";

    public string Key => KeyString;
    public string Description => "Export a series of exports summarising _bug_stats_ for JAVPM NZb Only.";

    public async Task ExecuteAsync(string[] args)
    {
        outputter.WriteLine($"{Key} - {Description}");
        outputter.WriteLine($"--------------------- {Constants.JavPmJiraProjectKey} ---------------------");
        await worker.UpdateSheet(Constants.JavPmJiraProjectKey, JavPmGoogleSheetId);
    }
}
