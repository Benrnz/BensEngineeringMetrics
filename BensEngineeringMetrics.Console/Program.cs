using System.Reflection;
using BensEngineeringMetrics.Google;
using BensEngineeringMetrics.Jira;
using BensEngineeringMetrics.Slack;
using BensEngineeringMetrics.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BensEngineeringMetrics;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<App>();
            services.AddTransient<ICsvExporter, SimpleCsvExporter>();
            services.AddTransient<IJiraQueryRunner, JiraQueryDynamicRunner>();
            services.AddTransient<IGreenHopperClient, JiraGreenHopperClient>();
            services.AddTransient<ICloudUploader, GoogleDriveUploader>();
            services.AddTransient<IWorkSheetUpdater, GoogleSheetUpdater>();
            services.AddTransient<IWorkSheetReader, GoogleSheetReader>();
            services.AddSingleton<BugStatsWorker>();
            services.AddSingleton<BugStatsWorkerNzb>();
            services.AddTransient<ISlackClient, SlackClient>();
            services.AddTransient<ISheetPieChart, GooglePieChart>();
            services.AddSingleton<IJiraIssueRepository, JiraIssueRepository>();
            services.AddSingleton<IJsonToJiraBasicTypeMapper, JsonToJiraBasicTypeMapper>();
            //services.AddSingleton<IEnvestPmPlanStories, EnvestPmPlanStories>();
            services.AddTransient<IApiClientFactory, JiraApiClientFactory>();
            services.AddTransient<IOutputter, ConsoleOutputter>();

            // Find and Register all tasks
            foreach (var taskType in TaskTypes())
            {
                // Added as singletons to cache their data for repeated use in the same session.
                services.AddSingleton(taskType);
                services.AddSingleton<IEngineeringMetricsTask>(sp => (IEngineeringMetricsTask)sp.GetRequiredService(taskType));
            }
        });

        var host = builder.Build();
        var app = host.Services.GetRequiredService<App>();

        await app.Run(args);
    }

    private static IEnumerable<Type> TaskTypes()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetTypes().Where(type => typeof(IEngineeringMetricsTask).IsAssignableFrom(type) && type is { IsInterface: false, IsAbstract: false });
    }
}
