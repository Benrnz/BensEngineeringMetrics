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
    Task InsertPieChart(string sheetAndRange, IList<IList<object?>> sourceData, int dataColumn, int dataRow, string chartTitle);

    /// <summary>
    ///     Initialise the tool and authenticate with the API.
    /// </summary>
    /// <param name="sheetId">The sheet identifier. ie the Google Sheet ID.</param>
    Task Open(string sheetId);

    /// <summary>
    ///     Submit the batch of requests to edit and update charts in the Workbook.
    /// </summary>
    Task SubmitBatch();
}
