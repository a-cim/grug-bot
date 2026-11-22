# 05 — Message formats (v1)

Goal: **look the same as th3essentials did**, so players see no change after switching. The reference is the owner's screenshot of their old th3essentials bot channel (March 2026) plus th3essentials' `assets/th3essentials/lang/en.json` (v2.17.2).

## Game → Discord

Everything posts as a **plain bot message** in **one channel** (decision #10). There are no webhooks and no per-player avatars. Optionally, system lines can go to a second channel (`SystemChannelId`, where 0 means the chat channel, as in th3essentials).

| Event | Discord text (Markdown as sent) | Rendered example |
|---|---|---|
| General chat | `**{IGN}:** {text}` | **Grug:** booo |
| Player joined | `***📥 Player {IGN} connected ({online}/{max})***` | ***📥 Player Ugg connected (2/10)*** |
| Player left | `***📤 Player {IGN} disconnected ({online}/{max})***` | ***📤 Player Grug disconnected (1/10)*** |
| Death | `***{vanilla death message}***` | ***Player Ugg got killed by a nightmare drifter*** |
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
- **Storm wording:** use the game's own `Lang.Get(...)` strings so it matches the in-game text. Approaching, imminent and waning each post once per storm, using th3essentials' thresholds (see 01-architecture). Waning always posts, even if the storm ends early (#43).
- **Bot presence:** `players: {online}/{max}`, updated on join and leave. Free, and it matches th3essentials.
- **Batching:** lines queued within ~1 s are joined with newlines into one Discord message (≤2000 chars), as th3essentials did. Discord would group them under one header anyway, so the screenshot looks the same either way.
- **Player text:** strip VTML, unescape `&lt;` / `&gt;` / `&amp;`, and neutralise `@everyone`, `@here` and pings (`AllowedMentions.None`). **Markdown passes through** (`*`, `_` and so on still format), which matches th3essentials. A config flag `EscapeMarkdown` (default `false`) is there for the strict version.
- **IGN** in the table above means the **shown name** (decisions #35, #36). There's no role prefix (th3essentials' `ShowRole` is out of scope).

## Discord → Game

Posted to the **General** group as `EnumChatType.OthersMessage`, with th3essentials' format:

```
<font color="#7289DA"><strong>{DiscordName}: </strong></font>{text}
```

- `DiscordName` is the server nickname (`SocketGuildUser.DisplayName`), falling back to the global name, then the username. It is VTML-escaped.
- Mentions resolve to `@Name`, `@Role` and `#channel` (decision #41). A user's name is their Discord name: from the message's own mention data when Discord.Net has them as a server member, else from a non-blocking REST lookup of the member, else the mention data's global name or username, else `@unknown-user`. A role or channel that can't be found shows as `@unknown-role` or `#unknown-channel`.
- Custom emoji `<:name:id>` and animated `<a:name:id>` show as `:name:`. Timestamps like `<t:1700000000:R>` and any other `<…>` stay as typed (decision #25).
- **Standard emoji** (the Unicode ones from Discord's own picker, which arrive as plain text) also show as `:name:`, because the game's chat font has no emoji (decision #47): 👍 → `:thumbsup:`, 😂 → `:joy:`, 🇳🇴 → `:flag_no:`, 1️⃣ → `:one:`, 👨‍👩‍👧 → `:family_man_woman_girl:`. Names come from gemoji, with Discord's name for a few common ones.
  - The longest sequence the table knows wins: a ZWJ sequence (emoji joined by U+200D), a flag (two regional-indicator letters, or a black flag with tag letters like England's) or a keycap (`0`–`9`, `#` or `*`, then U+20E3) is one name. A ZWJ sequence the table doesn't know is shown part by part, the way an emoji font without it shows it (phoenix → `:bird::fire:`).
  - Variation selectors (U+FE0F, U+FE0E) and skin tones are ignored when looking up the name: 👍🏽 is `:thumbsup:`, with no tone suffix. They, and the joiners of a sequence, never stay behind; a stray selector anywhere is dropped.
  - An emoji the table doesn't know (newer than gemoji, or a lone flag letter or skin tone) becomes `:emoji:`.
  - Symbols that are text by default and only emoji with U+FE0F after them (`©`, `®`, `™`, `↔`, `❤`, `♥`…) stay as typed unless U+FE0F, a skin tone or a joiner follows: on their own they're text the game can draw, and Discord shows them as text too. `©️` (with U+FE0F) is `:copyright:`. Other text (`→`, `…`, digits, letters) is never changed.
  - This runs right after mentions are resolved (so emoji in a resolved name are replaced too), before newlines, the length cap and escaping. The cap counts `:name:` as shown.
- Placeholders (decision #25): attachments only → `[Attachment]` or `[n Attachments]`; text plus attachments → text + ` [Attachment]` or ` [n Attachments]`; stickers only → `[Sticker]` (`[n Stickers]` for several, and after the text or attachments if there are any). Empty messages are ignored.
- Newlines become spaces (one per line break), and the text is trimmed.
- **Length cap:** the text after the name, placeholders included, is at most **500 characters**. The name isn't counted. If it's longer, the message's own text is cut and ends in `…`, and the placeholders stay after it. The cap counts what players see: it's applied before escaping, so it can't cut an `&lt;` in half.
- Replies relay just their own text (not the message replied to). Edits and deletes are ignored: only new messages are relayed.
- Last, the name and the whole text have `<` and `>` escaped, so resolved names and placeholders are escaped too. `&` is left as it is, because the game's chat shows `&amp;` literally (found in owner testing, decision #46).
- Colour is configurable (`DiscordNameColor`).
- Ignore our own bot, any webhook, and (configurable) other bots.

## Out of scope for v1

- Restart countdowns and backup notices (th3essentials' scheduler features)
- Slash commands of any kind (decision #12)
- Global OOC from thebasics (see decision #9)
