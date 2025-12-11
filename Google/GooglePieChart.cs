using System.Text.RegularExpressions;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Sheets.v4.Data;

namespace BensEngineeringMetrics.Google;

public class GooglePieChart(IWorkSheetUpdater sheetUpdater) : ISheetPieChart
{
    private const string ClientSecretsFile = "client_secret_apps.googleusercontent.com.json";
    private readonly List<Request> pendingSpreadsheetRequests = new();
    private readonly Dictionary<string, int> sheetNamesToIds = new();
    private UserCredential? credential;
    private string? googleSheetId;
    private IWorkSheetUpdater mySheetUpdater = sheetUpdater;

    public async Task Open(string sheetId, IWorkSheetUpdater? sheetUpdater1 = null)
    {
        if (!string.IsNullOrEmpty(this.googleSheetId) && this.credential is not null)
        {
            // Already initialised
            return;
        }

        Reset();
        if (sheetUpdater1 is not null)
        {
            // Sync with provided sheet updater, rather than use a new one.
            this.mySheetUpdater = sheetUpdater1;
        }

        await this.mySheetUpdater.Open(sheetId);
        this.googleSheetId = sheetId;
        await Authenticate();
    }

    public async Task SubmitBatch()
    {
        using var service = AuthHelper.InitiateService(this.googleSheetId, this.credential!);

        await this.mySheetUpdater.SubmitBatch();
        await ProcessBatchRequests();

        Reset();
    }

    public async Task InsertPieChart(string sheetAndRange, IList<IList<object?>> sourceData, int dataColumn, int dataRow, string chartTitle, System.Drawing.Color[]? colors = null)
    {
        ValidateSheetAndRange(sheetAndRange, out var sheetTab);

        this.mySheetUpdater.EditSheet(sheetAndRange, sourceData, true);

        var sheetTabId = await GetSheetIdByName(sheetTab);

        // Define the range for the Labels
        var endRowIndex = dataRow + sourceData.Count - 1;
        var labelSource = new ChartData
        {
            SourceRange = new ChartSourceRange
            {
                Sources = new List<GridRange>
                {
                    new() { SheetId = sheetTabId, StartRowIndex = dataRow, EndRowIndex = endRowIndex, StartColumnIndex = dataColumn, EndColumnIndex = 1 }
                }
            }
        };

        // Define the range for the Values
        var valueSource = new ChartData
        {
            SourceRange = new ChartSourceRange
            {
                Sources = new List<GridRange>
                {
                    new() { SheetId = sheetTabId, StartRowIndex = dataRow, EndRowIndex = endRowIndex, StartColumnIndex = dataColumn + 1, EndColumnIndex = 2 }
                }
            }
        };

        // Apply colors to data points if provided
        if (colors is not null && colors.Length > 0)
        {
            var dataPointCount = sourceData.Count - 1; // Exclude header row
            var dataPoints = new List<object>();

            for (var i = 0; i < dataPointCount; i++)
            {
                var colorIndex = i % colors.Length; // Cycle through colors if fewer colors than data points
                var rgbColor = colors[colorIndex].ToGoogleColor();

                dataPoints.Add(new
                {
                    ColorStyle = new ColorStyle
                    {
                        RgbColor = new Color { Red = rgbColor.Red, Green = rgbColor.Green, Blue = rgbColor.Blue }
                    }
                });
            }

            if (dataPoints.Any())
            {
                var dataPointOverridesProperty = typeof(ChartData).GetProperty("DataPointOverrides");
                dataPointOverridesProperty?.SetValue(valueSource, dataPoints);
            }
        }


        var chartRequest = new Request
        {
            AddChart = new AddChartRequest
            {
                Chart = new EmbeddedChart
                {
                    Spec = new ChartSpec
                    {
                        Title = chartTitle,
                        PieChart = new PieChartSpec
                        {
                            LegendPosition = "RIGHT_LEGEND",
                            Domain = labelSource,
                            Series = valueSource
                        }
                    },
                    // Position: Where the chart will appear (Anchored to )
                    Position = new EmbeddedObjectPosition
                    {
                        OverlayPosition = new OverlayPosition
                        {
                            AnchorCell = new GridCoordinate
                            {
                                SheetId = sheetTabId,
                                RowIndex = dataRow, // Aligned to top of data table
                                ColumnIndex = dataColumn + 3
                            },
                            OffsetXPixels = 0,
                            OffsetYPixels = 0
                        }
                    }
                }
            }
        };

        this.pendingSpreadsheetRequests.Add(chartRequest);
    }

    private async Task Authenticate()
    {
        this.credential ??= await AuthHelper.Authenticate(ClientSecretsFile);
    }

    /// <summary>
    ///     Gets the integer SheetId (gid) for a specific sheet tab name.
    /// </summary>
    private async Task<int?> GetSheetIdByName(string sheetName)
    {
        if (this.sheetNamesToIds.TryGetValue(sheetName, out var sheetId))
        {
            return sheetId;
        }

        if (sheetName.StartsWith('\'') && sheetName.EndsWith('\''))
        {
            // Remove surrounding single quotes
            sheetName = sheetName.Trim('\'');
        }

        using var service = AuthHelper.InitiateService(this.googleSheetId, this.credential!);
        var foundSheetId = await AuthHelper.GetSheetTabId(service, this.googleSheetId!, sheetName);

        if (foundSheetId is not null)
        {
            this.sheetNamesToIds.Add(sheetName, foundSheetId.Value);
            return foundSheetId.Value;
        }

        // Sheet name not found
        return null;
    }

    private async Task ProcessBatchRequests()
    {
        var batchRequest = new BatchUpdateSpreadsheetRequest
        {
            Requests = this.pendingSpreadsheetRequests
        };

        if (this.pendingSpreadsheetRequests.Any())
        {
            var service = AuthHelper.InitiateService(this.googleSheetId!, this.credential!);
            await service.Spreadsheets.BatchUpdate(batchRequest, this.googleSheetId).ExecuteAsync();
        }
    }

    private void Reset()
    {
        this.credential = null;
        this.googleSheetId = null;
        if (this.mySheetUpdater is GoogleSheetUpdater googleSheetUpdater)
        {
            googleSheetUpdater.Reset();
        }

        this.pendingSpreadsheetRequests.Clear();
    }

    private static void ValidateSheetAndRange(string sheetAndRange, out string sheetTab)
    {
        if (string.IsNullOrEmpty(sheetAndRange))
        {
            throw new ArgumentNullException(nameof(sheetAndRange));
        }

        if (sheetAndRange.Contains('!') is false)
        {
            throw new ArgumentException("The sheetAndRange parameter must include the sheet name followed by '!' and the range (e.g., 'Sheet1!A1:B5').", nameof(sheetAndRange));
        }

        sheetTab = sheetAndRange.Split('!')[0];
    }
}
