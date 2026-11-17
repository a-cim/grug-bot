# grug-bot

A lightweight bridge between **Vintage Story** server chat and **Discord**, in both directions. It's built to do just that, without the extra features of all-in-one server mods. It also works with the [thebasics](https://mods.vintagestory.at/thebasics) proximity chat mod.

> 🚧 **Status: planning.** No code yet. See `planning/` for the design work in progress.

## Target

- Vintage Story **1.22.x** (.NET 10)
- Discord bot with the Message Content intent enabled

## Setup (planned, v1)

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
2. Fill in `DiscordToken` and `ChatChannelId`, then restart.

## Credits

grug-bot stands on the shoulders of:

- **[Th3Essentials](https://gitlab.com/th3dilli_vintagestory/th3essentials)** by **Th3Dilli**. The Discord relay design, message formats and several techniques are adapted from it. If you want the full server toolkit (homes, warps, restarts, kits…), use Th3Essentials.
- **Discord Bot** by **bluelightning32**, the original Vintage Story ↔ Discord bridge mod.
- **[Discord.Net](https://github.com/discord-net/Discord.Net)**.

## License

[MIT](LICENSE). Third-party notices are in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
