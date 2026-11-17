# 03 — Reference mods: what each one teaches us

Sources are local downloads (see the paths in [README](README.md)). Line counts are approximate.

## Discord Bot (bluelightning32), v1.0.4, VS 1.15.5

- **Size:** two files, about 300 lines. `DiscordBotMod.cs` and `DiscordBotConfig.cs`. Server-side mod with Discord.Net pulled in as a git submodule.
- **Features:** chat both ways, join/leave/death, start/stop, ignore-list of Discord users, regex **log scraping** (used for temporal storms), and **per-player-group → Discord channel overrides**.
- **Worth keeping:**
  - The config shape: `DefaultChannel` + `ChannelOverrides` keyed by in-game group name, with per-route `ChatToDiscord` / `ChatToGame`. This maps almost directly onto our `routes`.
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

- **Proximity chat pipeline:** `RPProximityChatSystem` hooks `PlayerChat`, filters to `ProximityChatId`, sets `consumed.value = true`, and runs the transformer pipeline. `TransformerSystem.LogChatMessageOnce` then calls `Logger.Chat(...)` and **publishes `ProximityChatMessageProcessed`**.
- **Public extension surface** (`ITheBasicsProximityChatApi`):
  - `event EventHandler<ProximityChatMessageEventArgs> ProximityChatMessageProcessed`
  - `int ProximityChatGroupId`
  - The event args include `SendingPlayer`, `Recipients`, `PendingRecipients`, `GroupId`, `Kind` (`Speech, Emote, Environmental, PlacedEnvironmental, LocalOoc, GlobalOoc, Roll`), `ProcessedMessage`, `RenderedMessage`, `PlainTextMessage`, `Mode`, `Language` and `FromCommand`.
- **Its existing th3essentials integration** (`Th3EssentialsDiscordRelay.cs`):
  - Opt-in via `EnableTh3EssentialsDiscordRelay` (default `false`).
  - Strips VTML, unescapes, and neuters `@everyone`/`@here`. For rolls it zero-width-spaces every `@`.
  - It **reflects into th3essentials**: `GetModSystem("Th3Essentials.Th3Essentials")` → `.Th3Discord` → private `sendQueue`, plus `Config.DiscordChatRelay` and `DiscordChannel`.
- **What "model or duplicate" means for grug-bot:** we *don't* need thebasics to know about us. We subscribe to its public event ourselves, and do the same formatting (strip VTML → unescape → mention-safe). thebasics' relay toggle stays **off**. Zero changes to thebasics are needed.
- Optional nice-to-have: upstream a tiny "generic Discord relay" hook to thebasics later. Not required.

## The user's prior infra (context only)

- An older self-hosted Docker setup existed (VS 1.20). It isn't used now; production is PingPerfect.
- `Projects/vs-discordbot*`: earlier copies of the Discord Bot mod.
