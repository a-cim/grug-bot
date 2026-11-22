using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using GrugBot.Core.Config;
using GrugBot.Core.Connection;
using GrugBot.Core.Logging;
using GrugBot.Core.Sending;
using ILogger = Vintagestory.API.Common.ILogger;

namespace GrugBot;

/// <summary>
/// The bot's connection to Discord, the send loop that posts queued lines to the chat channel and system
/// messages to the system channel, and the hand-off of messages posted in the chat channel
/// (<see cref="ChatChannelMessageReceived"/>).
/// </summary>
/// <remarks>
/// Nothing here may hold up the server (01-architecture § Lifecycle): logging in and sending run on
/// background tasks, and the game thread only queues lines (<see cref="EnqueueChat"/>, <see cref="EnqueueSystem"/>).
/// The one exception is <see cref="FlushBeforeShutdown"/>, which waits for the last lines with a timeout
/// (gotcha #18, CODING_STANDARDS § Threads and lifetime). Chat channel messages are handed over on a
/// Discord.Net thread, so whoever handles them must reach the game through <c>EnqueueMainThreadTask</c>
/// (gotcha #16). Handlers for game events are subscribed once by the mod system, never on Ready, which
/// Discord.Net raises again after some reconnects (gotcha #11); for the same reason nothing is queued on
/// Ready either. The token is never logged; any text from Discord.Net is passed through
/// <see cref="SecretRedactor"/> before it reaches the log (decision #27).
/// <para>
/// Lines are routed by <see cref="DiscordOutbox"/>: chat to the chat channel, system messages to the system
/// channel (the chat channel when <c>SystemChannelId</c> is 0), one queue and one batch per channel.
/// </para>
/// </remarks>
public sealed class DiscordConnection : IDisposable
{
    /// <summary>Exactly these intents and no others (decision #41). MessageContent is for Discord → Game.</summary>
    private const GatewayIntents Intents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent;

    /// <summary>
    /// Gateway close code for "disallowed intents": a privileged intent (for us, Message Content) isn't
    /// turned on in the Developer Portal. Discord.Net stops reconnecting after it.
    /// </summary>
    private const int DisallowedIntentsCloseCode = 4014;

    /// <summary>
    /// Gateway close code for "authentication failed" (Discord's gateway docs: don't reconnect): the token
    /// is wrong or was reset while connected. Discord.Net 3.18 has no case for it and reconnects forever.
    /// </summary>
    private const int AuthenticationFailedCloseCode = 4004;

    /// <summary>
    /// How long one token check may take. Discord.Net's HTTP client has no timeout of its own (the .NET
    /// default is 100 s) and <c>RequestOptions.Timeout</c> doesn't cover the HTTP call, so with the network
    /// silently blocked the first warning would otherwise take that long (test plan 1.5).
    /// </summary>
    private static readonly TimeSpan LoginCheckTimeout = TimeSpan.FromSeconds(20);

    /// <summary>How often queued lines are posted. Lines queued in between are joined (05-message-format § Batching).</summary>
    private static readonly TimeSpan SendInterval = TimeSpan.FromSeconds(1);

    private readonly ILogger _log;
    private readonly ResolvedToken _token;
    private readonly DiscordSocketClient _client;
    private readonly DiscordOutbox _outbox;
    private readonly CancellationTokenSource _stopping = new();

    // One drain at a time: the send loop's tick and the shutdown flush never post the same channel at once,
    // so lines keep their order.
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    // Written by Ready (a Discord.Net thread), read by the send loop and the shutdown flush. The system channel
    // is the same object as the chat channel when SystemChannelId is 0.
    private volatile IMessageChannel? _chatChannel;
    private volatile IMessageChannel? _systemChannel;

    // 1 once Discord has refused us for good (rejected token, Message Content intent off): nothing will
    // ever be sent, so stop queueing. Read on the game thread.
    private int _gaveUp;

    private int _readyCount;
    private int _disposed;

