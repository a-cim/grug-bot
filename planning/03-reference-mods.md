# 03 — Reference mods: what each one teaches us

Sources are local downloads (see the paths in [README](README.md)). Line counts are approximate.

## Discord Bot (bluelightning32), v1.0.4, VS 1.15.5

- **Size:** two files, about 300 lines. `DiscordBotMod.cs` and `DiscordBotConfig.cs`. Server-side mod with Discord.Net pulled in as a git submodule.
- **Features:** chat both ways, join/leave/death, start/stop, ignore-list of Discord users, regex **log scraping** (used for temporal storms), and **per-player-group → Discord channel overrides**.
- **Worth keeping:**
  - The config shape: `DefaultChannel` + `ChannelOverrides` keyed by in-game group name, with per-route `ChatToDiscord` / `ChatToGame`. We relay General only in v1, but this is the model if per-group routing is ever added.
  - The log-scrape regex idea as a cheap catch-all for server notices.
  - Updating presence ("with N players").
- **Why it can't just be updated:**
  - It doesn't request the `MessageContent` intent (Discord change in 2022), so inbound content is empty.
  - `netstandard2.0` + submodule build; VS 1.22 is `net10.0`.
  - `StartDiscord().Wait()` blocks server startup on Discord login. If Discord is down, server start hangs or fails.
  - No rate-limit batching, no mention resolution, and only `AllowedMentions` for safety.

## th3essentials (Th3Dilli), v2.17.2 source, VS 1.22.0-rc.5

- **Size:** about 5.5k lines. The Discord part is `Discord/Th3Discord.cs` (~800 lines) plus about 15 slash-command files. The rest is homes, warps, kits, restarts, rtp, and so on.
- **Worth keeping:**
  - **Batching send queues** (`ConcurrentQueue` per channel, 1-second drain, joined to ≤1950 chars).
  - **Mention resolution** (`<@id>` → `@DisplayName`, roles, channels) in `CleanDiscordMessage`.
  - **Separate channels** for chat, system (join/leave/restart) and admin log.
  - The `_initialized` guard for repeated `Ready`.
  - Demoting noisy gateway reconnect exceptions to Debug in the log handler.
  - Temporal-storm detection via a Harmony postfix on `SystemTemporalStability.onTempStormTick` (approaching / imminent / waning thresholds).
- **Watch out for:**
  - It mutates `PlayerChat`'s `ref message` (role prefixes, timestamps), which affects what *we* see.
  - It relays only `GeneralChatGroup` (and notes that "Info log" shares that ID).
  - Death messages come from reflecting into `ServerMain.Systems` → `ServerSystemEntitySimulation.GetDeathMessage`.
  - It only escapes `<` and `>` (not `&`) on inbound.

## thebasics (BASIC), v5.9.1, `mods-dll/thebasics`

- **Names in chat** *(checked 2026-11-16)*: thebasics' `PlayerChat` handler returns early for any chat group except Proximity, and it filters vanilla join/leave/death lines out of Proximity without rewriting them. So **General chat and those vanilla lines carry the player name; nicknames only appear in Proximity.** Nicknames live in player ModData under `BASIC_NICKNAME`.
- **Proximity chat pipeline:** `RPProximityChatSystem` hooks `PlayerChat`, filters to `ProximityChatId`, sets `consumed.value = true`, and runs the transformer pipeline. `TransformerSystem.LogChatMessageOnce` then calls `Logger.Chat(...)` and **publishes `ProximityChatMessageProcessed`**.
- **Public extension surface** (`ITheBasicsProximityChatApi`):
  - `event EventHandler<ProximityChatMessageEventArgs> ProximityChatMessageProcessed`
  - `int ProximityChatGroupId`
  - The event args include `SendingPlayer`, `Recipients`, `PendingRecipients`, `GroupId`, `Kind` (`Speech, Emote, Environmental, PlacedEnvironmental, LocalOoc, GlobalOoc, Roll`), `ProcessedMessage`, `RenderedMessage`, `PlainTextMessage`, `Mode`, `Language` and `FromCommand`.
- **Its existing th3essentials integration** (`Th3EssentialsDiscordRelay.cs`):
  - Opt-in via `EnableTh3EssentialsDiscordRelay` (default `false`).
  - It relays the **rendered proximity line** (nickname plus speech verb, e.g. "Ugg says: …"), not a `**Name:** text` line. *(Checked 2026-11-16 against v5.9.1.)*
  - Strips VTML, unescapes, and neuters `@everyone`/`@here`. For rolls it zero-width-spaces every `@`.
  - It **reflects into th3essentials**: `GetModSystem("Th3Essentials.Th3Essentials")` → `.Th3Discord` → private `sendQueue`, plus `Config.DiscordChatRelay` and `DiscordChannel`.
- **What this means for grug-bot:** thebasics never needs to know about us. In v1 (General only) we only read `ProximityChatGroupId` as a guard. If we ever relay anything from Proximity, we subscribe to its public event ourselves and format it the same way (strip VTML → unescape → mention-safe), with our own code (ADR 0002). No changes to thebasics are needed.
- Optional nice-to-have: upstream a tiny "generic Discord relay" hook to thebasics later. Not required.

## Rustwall (server's own overhaul mod), v1.9.0, VS 1.22.3

Not a Discord mod, but it changes temporal storms, which grug-bot reports. *(Read 2026-11-16.)*

- Harmony **postfix on `SystemTemporalStability.onTempStormTick`**, the same method th3essentials patched. It keeps a static reference to the storm runtime data and applies a pending offset to it.
- **Kills shorten storms:** a player killing a drifter, shiver or bowtorn during a storm subtracts `TemporalStormDaysRemovedPerKill` (default 0.0417 days, about one in-game hour) from `stormActiveTotalDays`.
- **Postfix on `prepareNextStorm`:** every storm is forced to Heavy, with "climates" (Relaxed / Sporadic / Aggressive) setting the duration and the gap to the next storm.
- It mutates the **same `TemporalStormRunTimeData` object** that the public `SystemTemporalStability.StormData` returns, so anything reading `StormData` sees Rustwall's changes.
- It posts no chat messages of its own about storms.

## The user's prior infra (context only)

- An older self-hosted Docker setup existed (VS 1.20). It isn't used now; production is PingPerfect.
- `Projects/vs-discordbot*`: earlier copies of the Discord Bot mod.
