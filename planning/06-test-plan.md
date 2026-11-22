# 06 — v1 test plan

v1 is done when every check below passes (decision #30). Run them on a **local 1.22.7 server** with **thebasics using a copy of the live config** and **Rustwall**, connected to a **private test Discord server** with a **test bot application** (never the live token). Each check names the decision or doc it proves. Checks marked 🧪 should also exist as `GrugBot.Core` unit tests.

Use made-up names in any notes or screenshots kept in the repo (decision #31).

## Setup

- [ ] Test Discord server with two text channels (`#chat`, `#system`) and a test bot. Message Content intent on, permissions: View Channels, Send Messages, Read Message History.
- [ ] Token set with `setx GRUGBOT_DISCORD_TOKEN …` (not in the config file).
- [ ] Two game clients (or one client plus server console) so a second player can join and leave.
- [ ] Note the server's `MaxClients`.
- [ ] **After replacing the mod zip, restart the whole game** (or the dedicated server). In singleplayer, reloading the world keeps the previously loaded grug-bot DLL in memory, so new code isn't picked up.

## 1. Startup and config

| # | Do | Expect | Proves |
|---|---|---|---|
| 1.1 | Start with no `grugbot.json` | File created with defaults and `ConfigVersion`; one clear "not configured" log line; server starts normally | 01 Lifecycle, #28 |
| 1.2 | Env var set, `DiscordToken` empty | Connects; log says token came from the environment and doesn't print it | #27 |
| 1.3 | Env var unset, token in file | Connects; log says token came from the config file | #27 |
| 1.4 | Wrong token | Server still starts; one clear error; nothing else breaks | 01 Lifecycle |
| 1.5 | Discord unreachable at startup (block network) | Server starts without waiting; bot connects once the network returns | 01 Lifecycle |
| 1.6 | `ChatChannelId` points at a channel the bot can't see | Clear error naming the channel ID; no crash | |
| 1.7 | Force a gateway reconnect (drop network briefly) | No second "Server started"; no doubled relays | gotcha #11 |
| 1.8 | Check the zip contents | No `VintagestoryAPI.dll`, `Newtonsoft.Json.dll`, `0Harmony.dll`, `protobuf-net.dll`; `THIRD-PARTY-NOTICES.md` included | gotcha #3, #15 |
| 1.9 | Start with a `grugbot.json` at `ConfigVersion: 1` | Saved back at `ConfigVersion: 2` with `Relay.DeathCount: false` added after `Relay.Deaths`; every other value (token, channel IDs, `Relay.*`) unchanged; one "Upgraded … from config version 1 to 2" log line | #28, #52 |

## 2. Game → Discord: chat

| # | Do | Expect | Proves |
|---|---|---|---|
| 2.1 🧪 | Say `hello` in General | `**Grug:** hello` in `#chat` | 05 |
| 2.2 | Speak in Proximity, in a player group, and in Global OOC `((…))` | Nothing on Discord | #7, #9 |
| 2.3 🧪 | Say `<b>hi</b> & 1 < 2` | Text arrives readable, no VTML tags or `&lt;` | 05 Player text |
| 2.4 🧪 | Say `@everyone hi` and `<@&123> hi` | No one gets pinged | gotcha #8 |
| 2.5 🧪 | Say `*bold* _it_` | Markdown formats (pass-through); with `EscapeMarkdown: true` it shows literally | #10, 05 |
| 2.6 | Mute a player (or use any mod that consumes General messages) and have them talk | Nothing on Discord | #24 |
| 2.7 🧪 | Several players talk within 1 s | Lines joined into one Discord message | 05 Batching |
| 2.8 🧪 | One very long line (> 2000 chars, e.g. via a test command) | Split at a word boundary, nothing lost | #26 |
| 2.9 | **Observe:** a player with a thebasics nickname talks in General | Record what the game shows in General and what Discord shows. If General shows the nickname, decision #36 applies: open a ticket for the nickname lookup | #40 |

## 3. Game → Discord: system messages

| # | Do | Expect | Proves |
|---|---|---|---|
| 3.1 🧪 | Second player joins | `***📥 Player Ugg connected (2/max)***` | 05, #11 |
| 3.2 🧪 | Second player leaves | `***📤 Player Ugg disconnected (1/max)***` | gotcha #17 |
| 3.3 | Player disconnects while still loading | No leave line | gotcha #17 |
| 3.4 | Player dies (fall, drowning, mob) | Vanilla sentence in bold-italic, matching the in-game line; with `disableDeathMessages` on, nothing | 05 Deaths |
| 3.5 | Same player dies twice quickly | Two lines | 05 Deaths |
| 3.6 | Set `SystemChannelId` to `#system` | System messages go there; chat stays in `#chat` | 05 |
| 3.7 | Join and leave several times quickly | Presence shows the right count, updated once things settle (~5 s) | #26 |
| 3.8 | Turn each `Relay.*` flag off in turn | That message type stops; the others continue | #28 |
| 3.9 🧪 | With thebasics counting deaths, set `Relay.DeathCount: true`, restart, die (`/kill`), then die again | `***{sentence}. This is death number n.***`, n matching `/playerstats`, then n+1; a full stop is added only when the sentence has none (e.g. a creature kill); after `/clearstats`, the next death says `number 1`; still one line per death | #52 |
| 3.10 | Default config (`Relay.DeathCount` absent or `false`), die | Line as in 3.4, no count | #52 |
| 3.11 | `Relay.DeathCount: true` without thebasics (or with its Deaths stat off), die | Line as in 3.4, no count; at most one info log line saying there is no count | #52 |

## 4. Temporal storms (with Rustwall)

Use `/nexttempstorm` (it has a "start next storm now" subcommand; check the exact syntax) and `/time` to move between phases.

| # | Do | Expect | Proves |
|---|---|---|---|
| 4.1 | Advance to 0.35 days before a storm | One `***⛈️ A heavy temporal storm is approaching***` (Rustwall forces Heavy) | #42, 05 |
| 4.2 | Advance to ≤ 0.02 days before | One "imminent" line | #42 |
| 4.3 | Let the storm run out naturally | One "waning" line | #42 |
| 4.4 | Start a storm and kill drifters until Rustwall ends it early | Still exactly one "waning" line, posted when the storm ends | #43 |
| 4.5 | Restart the server mid-storm | No repeated "approaching"/"imminent" for the same storm; waning still arrives once | #42 |
| 4.6 | World config `temporalStability` off | No storm lines at all | 01 Data flow |
| 4.7 | Confirm Rustwall still behaves (kills still shorten storms) with grug-bot installed | Unchanged | #42 (no interference) |

## 5. Discord → Game

| # | Do | Expect | Proves |
|---|---|---|---|
| 5.1 🧪 | Post `hello` in `#chat` | `Name: hello` in General, name in `DiscordNameColor` | 05 |
| 5.2 🧪 | Post `<font color=red>x</font> & <i>` | Shown literally (a plain `&`, not `&amp;`), chat line not broken | gotcha #20, #46 |
| 5.3 🧪 | Mention a user, a role, a channel | `@Name`, `@Role`, `#channel` | 05, #41 |
| 5.4 | Mention a user who hasn't posted recently (not cached) | Still resolves to `@Name`; no stall in the game | #41 |
| 5.5 🧪 | Multi-line message | One line, newlines as spaces | #25 |
| 5.6 🧪 | Message over 500 chars | Cut at 500 with `…` | #25 |
| 5.7 🧪 | Custom emoji, image only, text + 2 images, sticker only; standard emoji 👍, 👍🏽, 😂, 👨‍👩‍👧, 🇳🇴, 1️⃣, and `© → …` | `:name:`, `[Attachment]`, `text [2 Attachments]`, `[Sticker]`; `:thumbsup:`, `:thumbsup:`, `:joy:`, `:family_man_woman_girl:`, `:flag_no:`, `:one:`, and `© → …` unchanged, with no boxes anywhere | #25, #47 |
| 5.8 | Reply to a message; edit a message; delete a message | Reply relays just its text; edit and delete do nothing | #25 |
| 5.9 | Another bot posts; a user in `IgnoredDiscordUserIds` posts | Nothing in game (bots only if `IgnoreOtherBots`) | #28 |
| 5.10 | `Relay.DiscordToGame: false` | Nothing reaches the game; Game → Discord still works | #28 |
| 5.11 | Relay a Discord message, then check `#chat` | No echo back to Discord | gotcha #7 |

## 6. Shutdown and outages

| # | Do | Expect | Proves |
|---|---|---|---|
| 6.1 | `/stop` with 2 players online | `***⛔ Server shutdown***` posted; **no** leave lines after it; server exits within ~3 s of flush | #22, gotcha #18 |
| 6.2 | Server start | `***✅ Server started***` once | 05 |
| 6.3 | Block Discord for 2 min, chat in game, unblock | Lines arrive after reconnect | #23 |
| 6.4 | Block Discord for > 5 min, chat in game, unblock | Old lines dropped; one `***⚠️ n messages skipped while Discord was unreachable***` | #23 |
| 6.5 | Start the server with Discord blocked, unblock within 3 min | "Server started" arrives after connecting | #23 |
| 6.6 | Kill the server process | No shutdown message (accepted limitation) | ADR 0001 |

## 7. thebasics guard

| # | Do | Expect | Proves |
|---|---|---|---|
| 7.1 | Run without thebasics installed | Works; at most one info log line | ADR 0002 |
| 7.2 | thebasics `UseGeneralChannelAsProximityChat: true` | Relay from General disabled, one clear warning | 01 Data flow, #14 |
| 7.3 | thebasics installed, `UseGeneralChannelAsProximityChat: false` | Relay from General on; one info line naming thebasics' Proximity chat group ID (not 0); Proximity chat not relayed | #7, #14 |
