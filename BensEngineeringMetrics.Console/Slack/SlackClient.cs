using System.Net.Http.Json;
using System.Text.Json;

namespace BensEngineeringMetrics.Slack;

public class SlackClient(IOutputter outputter) : ISlackClient
{
    private const string BaseApiUrl = "https://slack.com/api/";
    private const string SubTypeChannelJoin = "channel_join";

    public async Task<IReadOnlyList<SlackChannel>> FindAllChannels(string partialChannelName)
    {
        if (string.IsNullOrWhiteSpace(partialChannelName))
        {
            throw new ArgumentException("Channel name cannot be null or empty.", nameof(partialChannelName));
        }

        var (allChannels, totalChannels) = await GetAllSlackChannels(partialChannelName);

        outputter.WriteLine($"Total channels retrieved and searched: {totalChannels}");
        outputter.WriteLine($"Found {allChannels.Count} channel(s) matching '{partialChannelName}'");

        // Join channels and fetch last message timestamp for each channel
        var channelsWithTimestamps = new List<SlackChannel>();
        var skippedChannels = 0;
        foreach (var channel in allChannels)
        {
            // Try to join the channel first (if not already a member)
            if (!await JoinChannel(channel.Id, channel.IsPrivate))
            {
                skippedChannels++;
            }

            // Now try to get the last message timestamp
            var lastMessageTimestamp = await GetLastMessageTimestampAsync(channel.Id);
            channelsWithTimestamps.Add(channel with { LastMessageTimestamp = lastMessageTimestamp });
        }

        if (skippedChannels > 0)
        {
            outputter.WriteLine($"Note: Could not retrieve timestamps for {skippedChannels} channel(s)");
        }

        return channelsWithTimestamps;
    }