    /// <summary>
    /// Raised on a Discord.Net thread for each user message posted in the chat channel, including our own bot's,
    /// other bots' and webhooks': the handler decides what to ignore. Discord system messages (pins, joins) and
    /// other channels never get here. Handlers must be quick and must not call the game API directly (gotcha #16).
    /// Subscribe before <see cref="Start"/>.
    /// </summary>
    public event Action<SocketUserMessage>? ChatChannelMessageReceived;

    /// <summary>The user ID of our own bot, once Discord has said who we are (from the first Ready on); else <c>null</c>.</summary>
    public ulong? BotUserId => _client.CurrentUser?.Id;

    /// <param name="systemChannelId">The <c>SystemChannelId</c> setting: 0 sends system messages to the chat channel.</param>
    public DiscordConnection(ILogger log, ResolvedToken token, ulong chatChannelId, ulong systemChannelId)
    {
        if (token.Value is null)
        {
            throw new ArgumentException("A Discord token is needed to connect.", nameof(token));
        }

        _log = log;
        _token = token;
        _outbox = new DiscordOutbox(chatChannelId, systemChannelId);

        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = Intents,
            LogLevel = LogSeverity.Info,
        });
        _client.Log += OnDiscordLogAsync;
        _client.Ready += OnReadyAsync;
        _client.MessageReceived += OnMessageReceivedAsync;
    }

    /// <summary>
    /// Starts logging in and the send loop, and returns at once (fire-and-forget). A wrong token or an
    /// unreachable Discord only produces log lines; the server starts either way (test plan 1.4, 1.5).
    /// </summary>
    public void Start()
    {
        CancellationToken stopping = _stopping.Token;
        _ = Task.Run(() => LogInAsync(stopping));
        _ = Task.Run(() => RunSendLoopAsync(stopping));
    }

    /// <summary>
    /// Queues a finished line for the chat channel. Cheap and thread-safe; called on the game thread.
    /// Lines wait in the queue until Discord is ready.
    /// </summary>
    public void EnqueueChat(string line)
    {
        if (IsOff)
        {
            return;
        }

        _outbox.EnqueueChat(line);
    }

    /// <summary>
    /// Queues a finished system message (built by <c>SystemMessages</c>) for the system channel: <c>SystemChannelId</c>,
    /// or the chat channel when that is 0. Cheap and thread-safe. Like chat lines, it waits in the queue until
    /// Discord is ready (decision #23).
    /// </summary>
    public void EnqueueSystem(string line)
    {
        if (IsOff)
        {
            return;
        }

        _outbox.EnqueueSystem(line);
    }

    /// <summary>
    /// Posts everything still queued, and blocks the calling (game) thread until that's done or
    /// <paramref name="timeout"/> has passed, whichever comes first. For shutdown only: it's the one place the
    /// server waits on Discord, so the last lines aren't lost to the process exiting (gotcha #18, test plan 6.1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// It returns at once, without waiting, when nothing could be posted anyway: Discord isn't connected, or it
    /// rejected us (the relay gave up). Lines still waiting when the time is up are dropped; the sends in flight
    /// are cancelled.
    /// </para>
    /// <para>
    /// Whether anything is left to post is only decided under <see cref="_sendLock"/>, inside the bounded wait.
    /// A send loop tick may already have taken the shutdown message off the queue and be posting it: the flush
    /// waits for that tick to finish first, instead of finding the queue empty and letting <see cref="Dispose"/>
    /// cancel the send. Queues that are empty, or whose channel isn't known, are skipped there at no cost.
    /// </para>
    /// </remarks>
    public void FlushBeforeShutdown(TimeSpan timeout)
    {
        if (IsOff)
        {
            return;
        }

        if (_client.ConnectionState != ConnectionState.Connected)
        {
            if (!_outbox.IsEmpty)
            {
                _log.Notification("grug-bot isn't connected to Discord, so the lines still queued (such as the shutdown message) are dropped.");
            }

            return;
        }

        // Not disposed: if the wait times out, the flush may still be looking at its token while it winds down.
        var giveUp = new CancellationTokenSource();
        giveUp.CancelAfter(timeout);
        Task flush = Task.Run(() => FlushAsync(giveUp.Token));

        bool finished;
        try
        {
            // The only blocking wait in the mod, and it's bounded (CODING_STANDARDS § Threads and lifetime).
            // FlushAsync catches its own errors, so this only returns true or false.
            finished = flush.Wait(timeout);
        }
        catch (AggregateException)
        {
            finished = true;
        }

        if (!finished)
        {
            giveUp.Cancel();
            _log.Warning(
                "grug-bot could not post everything still queued (such as the shutdown message) to Discord within {0} seconds, so the rest was dropped.",
                timeout.TotalSeconds);
        }
        else if (!_outbox.IsEmpty)
        {
            // Left behind: lines for a channel Ready never found (an error was logged then), or the connection dropped.
            _log.Notification("grug-bot could not post some queued lines before shutdown (a Discord channel it can't find, or the connection dropped), so they are dropped.");
        }
    }

    /// <summary>
    /// Stops the background tasks and disconnects without blocking the caller. Anything still queued is
    /// dropped: call <see cref="FlushBeforeShutdown"/> first to post it. Safe to call more than once.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // Not disposed: the background tasks may still look at its token while they wind down.
        _stopping.Cancel();
        _client.MessageReceived -= OnMessageReceivedAsync;
        _client.Ready -= OnReadyAsync;
        _client.Log -= OnDiscordLogAsync;

        // LogoutAsync waits on the client's own state locks, and can wait there behind a gateway connect that
        // is still in progress. So stopping, logging out and disposing all happen on a background task, and
        // Dispose returns at once.
        DiscordSocketClient client = _client;
        _ = Task.Run(async () =>
        {
            try
            {
                await client.StopAsync();
                await client.LogoutAsync();
            }
            catch (Exception)
            {
                // Shutting down anyway; there's nothing useful to do with this.
            }
            finally
            {
                client.Dispose();
            }
        });
    }

    /// <summary>
    /// Checks the token, then starts the gateway. If Discord can't be reached, tries again later, so the bot
    /// connects once the network returns (test plan 1.5). After that, Discord.Net reconnects by itself.
    /// </summary>
    /// <remarks>
    /// <c>LoginAsync</c> only stores the token, and <c>StartAsync</c> only starts Discord.Net's connect loop,
    /// which retries a rejected token forever. So a REST call in between is what checks the token: a rejected
    /// one throws <see cref="HttpException"/> 401, and an unreachable Discord throws (for example)
    /// <c>HttpRequestException</c>, which Discord.Net doesn't retry. Calling <c>LoginAsync</c> again is safe; it
    /// logs out first.
    /// </remarks>
    private async Task LogInAsync(CancellationToken stopping)
    {
        int failures = 0;
        while (!stopping.IsCancellationRequested)
        {
            try
            {
                await _client.LoginAsync(TokenType.Bot, _token.Value!);
                using (CancellationTokenSource attempt = CancellationTokenSource.CreateLinkedTokenSource(stopping))
                {
                    attempt.CancelAfter(LoginCheckTimeout);
                    await _client.GetApplicationInfoAsync(new RequestOptions { CancelToken = attempt.Token });
                }

                await _client.StartAsync();
                _log.Debug("grug-bot logged in to Discord and is connecting to the gateway.");
                return;
            }
            catch (Exception e) when (IsRejectedToken(e))
            {
                GiveUpOnRejectedToken(e);
                return;
            }
            catch (Exception e) when (!stopping.IsCancellationRequested)
            {
                // Not shutting down, so a cancellation here is our own LoginCheckTimeout: a network failure
                // like any other, retried on the same schedule.
                failures++;
                TimeSpan delay = LoginRetryDelay.After(failures);
                string cause = e is OperationCanceledException
                    ? "no answer within " + (int)LoginCheckTimeout.TotalSeconds + " seconds"
                    : Redact(e.Message);
                const string RetryMessage = "grug-bot could not reach Discord and will try again in {0} seconds. The server carries on normally. Cause: {1}";
                if (failures == 1)
                {
                    _log.Warning(RetryMessage, (int)delay.TotalSeconds, cause);
                }
                else
                {
                    _log.Debug(RetryMessage, (int)delay.TotalSeconds, cause);
                }

                try
                {
                    await Task.Delay(delay, stopping);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
            catch (Exception)
            {
                // The server is shutting down; a failed login no longer matters.
                return;
            }
        }
    }

    /// <summary>A token Discord refuses won't work on a retry. Discord answers 401 for a wrong or reset token.</summary>
    private static bool IsRejectedToken(Exception? e) => e is HttpException { HttpCode: HttpStatusCode.Unauthorized };

    /// <summary>
    /// The gateway close Discord sent, if <paramref name="e"/> or an inner exception is one. Discord.Net wraps
    /// it in a <see cref="WebSocketException"/> before logging it.
    /// </summary>
    private static WebSocketClosedException? FindGatewayClose(Exception? e)
    {
        for (Exception? current = e; current is not null; current = current.InnerException)
        {
            if (current is WebSocketClosedException closed)
            {
                return closed;
            }
        }

        return null;
    }

    /// <summary>The cause names the close code and Discord's reason when there is one, not just the wrapper's message.</summary>
    private void GiveUpOnRejectedToken(Exception e) => GiveUp(
        "Discord rejected the bot token from {0}, so the relay is off. Check the token (it may have been reset in the Developer Portal) and restart the server. Cause: {1}",
        _token.DescribeSource(),
        Redact((FindGatewayClose(e) ?? e).Message));

    /// <summary>
    /// Stops for good after Discord has refused us: logs one error, stops queueing, drops what's queued
    /// and stops the client (on a background task) so Discord.Net doesn't keep retrying. Only the first
    /// call does anything.
    /// </summary>
    private void GiveUp(string format, params object[] args)
    {
        if (Interlocked.Exchange(ref _gaveUp, 1) != 0)
        {
            return;
        }

        _log.Error(format, args);
        _outbox.Clear();

        if (Volatile.Read(ref _disposed) == 0)
        {
            DiscordSocketClient client = _client;
            _ = Task.Run(async () =>
            {
                try
                {
                    await client.StopAsync();
                }
                catch (Exception)
                {
                    // Already stopping or disposed; nothing more to do.
                }
            });
        }
    }

    /// <summary>
    /// Runs after every (re)connect that sends Ready. It only looks up the chat and system channels; it
    /// subscribes and queues nothing, so a second Ready can't double anything up, "Server started" included
    /// (gotcha #11, test plan 1.7).
    /// </summary>
    private Task OnReadyAsync()
    {
        try
        {
            IMessageChannel? chatChannel = _client.GetChannel(_outbox.ChatChannelId) as IMessageChannel;
            IMessageChannel? systemChannel = _outbox.HasSeparateSystemChannel
                ? _client.GetChannel(_outbox.SystemChannelId) as IMessageChannel
                : chatChannel;
            _chatChannel = chatChannel;
            _systemChannel = systemChannel;
            bool first = Interlocked.Increment(ref _readyCount) == 1;
            string botName = _client.CurrentUser?.Username ?? "the bot";

            if (chatChannel is null)
            {
                _log.Error(
                    "grug-bot is connected to Discord as {0}, but can't find the chat channel {1} (ChatChannelId in {2}). Check the ID, and that the bot can view that channel. Lines for it wait until it can.",
                    botName,
                    _outbox.ChatChannelId,
                    GrugBotConfig.DisplayPath);
            }

            if (systemChannel is null && _outbox.HasSeparateSystemChannel)
            {
                _log.Error(
                    "grug-bot is connected to Discord as {0}, but can't find the system channel {1} (SystemChannelId in {2}; 0 uses the chat channel). Check the ID, and that the bot can view that channel. System messages wait until it can.",
                    botName,
                    _outbox.SystemChannelId,
                    GrugBotConfig.DisplayPath);
            }

            if (chatChannel is null || systemChannel is null)
            {
                return Task.CompletedTask;
            }

            if (!first)
            {
                _log.Debug("grug-bot's Discord connection is ready again after a reconnect.");
            }
            else if (_outbox.HasSeparateSystemChannel)
            {
                _log.Notification(
                    "grug-bot is connected to Discord as {0}, relays General to #{1} and posts system messages to #{2}.",
                    botName,
                    chatChannel.Name,
                    systemChannel.Name);
            }
            else
            {
                _log.Notification("grug-bot is connected to Discord as {0} and relays General to #{1}.", botName, chatChannel.Name);
            }
        }
        catch (Exception e)
        {
            _log.Error("grug-bot hit an error when Discord became ready: {0}", Redact(e.ToString()));
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Passes user messages posted in the chat channel to <see cref="ChatChannelMessageReceived"/>. Runs on a
    /// Discord.Net thread for every message the bot can see, so it only checks the channel and returns.
    /// </summary>
    private Task OnMessageReceivedAsync(SocketMessage message)
    {
        try
        {
            if (Volatile.Read(ref _disposed) != 0 || Volatile.Read(ref _gaveUp) != 0)
            {
                return Task.CompletedTask;
            }

            // A thread under the chat channel has its own ID, so it isn't relayed either.
            if (message is SocketUserMessage userMessage && message.Channel.Id == _outbox.ChatChannelId)
            {
                ChatChannelMessageReceived?.Invoke(userMessage);
            }
        }
        catch (Exception e)
        {
            // Never throw back into Discord.Net.
            _log.Error("grug-bot could not relay a Discord message to General: {0}", Redact(e.ToString()));
        }

        return Task.CompletedTask;
    }

    private async Task RunSendLoopAsync(CancellationToken stopping)
    {
        using var timer = new PeriodicTimer(SendInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stopping))
            {
                await _sendLock.WaitAsync(stopping);
                try
                {
                    await SendQueuedAsync(stopping);
                }
                catch (Exception e)
                {
                    // Once Dispose has cancelled the sends, an error here is only the client going away.
                    if (!stopping.IsCancellationRequested)
                    {
                        _log.Warning("grug-bot hit an error while sending to Discord: {0}", Redact(e.ToString()));
                    }
                }
                finally
                {
                    _sendLock.Release();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Dispose was called.
        }
    }

    /// <summary>
    /// The shutdown flush: waits for a send loop tick already in progress, then posts what's left. Never
    /// throws, so <see cref="FlushBeforeShutdown"/> only has to wait for it.
    /// </summary>
    private async Task FlushAsync(CancellationToken giveUp)
    {
        try
        {
            await _sendLock.WaitAsync(giveUp);
            try
            {
                await SendQueuedAsync(giveUp);
            }
            finally
            {
                _sendLock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // Out of time; FlushBeforeShutdown says so.
        }
        catch (Exception e)
        {
            _log.Warning("grug-bot hit an error while posting its last lines to Discord: {0}", Redact(e.ToString()));
        }
    }

    /// <summary>
    /// Posts everything queued since the last tick, joined into as few messages as fit, one channel at a time
    /// in parallel (rate limits are per channel). Waits (keeps the lines queued) while Discord isn't connected
    /// or a channel isn't known yet. Called with <see cref="_sendLock"/> held.
    /// </summary>
    private async Task SendQueuedAsync(CancellationToken cancel)
    {
        if (_client.ConnectionState != ConnectionState.Connected || _outbox.IsEmpty)
        {
            return;
        }

        var sends = new List<Task>(_outbox.ChannelIds.Count);
        foreach (ulong channelId in _outbox.ChannelIds)
        {
            IMessageChannel? channel = ChannelFor(channelId);
            if (channel is not null && !_outbox.IsEmptyFor(channelId))
            {
                sends.Add(SendChannelAsync(channel, _outbox.DrainMessages(channelId), cancel));
            }
        }

        await Task.WhenAll(sends);
    }

    private async Task SendChannelAsync(IMessageChannel channel, List<string> messages, CancellationToken cancel)
    {
        // The token reaches the HTTP call and Discord.Net's rate-limit waits, so a shutdown can cut them short.
        var options = new RequestOptions { CancelToken = cancel };
        foreach (string message in messages)
        {
            try
            {
                // No allowed mentions on any send (gotcha #8). Discord.Net already waits out rate limits
                // and retries; a message that still fails is dropped, with no retries of our own (decision #26).
                await channel.SendMessageAsync(message, options: options, allowedMentions: AllowedMentions.None);
            }
            catch (Exception e) when (IsRejectedToken(e))
            {
                // The token was reset while the server runs.
                GiveUpOnRejectedToken(e);
                return;
            }
            catch (Exception) when (cancel.IsCancellationRequested)
            {
                // Shutting down: out of time, or the client is going away.
                return;
            }
            catch (Exception e)
            {
                _log.Warning("grug-bot could not post a message to Discord, so it was dropped. Cause: {0}", Redact(e.Message));
            }
        }
    }

    /// <summary>The Discord channel for one of the outbox's channel IDs, or <c>null</c> until Ready has found it.</summary>
    private IMessageChannel? ChannelFor(ulong channelId) =>
        channelId == _outbox.ChatChannelId ? _chatChannel : _systemChannel;

    /// <summary>True once nothing will be sent any more: Discord refused us for good, or this was disposed.</summary>
    private bool IsOff => Volatile.Read(ref _gaveUp) != 0 || Volatile.Read(ref _disposed) != 0;

    /// <summary>Passes Discord.Net's own log lines to the server log, without the token.</summary>
    private Task OnDiscordLogAsync(LogMessage message)
    {
        try
        {
            // Discord.Net's connect loop would retry a rejected token (401 or close 4004) forever and stops on
            // 4014 with a cryptic error, so these get one clear line of our own instead. They're checked before
            // the reconnect-noise filter below, which would demote the 4004 warning to a debug line and leave
            // the relay silently dead.
            int? closeCode = FindGatewayClose(message.Exception)?.CloseCode;
            if (IsRejectedToken(message.Exception) || closeCode == AuthenticationFailedCloseCode)
            {
                // A token reset while the server runs: Discord closes the gateway with 4004 (or a REST call gets 401).
                GiveUpOnRejectedToken(message.Exception!);
                return Task.CompletedTask;
            }

            if (closeCode == DisallowedIntentsCloseCode)
            {
                GiveUp(
                    "Discord refused the connection because the bot's Message Content intent is off, so the relay is off. In the Discord Developer Portal, open the bot's application, go to Bot, turn on \"Message Content Intent\" under Privileged Gateway Intents, then restart the server.");
                return Task.CompletedTask;
            }

            string text = Redact(message.ToString(prependTimestamp: false));
            switch (message.Severity)
            {
                case LogSeverity.Critical:
                case LogSeverity.Error:
                    _log.Error("[Discord] {0}", text);
                    break;
                case LogSeverity.Warning when IsReconnectNoise(message.Exception):
                    _log.Debug("[Discord] {0}", text);
                    break;
                case LogSeverity.Warning:
                    _log.Warning("[Discord] {0}", text);
                    break;
                default:
                    _log.Debug("[Discord] {0}", text);
                    break;
            }
        }
        catch (Exception)
        {
            // Logging must never throw back into Discord.Net.
        }

        return Task.CompletedTask;
    }

    /// <summary>Gateway drops that Discord.Net recovers from by itself; as in th3essentials, they're only debug lines.</summary>
    private static bool IsReconnectNoise(Exception? e) =>
        e is GatewayReconnectException or WebSocketException or TaskCanceledException
        || e?.InnerException is WebSocketException;

    private string Redact(string text) => SecretRedactor.Redact(text, _token.Value);
}
