# Coding standards

Rules for grug-bot code. Each one comes from a decision already made; the reference says where. Formatting (LF, 4 spaces, file-scoped namespaces) is in `.editorconfig`.

## Boundaries

- **`GrugBot.Core` references no Vintage Story assemblies.** Formatting, escaping and batching live there, with unit tests. *(ADR 0001)*
- **No code from thebasics, and no compile-time reference to it.** Reach it only through runtime reflection, and keep working when it isn't installed. *(ADR 0002)*
- **No Harmony.** Read public game state instead. *(decision #42)*

## Threads and lifetime

- **Never block the server on Discord.** No `.Wait()`, `.Result` or `.GetAwaiter().GetResult()` in the mod. The only exception is the shutdown flush, which has a timeout. *(01-architecture § Lifecycle, decision #41)*
- **Game API calls made from Discord callbacks go through `sapi.Event.EnqueueMainThreadTask`.** *(gotcha #16)*
- **Game event handlers are subscribed once and unsubscribed in `Dispose`.** Discord's Ready can fire many times. *(01-architecture § Lifecycle, gotcha #11)*

## Safety

- **Every message sent to Discord uses `AllowedMentions.None`.** *(gotcha #8)*
- **Text going into the game has `&`, `<` and `>` VTML-escaped.** *(gotcha #20)*
- **The token never appears in logs.** Log which source it came from, not its value. *(decision #27)*

## Packaging

- **Vintage Story DLLs are referenced with `Private=false` and never end up in the mod zip.** That includes `VintagestoryAPI.dll`, `Newtonsoft.Json.dll`, `0Harmony.dll` and `protobuf-net.dll`. *(gotcha #3)*

## Naming

- **Code names follow `CONTEXT.md`.** For example, `ChatGroup` for a Vintage Story chat group and `Channel` only for Discord, even where the VS API says `channelId`. *(decision #20)*
