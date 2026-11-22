# grug-bot

A lightweight bridge between **Vintage Story** server chat and **Discord**, in both directions. It's built to do just that, without the extra features of all-in-one server mods. It also works with the [thebasics](https://mods.vintagestory.at/thebasics) proximity chat mod.

> **Status: 1.0.0.** General chat is relayed in both directions. Discord also hears when the server starts and stops, when players join and leave (the bot's status shows how many are online), when players die, and when a temporal storm is approaching, is imminent and is waning. See `planning/` for the design.

## Target

- Vintage Story **1.22.x** (.NET 10)
- Discord bot with the Message Content intent enabled

## Setup

### 1. Create the Discord bot

1. Go to the [Discord Developer Portal](https://discord.com/developers/applications) and click **New Application**. The name you pick becomes the bot's name.
2. On the **Bot** tab:
   - Click **Reset Token** and copy the token. Treat it like a password: never post it, commit it, or screenshot it.
   - Under **Privileged Gateway Intents**, turn on **Message Content Intent**. Without it, messages from Discord arrive empty. No other intents are needed.
   - Optional: turn off **Public Bot** so only you can add it to servers.
3. On **OAuth2 → URL Generator**:
   - Under scopes, tick **`bot`**.
   - Under bot permissions, tick **View Channels**, **Send Messages** and **Read Message History**.
   - Open the generated URL and add the bot to your Discord server.
4. In Discord, turn on **User Settings → Advanced → Developer Mode**. Then right-click the channel to mirror and choose **Copy Channel ID**.

### 2. Install the mod (server-side only)

1. Drop `grugbot_<version>.zip` into the server's `Mods` folder and start the server once. This creates `ModConfig/grugbot.json`.
2. Give grug-bot the bot token. The safest way is the `GRUGBOT_DISCORD_TOKEN` environment variable, which keeps the token out of the server's files; it wins over the config file when it's set. On Windows run `setx GRUGBOT_DISCORD_TOKEN "your-token"` and restart the server from a new terminal (or from the launcher after signing out and back in). On a hosted server without environment variables, put the token in `DiscordToken` instead.
3. Set `ChatChannelId` to the channel ID you copied, then restart.
4. Optional: to post system messages (such as `✅ Server started`) in a channel of their own, set `SystemChannelId` to that channel's ID. `0` keeps them in the chat channel.

If the token or channel is missing, grug-bot logs one message saying what to fill in and the server carries on without it.

### 3. Settings

All settings live in `ModConfig/grugbot.json`. Changes take effect after a restart. When an update adds settings, grug-bot adds them to your file at their defaults and keeps your values.

| Setting | Default | What it does |
| --- | --- | --- |
| `ConfigVersion` | `1` | Used by grug-bot to upgrade the file. Don't change it. |
| `DiscordToken` | `""` | The bot token, used only when `GRUGBOT_DISCORD_TOKEN` isn't set. Treat the file as secret if you fill this in. |
| `ChatChannelId` | `0` | The Discord channel paired with General. `0` means not set up. |
| `SystemChannelId` | `0` | The channel for system messages (start/stop, joins, leaves, deaths, storms). `0` uses the chat channel. |
| `DiscordNameColor` | `"#7289DA"` | The colour of Discord names in game. Must be `#RRGGBB` or `#RRGGBBAA`; anything else falls back to the default. |
| `IgnoreOtherBots` | `true` | Ignore messages from other bots in the chat channel. Webhooks and grug-bot's own posts are always ignored. |
| `IgnoredDiscordUserIds` | `[]` | Discord user IDs whose messages are never relayed to the game. |
| `EscapeMarkdown` | `false` | When on, `*`, `_` and other Markdown in game chat show literally in Discord instead of formatting. |
| `Relay.DiscordToGame` | `true` | Relay messages from the chat channel into General. |
| `Relay.Joins` / `Relay.Leaves` | `true` | Post when players join or leave. |
| `Relay.Deaths` | `true` | Post player deaths (also off when the world's `disableDeathMessages` is on). |
| `Relay.ServerStartStop` | `true` | Post when the server starts and stops. |
| `Relay.TemporalStorms` | `true` | Post when a temporal storm is approaching, is imminent and is waning. |

With [thebasics](https://mods.vintagestory.at/thebasics) installed and its Proximity chat group on General, grug-bot stops relaying General to Discord (so nearby-only chat stays in game) and says so in the log. System messages and Discord → Game keep working.

## Building

You need the .NET 10 SDK and a Vintage Story 1.22.x install. The game's DLLs can't be redistributed, so point the `VINTAGE_STORY` environment variable at the install folder (the one containing `VintagestoryAPI.dll`):

```sh
# Windows (new terminals pick it up):  setx VINTAGE_STORY "C:\path\to\Vintagestory"
export VINTAGE_STORY=/path/to/vintagestory

dotnet build GrugBot.slnx
dotnet test tests/GrugBot.Core.Tests            # needs no Vintage Story install
dotnet publish src/GrugBot/GrugBot.csproj -c Release   # writes dist/grugbot_<version>.zip
```

The version comes from `src/GrugBot/modinfo.json`.

CI (`.github/workflows/ci.yml`) builds and tests `GrugBot.Core` only, on every push to `main` and every pull request. It can't build the mod project because the Vintage Story DLLs aren't available there, so build that locally before opening a PR.

## Credits

grug-bot stands on the shoulders of:

- **[Th3Essentials](https://gitlab.com/th3dilli_vintagestory/th3essentials)** by **Th3Dilli**. The Discord relay design, message formats and several techniques are adapted from it. If you want the full server toolkit (homes, warps, restarts, kits…), use Th3Essentials.
- **Discord Bot** by **bluelightning32**, the original Vintage Story ↔ Discord bridge mod.
- **[Discord.Net](https://github.com/discord-net/Discord.Net)**.
- **[gemoji](https://github.com/github/gemoji)** by **GitHub**, for the emoji names shown in game.

## License

[MIT](LICENSE). Third-party notices are in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
