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

    /// <summary>
    /// Gateway close code for "disallowed intents": a privileged intent (for us, Message Content) isn't
    /// turned on in the Developer Portal. Discord.Net stops reconnecting after it.
    /// </summary>
    private const int DisallowedIntentsCloseCode = 4014;

    /// <summary>How often queued lines are posted. Lines queued in between are joined (05-message-format § Batching).</summary>
    private static readonly TimeSpan SendInterval = TimeSpan.FromSeconds(1);

    private readonly ILogger _log;
    private readonly ResolvedToken _token;
    private readonly ulong _chatChannelId;
    private readonly DiscordSocketClient _client;
    private readonly DiscordSendQueue _chatQueue = new();
    private readonly CancellationTokenSource _stopping = new();

    // Written by Ready (a Discord.Net thread), read by the send loop.
    private volatile IMessageChannel? _chatChannel;

    // 1 once Discord has refused us for good (rejected token, Message Content intent off): nothing will
    // ever be sent, so stop queueing. Read on the game thread.
    private int _gaveUp;

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
        if (Volatile.Read(ref _gaveUp) != 0 || Volatile.Read(ref _disposed) != 0)
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

        // LogoutAsync waits for Discord.Net's state lock, so stopping, logging out and disposing all happen
        // on a background task and Dispose returns at once.
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
                await _client.GetApplicationInfoAsync(new RequestOptions { CancelToken = stopping });
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

    /// <summary>A token Discord refuses won't work on a retry. Discord answers 401 for a wrong or reset token.</summary>
    private static bool IsRejectedToken(Exception? e) => e is HttpException { HttpCode: HttpStatusCode.Unauthorized };

    /// <summary>The gateway close code Discord sent, if <paramref name="e"/> or an inner exception carries one.</summary>
    private static int? GatewayCloseCode(Exception? e)
    {
        for (Exception? current = e; current is not null; current = current.InnerException)
        {
            if (current is WebSocketClosedException closed)
            {
                return closed.CloseCode;
            }
        }

        return null;
    }

    private void GiveUpOnRejectedToken(Exception e) => GiveUp(
        "Discord rejected the bot token from {0}, so the relay is off. Check the token (it may have been reset in the Developer Portal) and restart the server. Cause: {1}",
        _token.DescribeSource(),
        Redact(e.Message));

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
        _chatQueue.Clear();

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
            catch (Exception e) when (IsRejectedToken(e))
            {
                // The token was reset while the server runs.
                GiveUpOnRejectedToken(e);
                return;
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
            // Discord.Net's connect loop reports these and would retry the first forever and stop on the
            // second with a cryptic error, so they get one clear line of our own instead.
            if (IsRejectedToken(message.Exception))
            {
                // A token reset while the server runs: the gateway gets a 401 when it reconnects.
                GiveUpOnRejectedToken(message.Exception!);
                return Task.CompletedTask;
            }

            if (GatewayCloseCode(message.Exception) == DisallowedIntentsCloseCode)
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