    public async Task<bool> JoinChannel(string channelId, bool isPrivate)
    {
        // Private channels cannot be joined automatically - they require an invite
        if (isPrivate)
        {
            // For private channels, we can't auto-join, but we'll still try to access history
            // If the bot was previously invited, it will work
            return false;
        }

        try
        {
            var url = $"{BaseApiUrl}conversations.join?channel={Uri.EscapeDataString(channelId)}";

            var response = await App.HttpSlack.PostAsync(url, null);
            var responseContent = await response.Content.ReadAsStringAsync();
            var jsonDocument = JsonDocument.Parse(responseContent);

            if (jsonDocument.RootElement.TryGetProperty("ok", out var okProperty) && okProperty.GetBoolean())
            {
                return true;
            }

            // Check if bot is already in channel (this is fine)
            if (jsonDocument.RootElement.TryGetProperty("error", out var errorProperty))
            {
                var error = errorProperty.GetString();
                if (error == "already_in_channel")
                {
                    return true; // Already a member, treat as success
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<SlackMessage>> GetMessages(string channelId, int limitToNumberOfMessages = 10)
    {
        JsonDocument jsonDocument;
        try
        {
            var url = $"{BaseApiUrl}conversations.history?channel={Uri.EscapeDataString(channelId)}&limit={limitToNumberOfMessages}";

            var response = await App.HttpSlack.GetAsync(url);
            var responseContent = await response.Content.ReadAsStringAsync();
            jsonDocument = JsonDocument.Parse(responseContent);
        }
        catch (Exception ex)
        {
            // Any other errors - return null
            outputter.WriteLine(ex);
            return new List<SlackMessage>();
        }

        if (!jsonDocument.RootElement.TryGetProperty("ok", out var okProperty) || !okProperty.GetBoolean())
        {
            // Check for specific error types
            if (jsonDocument.RootElement.TryGetProperty("error", out var errorProperty))
            {
                var error = errorProperty.GetString();
                if (error is "not_in_channel" or "channel_not_found")
                {
                    // Bot is not a member of the channel - this is expected for some channels
                    return new List<SlackMessage>();
                }
            }

            // Other errors - return null
            return new List<SlackMessage>();
        }

        var messages = new List<SlackMessage>();
        if (jsonDocument.RootElement.TryGetProperty("messages", out var messagesProperty))
        {
            // Get the first (most recent) message
            foreach (var messageJson in messagesProperty.EnumerateArray())
            {
                var messageTimestamp = DateTimeOffset.MaxValue;
                if (messageJson.TryGetProperty("ts", out var tsProperty))
                {
                    var tsString = tsProperty.GetString();
                    if (!string.IsNullOrEmpty(tsString) && double.TryParse(tsString, out var timestamp))
                    {
                        // Convert Unix timestamp (seconds) to DateTimeOffset
                        messageTimestamp = DateTimeOffset.FromUnixTimeSeconds((long)timestamp).ToLocalTime();
                    }
                }

                ;
                messages.Add(new SlackMessage(
                    channelId,
                    messageJson.GetProperty("user").GetString()!,
                    messageJson.GetProperty("text").GetString()!,
                    messageTimestamp,
                    messageJson.TryGetProperty("type", out var typeProperty) ? typeProperty.GetString()! : string.Empty,
                    messageJson.TryGetProperty("subtype", out var subtypeProperty) ? subtypeProperty.GetString()! : string.Empty));
            }
        }

        return messages;
    }

    public async Task<bool> SendMessageToChannel(string channelIdOrName, string text)
    {
        if (string.IsNullOrWhiteSpace(channelIdOrName))
        {
            throw new ArgumentException("Channel ID or name cannot be null or empty.", nameof(channelIdOrName));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Message text cannot be null or empty.", nameof(text));
        }

        try
        {
            var responseContent = await PostChatMessageAndReadContentAsync(channelIdOrName, text);
            var (ok, error) = ParseChatPostMessageResponse(responseContent);

            if (ok)
            {
                return true;
            }

            if (error is "not_in_channel" or "channel_not_found")
            {
                var resolved = await TryResolveChannelAsync(channelIdOrName);
                if (resolved is { } r && !r.isPrivate && await JoinChannel(r.channelId, false))
                {
                    responseContent = await PostChatMessageAndReadContentAsync(channelIdOrName, text);
                    (ok, _) = ParseChatPostMessageResponse(responseContent);
                    if (ok)
                    {
                        return true;
                    }
                }
            }

            if (!string.IsNullOrEmpty(error))
            {
                outputter.WriteLine($"Slack chat.postMessage error: {error}");
            }

            return false;
        }
        catch (Exception ex)
        {
            outputter.WriteLine(ex.Message);
            return false;
        }
    }

    private static async Task<string> PostChatMessageAndReadContentAsync(string channelIdOrName, string text)
    {
        var payload = new { channel = channelIdOrName, text };
        var content = JsonContent.Create(payload);
        var response = await App.HttpSlack.PostAsync($"{BaseApiUrl}chat.postMessage", content);
        return await response.Content.ReadAsStringAsync();
    }

    private static (bool ok, string? error) ParseChatPostMessageResponse(string responseContent)
    {
        try
        {
            var jsonDocument = JsonDocument.Parse(responseContent);
            var ok = jsonDocument.RootElement.TryGetProperty("ok", out var okProperty) && okProperty.GetBoolean();
            var error = jsonDocument.RootElement.TryGetProperty("error", out var errorProperty) ? errorProperty.GetString() : null;
            return (ok, error);
        }
        catch
        {
            return (false, null);
        }
    }

    private static async Task<(List<SlackChannel> matchedChannels, int totalChannels)> GetAllSlackChannels(string partialChannelName)
    {
        var matchedChannels = new List<SlackChannel>();
        string? cursor = null;
        var totalChannels = 0;

        do
        {
            var url = $"{BaseApiUrl}conversations.list?types=public_channel&limit=1000&exclude_archived=true";
            if (!string.IsNullOrEmpty(cursor))
            {
                url += $"&cursor={Uri.EscapeDataString(cursor)}";
            }

            var response = await App.HttpSlack.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var jsonDocument = JsonDocument.Parse(responseContent);

            if (!jsonDocument.RootElement.TryGetProperty("ok", out var okProperty) || !okProperty.GetBoolean())
            {
                var error = jsonDocument.RootElement.TryGetProperty("error", out var errorProperty)
                    ? errorProperty.GetString()
                    : "Unknown error";
                throw new InvalidOperationException($"Slack API error: {error}");
            }

            if (jsonDocument.RootElement.TryGetProperty("channels", out var channelsProperty))
            {
                foreach (var channel in channelsProperty.EnumerateArray())
                {
                    totalChannels++;
                    if (channel.TryGetProperty("name", out var nameProperty))
                    {
                        var channelName = nameProperty.GetString();
                        if (!string.IsNullOrEmpty(channelName) && channelName.Contains(partialChannelName, StringComparison.OrdinalIgnoreCase))
                        {
                            var channelId = channel.TryGetProperty("id", out var idProperty) ? idProperty.GetString()! : "Unknown";
                            var isPrivate = channel.TryGetProperty("is_private", out var isPrivateProperty) && isPrivateProperty.GetBoolean();

                            matchedChannels.Add(new SlackChannel
                            (
                                channelId,
                                channelName,
                                isPrivate,
                                null // Will be populated after collecting all channels
                            ));
                            // Is Archived: channel.TryGetProperty("is_archived", out var isArchivedProperty) && isArchivedProperty.GetBoolean()
                        }
                    }
                }
            }

            // Check for next cursor in response_metadata
            cursor = null;
            if (jsonDocument.RootElement.TryGetProperty("response_metadata", out var responseMetadataProperty))
            {
                if (responseMetadataProperty.TryGetProperty("next_cursor", out var nextCursorProperty))
                {
                    var nextCursorValue = nextCursorProperty.GetString();
                    if (!string.IsNullOrEmpty(nextCursorValue))
                    {
                        cursor = nextCursorValue;
                    }
                }
            }
        } while (!string.IsNullOrEmpty(cursor));

        return (matchedChannels, totalChannels);
    }

    private static bool LooksLikeChannelId(string channelIdOrName)
    {
        if (string.IsNullOrEmpty(channelIdOrName) || channelIdOrName.Length < 8 || channelIdOrName.Length > 15)
        {
            return false;
        }

        var c = channelIdOrName[0];
        return (c == 'C' || c == 'G') && channelIdOrName.All(char.IsLetterOrDigit);
    }

    private async Task<(string channelId, bool isPrivate)?> TryResolveChannelAsync(string channelIdOrName)
    {
        if (LooksLikeChannelId(channelIdOrName))
        {
            try
            {
                var url = $"{BaseApiUrl}conversations.info?channel={Uri.EscapeDataString(channelIdOrName)}";
                var response = await App.HttpSlack.GetAsync(url);
                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonDocument = JsonDocument.Parse(responseContent);

                if (jsonDocument.RootElement.TryGetProperty("ok", out var okProperty) && okProperty.GetBoolean()
                    && jsonDocument.RootElement.TryGetProperty("channel", out var channelProperty))
                {
                    var channelId = channelProperty.TryGetProperty("id", out var idProperty) ? idProperty.GetString() : null;
                    var isPrivate = channelProperty.TryGetProperty("is_private", out var isPrivateProperty) && isPrivateProperty.GetBoolean();
                    if (!string.IsNullOrEmpty(channelId))
                    {
                        return (channelId, isPrivate);
                    }
                }
            }
            catch
            {
                // Fall through to return null
            }

            return null;
        }

        var name = channelIdOrName.TrimStart('#');
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return await TryFindChannelByExactNameAsync(name);
    }

    private static async Task<(string channelId, bool isPrivate)?> TryFindChannelByExactNameAsync(string channelName)
    {
        string? cursor = null;

        do
        {
            var url = $"{BaseApiUrl}conversations.list?types=public_channel,private_channel&limit=1000&exclude_archived=true";
            if (!string.IsNullOrEmpty(cursor))
            {
                url += $"&cursor={Uri.EscapeDataString(cursor)}";
            }

            var response = await App.HttpSlack.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var jsonDocument = JsonDocument.Parse(responseContent);

            if (!jsonDocument.RootElement.TryGetProperty("ok", out var okProperty) || !okProperty.GetBoolean())
            {
                return null;
            }

            if (jsonDocument.RootElement.TryGetProperty("channels", out var channelsProperty))
            {
                foreach (var channel in channelsProperty.EnumerateArray())
                {
                    if (channel.TryGetProperty("name", out var nameProperty))
                    {
                        var name = nameProperty.GetString();
                        if (string.Equals(name, channelName, StringComparison.OrdinalIgnoreCase))
                        {
                            var channelId = channel.TryGetProperty("id", out var idProperty) ? idProperty.GetString() : null;
                            var isPrivate = channel.TryGetProperty("is_private", out var isPrivateProperty) && isPrivateProperty.GetBoolean();
                            if (!string.IsNullOrEmpty(channelId))
                            {
                                return (channelId, isPrivate);
                            }
                        }
                    }
                }
            }

            cursor = null;
            if (jsonDocument.RootElement.TryGetProperty("response_metadata", out var responseMetadataProperty))
            {
                if (responseMetadataProperty.TryGetProperty("next_cursor", out var nextCursorProperty))
                {
                    var nextCursorValue = nextCursorProperty.GetString();
                    if (!string.IsNullOrEmpty(nextCursorValue))
                    {
                        cursor = nextCursorValue;
                    }
                }
            }
        } while (!string.IsNullOrEmpty(cursor));

        return null;
    }

    private async Task<DateTimeOffset?> GetLastMessageTimestampAsync(string channelId)
    {
        var messages = (await GetMessages(channelId, 3))
            .Where(m => m.SubType != SubTypeChannelJoin)
            .ToList();
        if (messages.Any())
        {
            return messages.First().LastMessageTimestamp;
        }

        return null;
    }
}
