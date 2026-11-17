# 01 — Architecture

> Status: **decided, pre-implementation.** See [`decisions.md`](decisions.md). As of 2026-11-16: **one server-side mod** (Option B), C# / .NET 10, General-only relay, no th3essentials, PingPerfect hosting, th3essentials-style output.

## The constraint everything hangs off

Vintage Story's server has **no network API for chat**. It has no RCON, no webhook, no HTTP endpoint. The only ways to see who said what in which channel, or to inject a message into a chat group, are:

1. **In-process**: a server-side mod (`ICoreServerAPI`): `Event.PlayerChat`, `SendMessageToGroup`, and so on.
2. **Out-of-process scraping**: tailing `server-chat.log` / `server-main.log`, and writing to the server console's stdin.

## Options considered

| | What it is | Outcome |
|---|---|---|
| **A** | Pure external app: log tail + console injection | ❌ Rejected. Brittle parsing, needs stdin control of the server, and `/announce` renders as a server notice rather than chat. |
| **B** | **One slim server-side mod with Discord.Net inside** (what th3essentials and the old Discord Bot do) | ✅ **Chosen** (decision #13). |
| **C** | Thin in-game shim + standalone bridge app over WebSocket | Considered first. Dropped once two facts landed. PingPerfect can't host a second process, so the bridge would need its own 24/7 machine plus `wss://` across the internet. And without th3essentials there's no Discord.Net clash to avoid. |

What we keep from the Option C thinking: **the formatting, escaping and batching logic lives in a VS-free library**, so it can be unit-tested without a game server. A split remains possible later if it's ever needed.

## Shape

```
┌──────────────────────── VS server process (PingPerfect / local) ────────────────────────┐
│                                                                                          │
│  Players ──General chat──▶ ┌────────────────────┐   ┌─────────────────┐   ┌────────────┐  │
│     ▲                      │ GrugBot (ModSystem) │──▶│  GrugBot.Core   │──▶│ Discord.Net │──┼──▶ Discord #chat
│     └──SendMessageToGroup──│  VS event adapters │◀──│ format / escape │◀──│   client    │◀─┼─── (+ optional #system)
│                            └────────────────────┘   │ batch / filters │   └────────────┘  │
│  thebasics (proximity) ── stays in-game; we only     └─────────────────┘                  │
│                            check its group ID ≠ 0                                        │
└──────────────────────────────────────────────────────────────────────────────────────────┘
```

## Solution layout

```
grug-bot/
├─ src/
│  ├─ GrugBot/          # the VS mod: modinfo.json, ModSystem, VS event adapters, config
│  │                    #   net10.0, refs VintagestoryAPI (+ VSEssentials/VSSurvivalMod, Harmony) with Private=false
│  └─ GrugBot.Core/     # VS-free: message formatting, VTML/Markdown/mention handling, send-queue batching
│                       #   net10.0, no VS references
├─ tests/
│  └─ GrugBot.Core.Tests/   # xUnit: formats from 05-message-format, escaping, batching edges
├─ build/               # packaging: produces grugbot_<version>.zip (GrugBot.dll, GrugBot.Core.dll, Discord.Net DLLs, modinfo.json)
└─ planning/            # this folder; export-ignored, never packaged
```

`modinfo.json`: `"type": "code"`, `"modid": "grugbot"`, `"side": "server"`, depends on `"game": "1.22.0"`. **No dependency on thebasics.** Detection is at runtime.

## Data flow

### Game → Discord

| Source | Hook | Notes |
|---|---|---|
| General chat **only** | `Event.PlayerChat` where `channelId == GlobalConstants.GeneralChatGroup` (0) | All other groups are ignored, including thebasics proximity, Global OOC (parked) and player groups. |
| *(safety check)* thebasics | At `RunGame`, reflect `ProximityChatGroupId` from `RPProximityChatSystem` if it's loaded | If it's **0**, General has become proximity chat. Disable the relay and log a warning. **The live server is not configured this way** (2026-11-16), so this is a guard only. |
| Join | `Event.PlayerNowPlaying` | `📥 Player {IGN} connected (n/max)` |
| Leave | `Event.PlayerDisconnect` | Skip players still `Connecting`. n = admitted − 1. |
| Death | `Event.PlayerDeath` | Vanilla sentence via reflection, with a fallback. Respect `disableDeathMessages`. |
| Server start / stop | `ServerRunPhase(RunGame)` / `ServerRunPhase(Shutdown)` | The stop message is flushed **synchronously with a timeout** before returning. |
| Temporal storms | Harmony postfix on `SystemTemporalStability.onTempStormTick` (th3essentials' approach) | Approaching / imminent / waning, once each per storm. Skipped if `temporalStability` is off. |

### Discord → Game

1. `MessageReceived` in `ChatChannelId`.
2. Ignore our own bot, webhooks, other bots (configurable), and empty messages.
3. Resolve mentions, then VTML-escape `& < >`.
4. `sapi.Event.EnqueueMainThreadTask(() => sapi.SendMessageToGroup(0, line, EnumChatType.OthersMessage))`.

## Config (`ModConfig/grugbot.json`)

```jsonc
{
  "DiscordToken": "",            // ⚠ secret: lives on the server; see gotchas
  "ChatChannelId": 0,            // General <-> this channel
  "SystemChannelId": 0,          // 0 = use ChatChannelId (th3essentials' default, matches the screenshot)
  "DiscordNameColor": "#7289DA",
  "IgnoreOtherBots": true,
  "EscapeMarkdown": false,
  "Relay": { "Joins": true, "Leaves": true, "Deaths": true, "ServerStartStop": true, "TemporalStorms": true }
}
```

## Lifecycle

- **StartServerSide:** load or create the config. If the token or channel is missing, log a single clear message and stay idle; never throw.
- **Discord login is fire-and-forget.** The server must start even if Discord is down or the token is wrong. *The old Discord Bot's `StartDiscord().Wait()` is exactly what not to do.*
- **Ready** can fire many times (gateway reconnects). Subscribe to VS events once, and send "Server started" once.
- **A send loop** runs on a background task: it drains the per-channel queues about every 1 s, joins lines up to 2000 chars, and logs 429s and retries.
- **Shutdown:** enqueue `⛔ Server shutdown`, flush with a ~3 s timeout, then `StopAsync`/`LogoutAsync`. A *crash* produces no shutdown message; that's an accepted limitation of in-process.
- **Dispose:** unsubscribe everything, unpatch Harmony, and dispose the client.

## Out of scope (v1)

Everything else th3essentials does: homes, warps, rtp, kits, restarts, account linking and rewards, slash commands, admin logs. Also webhooks, per-group channel routing, and Global OOC (parked).
