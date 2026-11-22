# 01 — Architecture

> Status: **decided, pre-implementation** (updated through decision #44). See [`decisions.md`](decisions.md). As of 2026-11-16: **one server-side mod** (Option B), C# / .NET 10, General-only relay, no th3essentials, PingPerfect hosting, th3essentials-style output.

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
│  ├─ GrugBot/          # the VS mod: modinfo.json, ModSystem, VS event adapters, loading/storing the config file
│  │                    #   net10.0, refs VintagestoryAPI (+ Newtonsoft.Json, protobuf-net; later VSEssentials/VSSurvivalMod) with Private=false
│  └─ GrugBot.Core/     # VS-free: config classes and defaults, token-source resolution, the "not configured" check,
│                       #   message formatting, VTML/Markdown/mention handling, send-queue batching
│                       #   net10.0, no VS references
├─ tests/
│  └─ GrugBot.Core.Tests/   # xUnit: config defaults and token resolution, formats from 05-message-format, escaping, batching edges
├─ build/               # Package.targets: after `dotnet publish`, writes dist/grugbot_<version>.zip
│                       #   (GrugBot.dll, GrugBot.Core.dll, Discord.Net DLLs, modinfo.json, LICENSE, THIRD-PARTY-NOTICES.md)
└─ planning/            # this folder; export-ignored, never packaged
```

`modinfo.json`: `"type": "code"`, `"modid": "grugbot"`, `"side": "server"`, depends on `"game": "1.22.0"`. **No dependency on thebasics.** Detection is at runtime.

## Data flow

### Game → Discord

| Source | Hook | Notes |
|---|---|---|
| General chat **only** | `Event.PlayerChat` where the chat group is `GlobalConstants.GeneralChatGroup` (0) | All other chat groups are ignored, including thebasics Proximity, Global OOC (parked) and player groups. grug-bot loads late (high `ExecuteOrder`) and **skips messages another mod consumed** (#24). Name = shown name (#36). |
| *(safety check)* thebasics | At `RunGame`, reflect `ProximityChatGroupId` from `RPProximityChatSystem` if it's loaded | If it's **0**, General has become proximity chat. Disable the relay and log a warning. **The live server is not configured this way** (2026-11-16), so this is a guard only. |
| Join | `Event.PlayerNowPlaying` | `📥 Player {IGN} connected (n/max)` |
| Leave | `Event.PlayerDisconnect` | Skip players still `Connecting`. n = admitted − 1. **Suppressed once shutdown begins** (#22). |
| Death | `Event.PlayerDeath` | Vanilla sentence via reflection, with a fallback. Respect `disableDeathMessages`. |
| Server start / stop | `ServerRunPhase(RunGame)` / `ServerRunPhase(Shutdown)` | The stop message is flushed **synchronously with a timeout** before returning. |
| Temporal storms | Game tick listener (1 s) reading the public `SystemTemporalStability.StormData`, fresh each time. No Harmony (#42). | Approaching (0.03 < days left < 0.35) / imminent (≤ 0.02) / waning (< 0.02 of the storm left), once each per storm. **If a storm ends without a waning line, post it then** (#43; Rustwall kills can skip the window). Skipped if `temporalStability` is off. |

### Discord → Game

1. `MessageReceived` in `ChatChannelId`.
2. Ignore our own bot, webhooks, other bots (configurable), users in `IgnoredDiscordUserIds`, and empty messages. Do nothing if `Relay.DiscordToGame` is off.
2a. Look up mentioned users Discord.Net doesn't have as server members over REST, on a background task (#41). Lines still reach General in the order they were posted.
3. Apply the Discord → Game rules (#25, `DiscordToGameText` in Core): resolve mentions and custom emoji, newlines to spaces, attachment and sticker placeholders, then the 500-character cap. Then VTML-escape `<` and `>` (not `&`: the game's chat shows `&amp;` literally, decision #46).
4. `sapi.Event.EnqueueMainThreadTask(() => sapi.SendMessageToGroup(0, line, EnumChatType.OthersMessage))`.

## Config (`ModConfig/grugbot.json`)

```jsonc
{
  "ConfigVersion": 1,
  "DiscordToken": "",            // ⚠ secret; GRUGBOT_DISCORD_TOKEN env var wins if set (#27)
  "ChatChannelId": 0,            // General <-> this channel
  "SystemChannelId": 0,          // 0 = use ChatChannelId (th3essentials' default, matches the screenshot)
  "DiscordNameColor": "#7289DA",
  "IgnoreOtherBots": true,
  "IgnoredDiscordUserIds": [],
  "EscapeMarkdown": false,
  "Relay": { "DiscordToGame": true, "Joins": true, "Leaves": true, "Deaths": true, "ServerStartStop": true, "TemporalStorms": true }
}
```

**Bump `ConfigVersion` whenever a setting is added, renamed or removed.** On start, a file with a lower version is saved back with the new settings at their defaults and every existing value kept; a file at the current version is never rewritten (issue #14).

## Lifecycle

- **Token:** `GRUGBOT_DISCORD_TOKEN` environment variable first, else `DiscordToken` from the config file (#27). Log which, never the value.

- **StartServerSide:** load or create the config. If the token or channel is missing, log a single clear message and stay idle; never throw.
- **Discord login is fire-and-forget.** The server must start even if Discord is down or the token is wrong. *The old Discord Bot's `StartDiscord().Wait()` is exactly what not to do.*
- **Ready** can fire many times (gateway reconnects). Subscribe to VS events once, and send "Server started" once.
- **A send loop** runs on a background task: it drains the per-channel queues about every 1 s and joins lines up to 2000 chars, splitting any single longer line at a word boundary. Discord.Net does the 429 retries (#26).
- **Backlog limits** while Discord is unreachable: drop lines older than 5 min, keep at most 500, and post one "n messages skipped" system message if anything was dropped (#23).
- **Presence** (`players: n/max`) updates 5 s after the last join or leave (#26).
- **Intents:** `Guilds | GuildMessages | MessageContent` only (#41).
- **Shutdown:** stop posting leaves, enqueue `⛔ Server shutdown`, flush with a ~3 s timeout, then `StopAsync`/`LogoutAsync`. A *crash* produces no shutdown message; that's an accepted limitation of in-process.
- **Dispose:** unsubscribe everything and dispose the client (no Harmony, decision #42).

## Out of scope (v1)

Everything else th3essentials does: homes, warps, rtp, kits, restarts, account linking and rewards, slash commands, admin logs. Also webhooks, per-group channel routing, and Global OOC (parked).
