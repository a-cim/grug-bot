# 02 — Gotchas

Things that will bite us, roughly ordered by how likely they are to cost a day. Items marked **(verify)** are inferences we still need to confirm against a running 1.22.7 server.

> **Cleaned up 2026-11-16** after decision #13 (one mod, ADR 0001): items that only applied to the shim + bridge plan or to running beside th3essentials are marked moot rather than deleted, so the numbers other docs cite stay stable.

## Platform / build

1. **VS 1.22.x runs on .NET 10.** `VintagestoryServer.runtimeconfig.json` says `tfm: net10.0`, and th3essentials targets `net10.0` for 1.22. The old Discord Bot mod targets `netstandard2.0` and 1.15.5. Its *code* is mostly reusable, but its project file and packaging are not.
2. **The install folder name is misleading.** `Vintagestory-v120-main` actually contains **1.22.7** (checked in `VintagestoryAPI.dll`). Point build paths at an env var (`VINTAGE_STORY`), not at a hard-coded folder name.
3. **Reference VS DLLs with `<Private>false</Private>`.** Never ship `VintagestoryAPI.dll`, `Newtonsoft.Json.dll`, `0Harmony.dll` or `protobuf-net.dll` in the mod zip. th3essentials has an explicit target that deletes a duplicate Newtonsoft.
4. **Discord.Net assembly clashes.** Another mod bundling a different Discord.Net version (th3essentials ships 3.18.0) could clash with ours in-process; which one wins depends on load order. th3essentials won't be installed (decision #8), so this only matters if another Discord mod is ever added. **(verify then)**
5. **Dev happens on Windows; the production OS (PingPerfect) is unconfirmed and may be Linux.** Watch path separators, line endings in configs, and case-sensitive file names (`modinfo.json`, not `ModInfo.json`).

## Discord

