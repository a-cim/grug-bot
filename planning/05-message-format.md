# 05 — Message formats (v1)

Goal: **look the same as th3essentials did**, so players see no change after switching. The reference is the owner's screenshot of their old th3essentials bot channel (March 2026) plus th3essentials' `assets/th3essentials/lang/en.json` (v2.17.2).

## Game → Discord

Everything posts as a **plain bot message** in **one channel** (decision #10). There are no webhooks and no per-player avatars. Optionally, system lines can go to a second channel (`SystemChannelId`, where 0 means the chat channel, as in th3essentials).

| Event | Discord text (Markdown as sent) | Rendered example |
|---|---|---|
| General chat | `**{IGN}:** {text}` | **Kazimiera:** booo |
| Player joined | `***📥 Player {IGN} connected ({online}/{max})***` | ***📥 Player Ember connected (2/10)*** |
| Player left | `***📤 Player {IGN} disconnected ({online}/{max})***` | ***📤 Player Kazimiera disconnected (1/10)*** |
| Death | `***{vanilla death message}***` | ***Player Ember got killed by a nightmare drifter*** |
| Storm approaching | `***⛈️ A {light\|medium\|heavy} temporal storm is approaching***` | ***⛈️ A heavy temporal storm is approaching*** |
| Storm imminent | `***⛈️ A {strength} temporal storm is imminent***` | |
| Storm waning | `***⛈️ The temporal storm seems to be waning***` | |
| Server started | `***✅ Server started***` | |
| Server stopped | `***⛔ Server shutdown***` | |

Notes:

- **System lines are bold + italic** (`***…***`). That's th3essentials' `ServerMsg()` wrapper, and it's how players tell them apart from chat at a glance.
- **Use literal Unicode emoji** (📥 📤 ⛈️ ✅ ⛔), not th3essentials' `:inbox_tray:` shortcodes. Shortcodes rendered for th3essentials, but literal characters don't depend on Discord expanding them.
- **`{online}/{max}`:** online = admitted players (minus one on disconnect, because the leaving player is still counted). max = `sapi.Server.Config.MaxClients`.
- **Deaths:** use vanilla's own sentence, taken via reflection from `ServerSystemEntitySimulation.GetDeathMessage` as th3essentials does. If that breaks, fall back to our own `Player {IGN} died` / `… was killed by {entity}` (the old Discord Bot's `EnumDamageSource` switch). Skip entirely when the world config `disableDeathMessages` is true. *Two identical death lines in a row can be legitimate (a player died twice quickly), so don't de-duplicate.*
- **Storm wording:** use the game's own `Lang.Get(...)` strings so it matches the in-game text. Approaching, imminent and waning each post once per storm, using th3essentials' thresholds (see 03-reference-mods).
- **Bot presence:** `players: {online}/{max}`, updated on join and leave. Free, and it matches th3essentials.
- **Batching:** lines queued within ~1 s are joined with newlines into one Discord message (≤2000 chars), as th3essentials did. Discord would group them under one header anyway, so the screenshot looks the same either way.
- **Player text:** strip VTML, unescape `&lt;` / `&gt;` / `&amp;`, and neutralise `@everyone`, `@here` and pings (`AllowedMentions.None`). **Markdown passes through** (`*`, `_` and so on still format), which matches th3essentials. A config flag `EscapeMarkdown` (default `false`) is there for the strict version.
- **IGN** is `IServerPlayer.PlayerName`. There's no role prefix (th3essentials' `ShowRole` is out of scope).

## Discord → Game

Posted to the **General** group as `EnumChatType.OthersMessage`, with th3essentials' format:

```
<font color="#7289DA"><strong>{DiscordName}: </strong></font>{text}
```

- `DiscordName` is the server nickname (`SocketGuildUser.DisplayName`), falling back to the global name, then the username. It is VTML-escaped.
- Mentions resolve to `@Name`, `@Role` and `#channel`, then the whole text has `& < >` escaped.
- Attachments-only messages show `[Attachments]`. Empty messages are ignored.
- Colour is configurable (`DiscordNameColor`).
- Ignore our own bot, any webhook, and (configurable) other bots.

## Out of scope for v1

- Restart countdowns and backup notices (th3essentials' scheduler features)
- Slash commands of any kind (decision #12)
- Global OOC from thebasics (see decision #9)
