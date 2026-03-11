using BensEngineeringMetrics.Slack;

namespace BensEngineeringMetrics;

public interface ISlackClient
{
    Task<IReadOnlyList<SlackChannel>> FindAllChannels(string partialChannelName);

    Task<IReadOnlyList<SlackMessage>> GetMessages(string channelId, int limitToNumberOfMessages = 10);

    /// <summary>
    ///     Join the specified Slack channel. This can be called safely even if the bot is already a member.
    /// </summary>
    Task<bool> JoinChannel(string channelId, bool isPrivate);

    /// <summary>
    ///     Post a message to a public Slack channel. For public channels, the app will join the channel if needed; for private channels, the app must already be in the channel (e.g. via invite).
    /// </summary>
    /// <param name="channelIdOrName">Channel ID (e.g. C123) or channel name (e.g. #channel-name).</param>
    /// <param name="text">Message text to post.</param>
    /// <returns>True if the message was sent successfully; otherwise false.</returns>
    Task<bool> SendMessageToChannel(string channelIdOrName, string text);
}
