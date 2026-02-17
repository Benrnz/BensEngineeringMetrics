namespace BensEngineeringMetrics;

public interface IWorkSheetUpdater
{
    void AddSheet(string sheetName);

    void ApplyDateFormat(string sheetName, int column, string format);

    /// <summary>
    ///     Bold the text / values in a range of cells.
    ///     This is only async to be able to get the Tab ID from the Tab-Name, the action is still batched along with other changes.
    /// </summary>
    /// <param name="sheetName">The text name of the tab within the Google Sheet.</param>
    /// <param name="startRow">Zero-based index of the row to start at. This is inclusive.</param>
    /// <param name="endRow">Zero-based index, excluding this identified row. This is exclusive.</param>
    /// <param name="startColumn">Zero-based index of the column to start at. This is inclusive.</param>
    /// <param name="endColumn">Zero-based index, excluding this identified column. This is exclusive.</param>
    Task BoldCellsFormat(string sheetName, int startRow, int endRow, int startColumn, int endColumn);

    /// <summary>
    ///     Clear the sheet / range values.
    /// </summary>
    void ClearRange(string sheetName, string range = "A1:Z10000");

    /// <summary>
    ///     Clear formatting for a range of cells (resets to defaults). Optionally removes conditional formatting rules on that sheet.
    ///     This method is only async to be able to get the Tab ID from the Tab-Name, the action is still batched along with other changes.
    /// </summary>
    /// <param name="sheetName">The text name of the tab within the Google Sheet.</param>
    /// <param name="startRow">Zero-based index of the row to start at. This is inclusive.</param>
    /// <param name="endRow">Zero-based index, excluding this identified row. This is exclusive.</param>
    /// <param name="startColumn">Zero-based index of the column to start at. This is inclusive.</param>
    /// <param name="endColumn">Zero-based index, excluding this identified column. This is exclusive.</param>
    Task ClearRangeFormatting(string sheetName, int startRow, int endRow, int startColumn, int endColumn);

    void DeleteSheet(string sheetName);

    Task<bool> DoesSheetExist(string sheetName);

    /// <summary>
    ///     Edit a sheet and insert data provided by <paramref name="sourceData" />.
    /// </summary>
    /// <param name="sheetAndRange">'Sheet1!A1'</param>
    /// <param name="sourceData">data to be inserted into the sheet</param>
    /// <param name="userMode">Defaults to false.  If true, data is entered and interpreted by the workbook as if entered by the user.</param>
    void EditSheet(string sheetAndRange, IList<IList<object?>> sourceData, bool userMode = false);

    Task HideColumn(string sheetName, int column);

    /// <summary>
    ///     Edit a sheet and insert data provided in the CSV file.
    /// </summary>
    /// <param name="sheetAndRange">'Sheet1!A1'</param>
    /// <param name="csvFileName">The file to import</param>
    /// <param name="userMode">Defaults to false.  If true, data is entered and interpreted by the workbook as if entered by the user.</param>
    Task ImportFile(string sheetAndRange, string csvFileName, bool userMode = false);

    Task Open(string sheetId);

    Task PercentFormat(string sheetName, int startRow, int endRow, int startColumn, int endColumn, string pattern = "0.0%");

    /// <summary>
    ///     Submit all queued changes to Google Sheets in as few requests as possible.
    ///     Sends a single spreadsheet `BatchUpdateSpreadsheetRequest` for structural/formatting changes
    ///     and batches value updates/clears using the Values API.
    /// </summary>
    Task SubmitBatch();
}
