using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace BensEngineeringMetrics.Tasks;

public class MultiBatchTask(IServiceProvider serviceProvider, IOutputter writer) : IEngineeringMetricsTask
{
    private const string TaskKey = "BATCH";

    public string Description => "Run multiple tasks in a batch. Usage: BATCH [task1] [task2] ...";

    public string Key => TaskKey;

    public async Task ExecuteAsync(string[] args)
    {
        writer.WriteLine($"{Key} - {Description}");
        writer.WriteLine();

        // Get all registered task implementations from the IOC container
        var allTasks = serviceProvider.GetServices<IEngineeringMetricsTask>().ToList();

        if (!allTasks.Any())
        {
            writer.WriteLine("No tasks found in container.");
            return;
        }

        // Build a map of task keys to task instances for quick lookup
        var taskKeyMap = allTasks.ToDictionary(t => t.Key, t => t);

        // Parse args to identify batch tasks and their corresponding arguments
        var batchEntries = ParseBatchEntries(args, taskKeyMap);

        if (!batchEntries.Any())
        {
            writer.WriteLine("No valid tasks specified in batch.");
            return;
        }

        var stopWatch = Stopwatch.StartNew();
        writer.WriteLine($"{DateTime.Now} Executing {batchEntries.Count} tasks in batch:");
        writer.WriteLine();

        // Execute each task in sequence
        var successCount = 0;
        var failureCount = 0;

        foreach (var entry in batchEntries)
        {
            var task = taskKeyMap[entry.TaskKey];
            var taskArgs = entry.TaskArgs.ToArray();

            try
            {
                var sw = Stopwatch.StartNew();
                writer.WriteLine($"{entry.TaskKey} [{string.Join(' ', entry.TaskArgs)}] Starting execution...");
                await task.ExecuteAsync(taskArgs);
                sw.Stop();
                writer.WriteLine($"[{entry.TaskKey}] Completed successfully in {sw.Elapsed.TotalSeconds:N2} seconds.");
                successCount++;
            }
            catch (Exception ex)
            {
                writer.WriteLine($"[{entry.TaskKey}] Failed with error: {ex.Message}");
                failureCount++;
            }

            writer.WriteLine();
        }

        // Report summary
        stopWatch.Stop();
        writer.WriteLine($"{DateTime.Now} Batch execution complete: {successCount} succeeded, {failureCount} failed.");
        writer.WriteLine($"{stopWatch.Elapsed}");
    }

    /// <summary>
    ///     Parses the args array to identify batch tasks and their task-specific arguments.
    ///     Each task key marks the beginning of a new batch entry; all following arguments
    ///     until the next task key are considered arguments for that task.
    /// </summary>
    private List<BatchEntry> ParseBatchEntries(string[] args, Dictionary<string, IEngineeringMetricsTask> taskKeyMap)
    {
        var batchEntries = new List<BatchEntry>();
        var startIndex = args.Length > 0 ? 1 : 0; // Skip the "BATCH" key at index 0

        if (startIndex >= args.Length)
        {
            return batchEntries;
        }

        BatchEntry? currentEntry = null;

        for (var i = startIndex; i < args.Length; i++)
        {
            var arg = args[i];

            // Check if this argument is a valid task key
            if (taskKeyMap.ContainsKey(arg))
            {
                // If we have a current entry, add it to the list
                if (currentEntry != null)
                {
                    batchEntries.Add(currentEntry);
                }

                // Start a new batch entry with the task key and its own key as first argument
                currentEntry = new BatchEntry
                {
                    TaskKey = arg,
                    TaskArgs = [arg]
                };
            }
            else
            {
                // This is an argument for the current task
                if (currentEntry != null)
                {
                    currentEntry.TaskArgs.Add(arg);
                }
                else
                {
                    // Argument before any task key - log warning and skip
                    writer.WriteLine($"Warning: Skipping argument '{arg}' - no task key specified yet.");
                }
            }
        }

        // Don't forget to add the last entry
        if (currentEntry != null)
        {
            batchEntries.Add(currentEntry);
        }

        return batchEntries;
    }

    /// <summary>
    ///     Represents a single task entry in a batch with its key and task-specific arguments.
    /// </summary>
    private class BatchEntry
    {
        public List<string> TaskArgs { get; init; } = new();
        public string TaskKey { get; init; } = string.Empty;
    }
}
