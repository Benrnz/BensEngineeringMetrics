using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Util.Store;

namespace BensEngineeringMetrics.Google;

internal static class AuthHelper
{
    private static readonly string[] Scopes = [SheetsService.Scope.Spreadsheets];

    public static async Task<UserCredential?> Authenticate(string clientSecretsFile)
    {
        //Console.WriteLine("Authenticating with Google Sheets API...");
        try
        {
            // Load the client secrets file for authentication.
            await using var stream = new FileStream(clientSecretsFile, FileMode.Open, FileAccess.Read);
            // The DataStore stores your authentication token securely.
            var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                (await GoogleClientSecrets.FromStreamAsync(stream)).Secrets,
                Scopes,
                "user",
                CancellationToken.None,
                new FileDataStore("Sheets.Api.Store"));
            return credential;
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine($"Error: The required file '{clientSecretsFile}' was not found.");
            Console.WriteLine("Please download it from the Google Cloud Console and place it next to the application executable.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during authentication: {ex.Message}");
        }

        return null;
    }

    public static async Task<bool> DoesSheetExist(UserCredential credential, string spreadsheetId, string sheetName)
    {
        try
        {
            using var service = InitiateService(spreadsheetId, credential!);

            // 1. Create a request to get the spreadsheet metadata.
            // We use a field mask to limit the response data to ONLY include sheet properties (title).
            // This makes the API call more performant by reducing the payload size.
            var getRequest = service.Spreadsheets.Get(spreadsheetId);
            getRequest.Fields = "sheets.properties.title"; // Field mask

            // 2. Execute the request.
            var spreadsheet = await getRequest.ExecuteAsync();

            // 3. Check if the Sheets collection exists and iterate through the sheets.
            if (spreadsheet.Sheets != null)
            {
                return spreadsheet.Sheets.Any(sheet => sheet.Properties.Title.Equals(sheetName));
            }

            // 4. If the loop completes without finding a match, the sheet does not exist.
            return false;
        }
        catch (Exception ex)
        {
            // Handle API errors, network issues, or invalid spreadsheet ID
            Console.WriteLine($"An error occurred: {ex.Message}");
            // Depending on your application's needs, you might want to rethrow the exception
            // or return false, assuming an error means the sheet couldn't be confirmed as existing.
            return false;
        }
    }

    public static async Task<int?> GetSheetTabId(SheetsService service, string sheetId, string sheetName)
    {
        var request = service.Spreadsheets.Get(sheetId);
        request.Fields = "sheets.properties";

        var spreadsheet = await request.ExecuteAsync();

        var sheet = spreadsheet.Sheets.FirstOrDefault(s => s.Properties.Title == sheetName);

        if (sheet is null)
        {
            // Sheet name not found
            return null;
        }

        return sheet.Properties.SheetId;
    }

    public static SheetsService InitiateService(string? sheetId, UserCredential credential)
    {
        SheetsService? service = null;
        try
        {
            if (string.IsNullOrEmpty(sheetId))
            {
                throw new InvalidOperationException("Google Sheet ID is not set. Call Open(sheetId) first.");
            }

            service = new SheetsService(new BaseClientService.Initializer { HttpClientInitializer = credential, ApplicationName = Constants.ApplicationName });
            return service;
        }
        catch
        {
            service?.Dispose();
            throw;
        }
    }
}
