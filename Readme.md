Ben's Engineering Metrics Console
=================================

Purpose
-------
A lightweight command-line tool to run predefined metric calculations and data export tasks against the Jira, Slack, and git API's. The tool is intended for
repeatable automation and quick adhoc exports.

Key features
------------
- Discover and list available export tasks (implementations of `IJiraExportTask`).
- Run tasks interactively or run a specific task headlessly by passing the task key on the command line.
- Export results to CSV into a configurable output folder.
- Update Google Sheets with exported data (for tasks that support it).
- Easy to extend: add a new class that implements `IJiraExportTask` and the app will discover it at runtime.

Requirements
------------
- .NET 9 SDK (download from https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
  - On Windows you can install via:

  ```cmd
  winget install Microsoft.DotNet.SDK.9
  ```

Repository
----------
Clone the repository:

```cmd
git clone https://github.com/Benrnz/BensEngineeringMetrics.git
```

Setup
-----
1. Create a `Secrets.cs` file from the provided template `Secrets.rename-me.cs`.
   - Copy `Secrets.rename-me.cs` to `Secrets.cs` and place it in the same directory as `Program.cs`.
   - Populate it with your Jira account email and API token (the API token is available from your Atlassian account security settings).
   - Populate with any other required secrets (Slack token, Google API credentials) as needed.
   - `Secrets.cs` is intentionally excluded from source control; keep it private.

2. Build the project:

```cmd
cd "d:\Development\BensEngineeringMetrics"
dotnet build
```

Run
---
Interactive (lists available tasks and prompts for selection):

```cmd
dotnet run --project .\BensEngineeringMetricsConsole.csproj
```

Or run the built executable directly (example path for a Debug build):

```cmd
."\bin\Debug\net9.0\BensEngineeringMetricsConsole.exe"
```

Headless (run a task by its key)
--------------------------------
1. First run the app interactively (no args) to list tasks and their keys. Example output:

```
Jira Console Exporter tool.  Select a task to execute, or 'exit' to quit.
1: Calculate Overall PM Plan Release Burn Up (PMPLAN_RBURNUP)
```

2. Use the task key shown in the list to run the task directly. Example:

```cmd
."\bin\Debug\net9.0\BensEngineeringMetricsConsole.exe" PMPLAN_RBURNUP
```

Output files
------------
Exports are written to the configured output folder (default used by the app is `C:\Downloads\JiraExports`). Each task controls the file name and format of its export.

Extending the application
-------------------------
To add a new export task:
1. Implement the `IJiraExportTask` interface in a new public class in the project.
2. Include the class in the project build (add to the project if necessary).
3. At runtime the app discovers implementations via reflection; no extra registration is required.
4. Provide a stable task key (used for headless runs) and a concise description to help users identify the task.

Configuration & Secrets
-----------------------
- Place a `Secrets.cs` (copied from `Secrets.rename-me.cs`) next to `Program.cs` and keep it out of source control.
- The secrets file should contain your Jira email and API token for authentication.

Troubleshooting
---------------
- "Task not found" when running headless: verify the task key exactly matches the key printed by the app (case-sensitive).
- Authentication errors: double-check the Jira email and API token in `Secrets.cs` and that the token has not been revoked.
- Build errors: ensure the .NET 9 SDK is installed and that `dotnet --version` reports a compatible SDK.

Notes
-----
- This tool is designed for automated, repeatable exports rather than an interactive analytics UI.
- Keep `Secrets.cs` out of version control; a template `Secrets.rename-me.cs` is included in the repo.

Contributing
------------
Contributions welcome. Open issues or pull requests for bug fixes, new task templates, or improvements.

Contact
-------
Repository owner / maintainer: Ben (see repository on GitHub for issues and pull requests)
