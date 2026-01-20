using BensEngineeringMetrics.Jira;
using BensEngineeringMetrics.Tasks;
using BensEngineeringMetrics.Test.TestHarnesses;
using NSubstitute;
using Xunit.Abstractions;

namespace BensEngineeringMetrics.Test;

// IS THIS THE RIGHT APPROACH??  WHY NOT GET THE DESIRED OUTPUT FOR A TASK AND VALIDATE THE NEW APPROACH PRODUCES THE SAME OUTPUT?
// This might be useful for isolated refactoring once data is pulled from Jira.  But anything that changes the order or data pulled from Jira this won't work well.
public class CalculatePmPlanReleaseBurnUpValuesTest(ITestOutputHelper testOutputHelper)
{
    private readonly IOutputter outputter = new TestOutputter(testOutputHelper);

    [Fact(Skip = "Only run manually")]
    //[Fact]
    public async Task Recorder()
    {
        // This test is intended to put the JiraApiClient into recording mode to capture live data for later playback during a test.
        // This approach allows for refactoring of downstream code from the API and ensure results are the same.

        var mapper = new JsonToJiraBasicTypeMapper();
        var factory = new JiraApiClientFactory(this.outputter, true);
        var runner = new JiraQueryDynamicRunner(mapper, factory);
        var envestPmPlanStories = new EnvestPmPlanStories(runner, this.outputter);
        // No need to export to csv, stub this out.
        var mockExporter = Substitute.For<ICsvExporter>();

        var sut = new CalculatePmPlanReleaseBurnUpValues(mockExporter, envestPmPlanStories, this.outputter);

        await sut.ExecuteAsync(["TASKID"]);
    }

    [Fact]
    public async Task Test1()
    {
        // This test is reliant on the above recorder to have been run and stored json files locally inside this test project.
        var mapper = new JsonToJiraBasicTypeMapper();
        var harnessFactory = new JiraApiClientFactoryTestHarness("CalculatePmPlanReleasesBurnUpValuesLogs", this.outputter); // The folder where the json log recorded files are stored.
        var runner = new JiraQueryDynamicRunner(mapper, harnessFactory);
        var envestPmPlanStories = new EnvestPmPlanStories(runner, this.outputter);
        var mockExporter = Substitute.For<ICsvExporter>();

        var sut = new CalculatePmPlanReleaseBurnUpValues(mockExporter, envestPmPlanStories, this.outputter);

        await sut.ExecuteAsync(["TASKID"]);

        var entireLog = ((TestOutputter)this.outputter).GetEntireLog();

        var totalWorkToBeDone = ExtractDoubleValue(entireLog, "Total work to be done:");
        var workCompleted = ExtractDoubleValue(entireLog, "Work completed:");
        var pmPlansWithHighLevelEstimatesOnly = ExtractIntValue(entireLog, "PmPlans with High level estimates only:");
        var pmPlansWithNoEstimate = ExtractIntValue(entireLog, "PmPlans with no estimate:");
        var pmPlansWithSpecedAndEstimated = ExtractIntValue(entireLog, "PmPlans with Spec'ed and Estimated:");
        var storiesWithNoEstimate = ExtractIntValue(entireLog, "Stories with no estimate:");
        var averageVelocity = ExtractDoubleValue(entireLog, "Average Velocity (last 6 weeks):");

        Assert.Equal(1733.05, totalWorkToBeDone);
        Assert.Equal(1486.05, workCompleted);
        Assert.Equal(28, pmPlansWithHighLevelEstimatesOnly);
        Assert.Equal(11, pmPlansWithNoEstimate);
        Assert.Equal(24, pmPlansWithSpecedAndEstimated);
        Assert.Equal(167, storiesWithNoEstimate);
        Assert.Equal(29.7, averageVelocity, 2);

        /*
         Data to validate this test against.  This was captured from the live run hitting the API and log files were created during that run.
            As at 20/01/2026
            Total work to be done: 1733.05
            Work completed: 1486.05
            PmPlans with High level estimates only: 28
            PmPlans with no estimate: 11
            PmPlans with Spec'ed and Estimated: 24
            Stories with no estimate: 167 / 292
            Average Velocity (last 6 weeks): 29.7 story points per sprint
         */
    }

    private static double ExtractDoubleValue(string log, string searchLabel)
    {
        var line = log.Split('\n').FirstOrDefault(l => l.Contains(searchLabel));

        if (line != null)
        {
            var valueStr = line.Split(':')[1].Trim();
            if (double.TryParse(valueStr, out var value))
            {
                return value;
            }

            var parts = valueStr.Split(' ');
            if (double.TryParse(parts[0], out var value2))
            {
                return value2;
            }
        }

        return 0.0;
    }

    private static int ExtractIntValue(string log, string searchLabel)
    {
        var line = log.Split('\n').FirstOrDefault(l => l.Contains(searchLabel));

        if (line != null)
        {
            var valueStr = line.Split(':')[1].Trim();
            // Handle cases where there might be additional text after the number (e.g., "167 / 292")
            var numericPart = valueStr.Split()[0];
            if (int.TryParse(numericPart, out var value))
            {
                return value;
            }
        }

        return 0;
    }
}
