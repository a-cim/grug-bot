using System;
using System.Threading;
using Discord;
using Discord.WebSocket;
using GrugBot.Core.Config;
using GrugBot.Core.Formatting;
using GrugBot.Core.Relaying;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace GrugBot;

/// <summary>
/// Discord → Game for chat: shows what people post in the chat channel to every player in General, with the
/// poster's Discord name in <c>DiscordNameColor</c> (05-message-format § Discord → Game).
/// </summary>
/// <remarks>
/// Messages arrive on a Discord.Net thread. Filtering and formatting are plain string work and happen there;
/// only the finished line goes to the game, on the main thread (gotcha #16). Lines sent this way don't raise
/// the game's <c>PlayerChat</c> event, which only fires for chat a player wrote, so <see cref="GeneralChatListener"/>
/// never sees them and nothing echoes back to Discord (gotcha #7). Our own bot's posts are ignored here as well.
/// </remarks>
public sealed class ChatChannelListener : IDisposable
{
    /// <summary>The category the game's frame profiler shows our main-thread tasks under.</summary>
    private const string MainThreadTaskCode = "grugbot-discord-to-general";

    private readonly ICoreServerAPI _api;
    private readonly ILogger _log;
    private readonly DiscordConnection _discord;
    private readonly DiscordToGameFilter _filter;
    private readonly string _nameColor;
    private bool _subscribed;

    // Read on Discord.Net threads and the game thread.
    private int _disposed;

    public ChatChannelListener(ICoreServerAPI api, ILogger log, DiscordConnection discord, GrugBotConfig config)
    {
        _api = api;
        _log = log;
        _discord = discord;
        _filter = new DiscordToGameFilter(config.IgnoreOtherBots, config.IgnoredDiscordUserIds);
        _nameColor = config.DiscordNameColor;

        if (!DiscordToGameFormatter.IsValidNameColor(_nameColor))
        {
            log.Warning(
                "DiscordNameColor in {0} is \"{1}\", which isn't a colour like {2}, so Discord names show in {2}.",
                GrugBotConfig.DisplayPath,
                _nameColor,
                GrugBotConfig.DefaultDiscordNameColor);
        }
    }

    /// <summary>Starts listening to the chat channel. Called once, from <c>StartServerSide</c>, before Discord connects.</summary>
    public void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }

        _discord.ChatChannelMessageReceived += OnChatChannelMessage;
        _subscribed = true;
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _disposed, 1);
        if (_subscribed)
        {
            _discord.ChatChannelMessageReceived -= OnChatChannelMessage;
            _subscribed = false;
        }
    }

    /// <summary>
    /// Runs on a Discord.Net thread. Never touches the game API: the line is handed to the main thread.
    /// <see cref="DiscordConnection"/> catches and logs anything thrown here.
    /// </summary>
    private void OnChatChannelMessage(SocketUserMessage message)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        // Discord says who we are before it sends any message. Until then we can't tell our own posts apart,
        // so nothing is relayed.
        ulong? botUserId = _discord.BotUserId;
        if (botUserId is null)
        {
            return;
        }

        SocketUser author = message.Author;
        // Discord.Net makes a webhook's author a SocketWebhookUser (with the message's WebhookId) and marks the
        // message's Source as Webhook; either is enough.
        bool isWebhook = author.IsWebhook || message.Source == MessageSource.Webhook;
        if (!_filter.ShouldRelay(author.Id, author.IsBot, isWebhook, botUserId.Value))
        {
            return;
        }

        string? line = DiscordToGameFormatter.Format(DiscordName(author), MessageText(message), _nameColor);
        if (line is null)
        {
            return;
        }

        _api.Event.EnqueueMainThreadTask(() => SendToGeneral(line), MainThreadTaskCode);
    }

    /// <summary>Runs on the main thread.</summary>
    private void SendToGeneral(string line)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        try
        {
            _api.SendMessageToGroup(GlobalConstants.GeneralChatGroup, line, EnumChatType.OthersMessage);
        }
        catch (Exception e)
        {
            _log.Error("grug-bot could not show a Discord message in General: {0}", e);
        }
    }

    /// <summary>
    /// The text to show for a message, before escaping. For now that's the content exactly as posted, so
    /// a message with only attachments or a sticker is empty and ignored. The Discord → Game rules go here
    /// (issue #5, decisions #25 and #41): mentions as <c>@Name</c>, <c>@Role</c> and <c>#channel</c>, custom emoji,
    /// attachments, stickers, newlines and the length cap.
    /// </summary>
    private static string MessageText(SocketUserMessage message) => message.Content ?? "";

    /// <summary>
    /// The Discord name (<c>CONTEXT.md</c>). Only a server member has a server nickname; a webhook or a user
    /// Discord.Net knows only globally doesn't.
    /// </summary>
    private static string DiscordName(SocketUser author) => DiscordToGameFormatter.DiscordName(
        (author as SocketGuildUser)?.Nickname,
        author.GlobalName,
        author.Username);
}
