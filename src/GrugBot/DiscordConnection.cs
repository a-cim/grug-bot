using System;
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
/// The bot's connection to Discord, and the send loop that posts queued lines to the chat channel.
/// </summary>
/// <remarks>
/// Nothing here may hold up the server (01-architecture § Lifecycle): logging in and sending run on
/// background tasks, the game thread only ever calls <see cref="EnqueueChat"/>, and nothing waits on a
/// Discord task. Handlers for game events are subscribed once by the mod system, never on Ready, which
/// Discord.Net raises again after some reconnects (gotcha #11). The token is never logged; any text from
/// Discord.Net is passed through <see cref="SecretRedactor"/> before it reaches the log (decision #27).
/// </remarks>
public sealed class DiscordConnection : IDisposable
{
    /// <summary>Exactly these intents and no others (decision #41). MessageContent is for Discord → Game.</summary>
    private const GatewayIntents Intents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent;

    /// <summary>How often queued lines are posted. Lines queued in between are joined (05-message-format § Batching).</summary>
    private static readonly TimeSpan SendInterval = TimeSpan.FromSeconds(1);

    private readonly ILogger _log;
    private readonly ResolvedToken _token;
    private readonly string _tokenValue;
    private readonly ulong _chatChannelId;
    private readonly DiscordSocketClient _client;
    private readonly DiscordSendQueue _chatQueue = new();
    private readonly CancellationTokenSource _stopping = new();

    // Written by Ready (a Discord.Net thread), read by the send loop.
    private volatile IMessageChannel? _chatChannel;

    // Set when Discord rejects the token: nothing will ever be sent, so stop queueing.
    private volatile bool _gaveUp;

    private int _readyCount;
    private int _disposed;

    public DiscordConnection(ILogger log, ResolvedToken token, ulong chatChannelId)
    {
        if (token.Value is null)
        {
            throw new ArgumentException("A Discord token is needed to connect.", nameof(token));
        }

        _log = log;
        _token = token;
        _tokenValue = token.Value;
        _chatChannelId = chatChannelId;

        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = Intents,
            LogLevel = LogSeverity.Info,
        });
        _client.Log += OnDiscordLogAsync;
        _client.Ready += OnReadyAsync;
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
        if (_gaveUp || Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        _chatQueue.Enqueue(line);
    }

    /// <summary>
    /// Stops the background tasks and disconnects without blocking the caller. The shutdown message
    /// and its flush with a timeout are added with the system messages (issue #6).
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // Not disposed: the background tasks may still look at its token while they wind down.
        _stopping.Cancel();
        _client.Ready -= OnReadyAsync;
        _client.Log -= OnDiscordLogAsync;

        // DiscordSocketClient.Dispose waits for the client to stop, so it runs on a background task.
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
    /// Logs in and starts the gateway. If Discord can't be reached, tries again later, so the bot connects
    /// once the network returns (test plan 1.5). After that, Discord.Net reconnects by itself.
    /// </summary>
    private async Task LogInAsync(CancellationToken stopping)
    {
        int failures = 0;
        while (!stopping.IsCancellationRequested)
        {
            try
            {
                await _client.LoginAsync(TokenType.Bot, _tokenValue);
                await _client.StartAsync();
                _log.Debug("grug-bot logged in to Discord and is connecting to the gateway.");
                return;
            }
            catch (Exception e) when (IsRejectedToken(e))
            {
                _gaveUp = true;
                _log.Error(
                    "Discord rejected the bot token from {0}, so the relay is off. Check the token and restart the server. Cause: {1}",
                    _token.DescribeSource(),
                    Redact(e.Message));
                return;
            }
            catch (Exception e) when (!stopping.IsCancellationRequested)
            {
                failures++;
                TimeSpan delay = LoginRetryDelay.After(failures);
                const string RetryMessage = "grug-bot could not reach Discord and will try again in {0} seconds. The server carries on normally. Cause: {1}";
                if (failures == 1)
                {
                    _log.Warning(RetryMessage, (int)delay.TotalSeconds, Redact(e.Message));
                }
                else
                {
                    _log.Debug(RetryMessage, (int)delay.TotalSeconds, Redact(e.Message));
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

    /// <summary>A token Discord refuses (or one that isn't even shaped like a token) won't work on a retry.</summary>
    private static bool IsRejectedToken(Exception e) =>
        e is HttpException { HttpCode: HttpStatusCode.Unauthorized } || e is ArgumentException;

    /// <summary>
    /// Runs after every (re)connect that sends Ready. It only looks up the chat channel; it subscribes
    /// nothing, so a second Ready can't double anything up (gotcha #11, test plan 1.7).
    /// </summary>
    private Task OnReadyAsync()
    {
        try
        {
            IMessageChannel? channel = _client.GetChannel(_chatChannelId) as IMessageChannel;
            _chatChannel = channel;
            bool first = Interlocked.Increment(ref _readyCount) == 1;
            string botName = _client.CurrentUser?.Username ?? "the bot";

            if (channel is null)
            {
                _log.Error(
                    "grug-bot is connected to Discord as {0}, but can't find the chat channel {1} (ChatChannelId in {2}). Check the ID, and that the bot can view that channel. Chat lines wait until it can.",
                    botName,
                    _chatChannelId,
                    GrugBotConfig.DisplayPath);
            }
            else if (first)
            {
                _log.Notification("grug-bot is connected to Discord as {0} and relays General to #{1}.", botName, channel.Name);
            }
            else
            {
                _log.Debug("grug-bot's Discord connection is ready again after a reconnect.");
            }
        }
        catch (Exception e)
        {
            _log.Error("grug-bot hit an error when Discord became ready: {0}", Redact(e.ToString()));
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
                try
                {
                    await SendQueuedAsync();
                }
                catch (Exception e)
                {
                    _log.Warning("grug-bot hit an error while sending to Discord: {0}", Redact(e.ToString()));
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Dispose was called.
        }
    }

    /// <summary>
    /// Posts everything queued since the last tick, joined into as few messages as fit. Waits (keeps the
    /// lines queued) while Discord isn't connected or the chat channel isn't known yet.
    /// </summary>
    private async Task SendQueuedAsync()
    {
        IMessageChannel? channel = _chatChannel;
        if (channel is null || _client.ConnectionState != ConnectionState.Connected || _chatQueue.IsEmpty)
        {
            return;
        }

        foreach (string message in _chatQueue.DrainMessages())
        {
            try
            {
                // No allowed mentions on any send (gotcha #8). Discord.Net already waits out rate limits
                // and retries; a message that still fails is dropped, with no retries of our own (decision #26).
                await channel.SendMessageAsync(message, allowedMentions: AllowedMentions.None);
            }
            catch (Exception e)
            {
                _log.Warning("grug-bot could not post a message to Discord, so it was dropped. Cause: {0}", Redact(e.Message));
            }
        }
    }

    /// <summary>Passes Discord.Net's own log lines to the server log, without the token.</summary>
    private Task OnDiscordLogAsync(LogMessage message)
    {
        try
        {
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

    private string Redact(string text) => SecretRedactor.Redact(text, _tokenValue);
}
