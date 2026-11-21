# grug-bot

A lightweight bridge between **Vintage Story** server chat and **Discord**, in both directions. It's built to do just that, without the extra features of all-in-one server mods. It also works with the [thebasics](https://mods.vintagestory.at/thebasics) proximity chat mod.

> 🚧 **Status: early development.** General chat reaches the Discord chat channel; Discord → Game and system messages are still to come. See `planning/` for the design.

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

## License

[MIT](LICENSE). Third-party notices are in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
