using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
/// <para>
/// The text follows the Discord → Game rules in <see cref="DiscordToGameText"/> (decisions #25 and #41). A mentioned
/// user Discord.Net doesn't have as a server member is looked up over REST on a background task, so no thread
/// waits for Discord. Lines still reach General in the order they were posted: a line waits for the lines
/// before it, never longer than <see cref="UserLookupTimeout"/>. Only new messages arrive here, so edits and
/// deletes are ignored, and a reply's content is just its own text.
/// </para>
/// </remarks>
public sealed class ChatChannelListener : IDisposable
{
    /// <summary>The category the game's frame profiler shows our main-thread tasks under.</summary>
    private const string MainThreadTaskCode = "grugbot-discord-to-general";

    /// <summary>
    /// How long the REST lookups for one message's mentioned users may take in all. Discord.Net's HTTP client has no
    /// timeout of its own and <c>RequestOptions.Timeout</c> doesn't cover the HTTP call, so a cancel token does it.
    /// </summary>
    private static readonly TimeSpan UserLookupTimeout = TimeSpan.FromSeconds(5);

    private readonly ICoreServerAPI _api;
    private readonly ILogger _log;
    private readonly DiscordConnection _discord;
    private readonly DiscordToGameFilter _filter;
    private readonly string _nameColor;
    private readonly CancellationTokenSource _stopping = new();
    private readonly object _deliveryLock = new();
    private bool _subscribed;

    // The delivery of the latest message; the next one waits for it. Never faults. Guarded by _deliveryLock.
    private Task _lastDelivery = Task.CompletedTask;

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
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            // Not disposed: a lookup still running may look at its token while it winds down.
            _stopping.Cancel();
        }

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

        string discordName = DiscordName(author);
        string content = message.Content ?? "";
        SocketGuild? guild = (message.Channel as SocketGuildChannel)?.Guild;

        // Server members Discord.Net already has are named at once (a nickname may be out of date without the
        // Server Members intent, decision #41). Anyone else is looked up first.
        var userNames = new Dictionary<ulong, string>();
        var toLookUp = new List<ulong>();
        foreach (ulong id in DiscordToGameText.FindUserMentionIds(content))
        {
            if (FindMentionedUser(message, id) is SocketGuildUser member)
            {
                userNames[id] = DiscordName(member);
            }
            else
            {
                toLookUp.Add(id);
            }
        }

        Task<string?> line = toLookUp.Count == 0
            ? Task.FromResult(FormatLine(message, discordName, content, guild, userNames))
            : Task.Run(() => LookUpThenFormatAsync(message, discordName, content, guild, userNames, toLookUp));

        Deliver(line);
    }

    /// <summary>
    /// Looks up each user in <paramref name="toLookUp"/> as a member of the server, then formats the line. Runs on
    /// a background task. A user the lookup can't find (not a member, Discord unreachable, out of time) gets the
    /// name in the message's own mention data, which has no server nickname; failing that, <c>@unknown-user</c>.
    /// </summary>
    private async Task<string?> LookUpThenFormatAsync(
        SocketUserMessage message,
        string discordName,
        string content,
        SocketGuild? guild,
        Dictionary<ulong, string> userNames,
        List<ulong> toLookUp)
    {
        using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token))
        {
            timeout.CancelAfter(UserLookupTimeout);
            var options = new RequestOptions { CancelToken = timeout.Token };

            foreach (ulong id in toLookUp)
            {
                IGuildUser? member = null;
                if (guild is not null && !timeout.IsCancellationRequested)
                {
                    try
                    {
                        // Checks Discord.Net's cache, then asks Discord for the member; null if they aren't one.
                        member = await ((IGuild)guild).GetUserAsync(id, CacheMode.AllowDownload, options).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        // Only the exception's type: text from Discord.Net isn't logged unredacted (decision #27).
                        _log.Debug("grug-bot could not look up the mentioned Discord user {0}: {1}", id, e.GetType().Name);
                    }
                }

                IUser? known = (IUser?)member ?? FindMentionedUser(message, id);
                if (known is not null)
                {
                    userNames[id] = DiscordName(known);
                }
            }
        }

        return FormatLine(message, discordName, content, guild, userNames);
    }

    /// <summary>The General line for a message, once every name that can be found is in <paramref name="userNames"/>.</summary>
    private string? FormatLine(
        SocketUserMessage message,
        string discordName,
        string content,
        SocketGuild? guild,
        Dictionary<ulong, string> userNames)
    {
        var lookups = new MentionLookups(
            id => userNames.TryGetValue(id, out string? name) ? name : null,
            id => guild?.GetRole(id)?.Name,
            id => guild?.GetChannel(id)?.Name);
        string text = DiscordToGameText.Build(content, lookups, message.Attachments.Count, message.Stickers.Count);
        return DiscordToGameFormatter.Format(discordName, text, _nameColor);
    }

    /// <summary>
    /// Hands a line to the main thread once it's ready and every earlier line has been handed over, so lines keep
    /// the order they were posted in. Doesn't wait: a line that is ready, behind lines already handed over, goes
    /// to the main thread before this returns.
    /// </summary>
    private void Deliver(Task<string?> line)
    {
        lock (_deliveryLock)
        {
            _lastDelivery = DeliverAfterAsync(_lastDelivery, line);
        }
    }

    /// <summary>Never throws, so the next message's delivery always goes ahead.</summary>
    private async Task DeliverAfterAsync(Task previous, Task<string?> line)
    {
        try
        {
            await previous.ConfigureAwait(false);
            string? ready = await line.ConfigureAwait(false);
            if (ready is not null && Volatile.Read(ref _disposed) == 0)
            {
                _api.Event.EnqueueMainThreadTask(() => SendToGeneral(ready), MainThreadTaskCode);
            }
        }
        catch (Exception e)
        {
            if (Volatile.Read(ref _disposed) == 0)
            {
                // The type and stack trace, not the message: it may hold text from Discord.Net (decision #27).
                _log.Error("grug-bot could not relay a Discord message to General: {0}{1}", e.GetType().FullName, e.StackTrace);
            }
        }
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
    /// The user with this ID in the message's own mention data, if Discord sent one: a <see cref="SocketGuildUser"/>
    /// if Discord.Net has them as a server member, else a user with no server nickname.
    /// </summary>
    private static SocketUser? FindMentionedUser(SocketUserMessage message, ulong id)
    {
        foreach (SocketUser user in message.MentionedUsers)
        {
            if (user is not null && user.Id == id)
            {
                return user;
            }
        }

        return null;
    }

    /// <summary>
    /// The Discord name (<c>CONTEXT.md</c>) of a poster or a mentioned user. Only a server member (an
    /// <see cref="IGuildUser"/>) has a server nickname; a user from the mention data that Discord.Net doesn't have
    /// as a member, or a user it knows only globally, doesn't.
    /// </summary>
    private static string DiscordName(IUser user) => DiscordToGameFormatter.DiscordName(
        (user as IGuildUser)?.Nickname,
        user.GlobalName,
        user.Username);
}