6. **The Message Content intent is privileged.** Without `GatewayIntents.MessageContent`, *and* the toggle turned on in the Developer Portal, every received message has empty `Content`. The 1.15-era Discord Bot mod predates this and would silently relay nothing.
7. **Echo loops.** If we post via webhook, our own webhook messages come back as `MESSAGE_CREATE`. Filtering `Author.Id == bot` is not enough. You must also drop messages with a `WebhookId`. The same goes for th3essentials if both run during migration: each bot will relay the other's posts unless it's ignored.
8. **Mentions.** Players can type `@everyone`, `@here` or `<@&roleId>` in game. Send with `AllowedMentions.None`. Don't rely on string replacement alone; th3essentials and thebasics both use `@_everyone`-style hacks.
9. **Markdown injection.** *(Decision: pass Markdown through like th3essentials did. `EscapeMarkdown` is an opt-in flag. See 05-message-format.)* Game text containing `*`, `_`, `~`, `` ` ``, `|`, `>` or `#` gets reformatted by Discord. Escape it (or wrap the text) when relaying.
10. **The 2000-character limit and rate limits.** About 5 messages per 5 seconds per channel. A join/leave burst on restart, or a chatty RP scene, will hit 429s. Batch per channel, as th3essentials does with its 1-second queue drain.
11. **Ready can fire more than once.** Discord.Net raises `Ready` again after gateway reconnects. th3essentials guards it with `_initialized`. Don't re-subscribe handlers or re-send "server started" on every reconnect.
12. *(Moot for v1, decision #12.)* **Slash commands** need the `applications.commands` OAuth scope. Guild-scoped commands update instantly; global ones can take up to an hour.

## Vintage Story chat API

13. **`PlayerChat`'s `message` is already formatted.** It arrives as something like `<strong>Name:</strong> text`, with VTML-escaped entities. Both reference mods regex out the text after `> `. Other mods (th3essentials role prefixes, timestamps) **mutate the same `ref string message`**, so what we see depends on handler order.
14. **`consumed` doesn't stop other handlers.** It only stops vanilla delivery. thebasics sets `consumed.value = true` for proximity lines, but our `PlayerChat` handler may run *before or after* it. Filter on **chat group ID**. In General, also skip consumed messages (decision #24).
15. **The proximity group ID isn't constant.** thebasics creates a player group named `Proximity` (configurable) per world, *or* repurposes General (`UseGeneralChannelAsProximityChat=true`). Read `ProximityChatGroupId` from thebasics at runtime. If it equals `GlobalConstants.GeneralChatGroup` (0), General is no longer global chat, and relaying it naively leaks local RP to Discord.
16. **Off-main-thread calls.** Discord.Net callbacks run on thread-pool threads. Calling `SendMessageToGroup` from them works in the reference mods, but the safe pattern is `sapi.Event.EnqueueMainThreadTask(...)`.
17. **Join/leave edge cases.** `PlayerDisconnect` fires for players who were still `Connecting` (th3essentials checks `ConnectionState`). The disconnecting player is still counted in `AllOnlinePlayers`, so subtract 1.
18. **Shutdown messages get lost.** `async void` sends during `EnumServerRunPhase.Shutdown` race the process exit. Flush the queue synchronously with a ~3 s timeout before returning, and suppress leave messages from then on (decision #22). A crash still produces no message (ADR 0001).
19. **Death messages.** *(Verified 2026-11-16: `GetDeathMessage` exists and is private in 1.22.7; the sentences are `deathmsg-*` lang keys like "Player {0} starved to death.")* Vanilla wording lives in a private `ServerSystemEntitySimulation.GetDeathMessage`, which th3essentials reaches by reflection. It's private, so expect breakage across versions. Also honour the world config `disableDeathMessages`.
20. **VTML going *into* the game.** Discord text must have `<` and `>` escaped, or a stray `<font` will break the chat line. Leave `&` as it is: the game's chat shows `&amp;` literally (found in owner testing), and a lone `&` can't open a tag. th3essentials does the same (decision #46).

## thebasics (Proximity Chat) integration

> **Update 2026-11-16:** We're relaying General only, and th3essentials won't be installed. That leaves **#15 as the one that matters**. #21–#23 and #26 apply only if we ever relay proximity chat. #4 and #25 are moot.

21. **Integrate through its public event, not th3essentials' internals.** thebasics exposes `ITheBasicsProximityChatApi.ProximityChatMessageProcessed` (on `RPProximityChatSystem`). Its built-in th3essentials relay reaches into th3essentials by **reflection on a private `sendQueue` field**. That's the coupling we don't want to copy.
22. **Use a soft dependency.** If we compile against `thebasics.dll` and it isn't installed, the type load fails. Plan: `GetModSystem("thebasics.ModSystems.ProximityChat.RPProximityChatSystem")`, then `EventInfo.AddEventHandler` with a delegate built at runtime, then read event-args properties by reflection or `dynamic`. Log once if anything is missing.
23. **The event fires before delivery.** thebasics' own research notes say `ProximityChatMessageProcessed` is published from `LogChatMessageOnce` *before* recipients are sent to, even when there are zero recipients. Treat it as "a player said this", not "someone heard this".
24. **Privacy and immersion.** Relaying proximity speech makes local, distance-obfuscated, in-language RP readable by everyone on Discord. thebasics defaults its relay to **off** for this reason. Plan to route by `Kind` (e.g. only `GlobalOoc` and `Roll` by default) and consider a separate Discord channel for RP.
25. *(Moot: th3essentials won't be installed, decision #8.)*
26. **Rolls:** thebasics suppresses all `@` for `Kind == Roll` (zero-width-space). Keep that behaviour.

## Operational

27. **Secrets.** The token is read from the `GRUGBOT_DISCORD_TOKEN` environment variable first, then from `ModConfig/grugbot.json` (decision #27). Never commit it, and keep that config file out of shared backups and screenshots. Log which source was used, never the value.
28. *(Moot: no shim↔bridge link, ADR 0001.)*
29. *(Moot: no shim↔bridge link, ADR 0001.)*
30. *(Moot: th3essentials won't be installed, decision #8.)*
31. *(Moot: this is why we chose one mod, ADR 0001.)*
32. **Global OOC won't relay** *(accepted for now, decision #9; parked in 04-open-questions)*. thebasics' `((...))` global OOC reaches every player but travels in the proximity group, not General, so the General-only rule excludes it. This is intentional unless decided otherwise.

## Licensing

33. **thebasics is GPL-2.0.** Read it for ideas, but **write our own** VTML stripping/unescaping. Don't paste from `VtmlUtils.cs` or any other thebasics file. th3essentials and Discord Bot are MIT, so adapting them is fine as long as `THIRD-PARTY-NOTICES.md` keeps their notices.
34. **Bundled binaries need notices too.** Discord.Net and its transitive dependencies ship inside the mod zip, so their license texts go into `THIRD-PARTY-NOTICES.md` at packaging time (there's a TODO in that file).
