namespace BensEngineeringMetrics;

public interface ISheetPieChart
{
    /// <summary>
    ///     Insert a pie chart using the provided data at the co-ordinates provided.
    /// </summary>
    /// <param name="sheetAndRange">Sheet1!A1</param>
    /// <param name="sourceData">The data to insert and base the chart on.</param>
    /// <param name="dataColumn">The column, 0-based index to insert the data at.</param>
    /// <param name="dataRow">The row, 0-based index to insert the data at.</param>
    /// <param name="chartTitle">The chart title</param>
    /// <param name="colors">
    ///     Optional array of colors to use for pie chart slices. Colors should be in hex format (e.g., "#FF0000") or RGB format (e.g., "rgb(255,0,0)"). If provided, colors will be applied
    ///     to slices in order.
    /// </param>
    Task InsertPieChart(string sheetAndRange, IList<IList<object?>> sourceData, int dataColumn, int dataRow, string chartTitle, System.Drawing.Color[]? colors = null);

    /// <summary>
    ///     Initialise the tool and authenticate with the API.
    /// </summary>
    /// <param name="sheetId">The sheet identifier. ie the Google Sheet ID.</param>
    /// <param name="sheetUpdater">An optional sheetUpdater can be provided to ensure the <see cref="ISheetPieChart" /> instance doesn't create its own.</param>
    Task Open(string sheetId, IWorkSheetUpdater? sheetUpdater = null);

    /// <summary>
    ///     Submit the batch of requests to edit and update charts in the Workbook.
    /// </summary>
    Task SubmitBatch();
}
