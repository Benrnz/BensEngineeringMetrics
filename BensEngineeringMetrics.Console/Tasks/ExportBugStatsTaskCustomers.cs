namespace BensEngineeringMetrics.Tasks;

public class ExportBugStatsTaskCustomers(BugStatsWorkerNzb workerNzb, BugStatsWorkerEnvest workerEnvest, IOutputter outputter) : IEngineeringMetricsTask
{
    // JAVPM Bug Analysis
    private const string NzbGoogleSheetId = "1kAdXDPqn-avk21IZ7fLqKuqbs69UazKcns6M5VIol4I";
    private const string EnvestGoogleSheetId = "1Na2kv5ADLwKZ6IVFV_nUY8BQhYccMiC3ioaYhLRmdwk";
    private const string KeyString = "BUG_STATS_CUSTOMERS";

    public string Key => KeyString;
    public string Description => "Export a series of exports summarising _bug_stats_ for JAVPM Major clients Only.";

    public async Task ExecuteAsync(string[] args)
    {
        outputter.WriteLine($"{Key} - {Description}");
        outputter.WriteLine($"--------------------- {Constants.JavPmJiraProjectKey} ---------------------");

        await workerNzb.UpdateSheet(Constants.JavPmJiraProjectKey, NzbGoogleSheetId);

        await workerEnvest.UpdateSheet(Constants.JavPmJiraProjectKey, EnvestGoogleSheetId);
    }
}
