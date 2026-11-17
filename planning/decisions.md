# Decisions log

Record each settled decision here: date, the decision, and why. Newest at the bottom.

| # | Date | Decision | Why |
|---|------|----------|-----|
| 1 | 2026-11-16 | Project name is **grug-bot**. | Owner's choice. |
| 2 | 2026-11-16 | Target **VS 1.22.7 / .NET 10**. | The running install is 1.22.7. |
| 3 | 2026-11-16 | `planning/` stays in git but is `export-ignore`d and never packaged. | Keeps the planning history without shipping it. Delete the folder whenever. |
| 4 | 2026-11-16 | ~~Architecture: thin in-game shim + standalone bridge app (Option C).~~ **Superseded by #13.** | |
| 5 | 2026-11-16 | **C# / .NET 10** for both the shim and the bridge. | One toolchain, and the protocol DTOs can be shared. |
| 6 | 2026-11-16 | Production server is hosted by **PingPerfect**. Development and testing happen **locally** for now. | A game-server host normally runs only the VS process, so the bridge would need its own 24/7 host in production (see 01-architecture § Hosting). |
| 7 | 2026-11-16 | **Only General chat (group 0) is relayed to Discord.** Proximity chat stays in-game. | Owner's choice. This also means no subscription to thebasics' message event; we only need its proximity group ID as a guard. |
| 8 | 2026-11-16 | **th3essentials will not be installed.** grug-bot replaces only its Discord ↔ chat mirroring. | Owner's choice. Removes the Discord.Net version-clash and double-relay concerns. |
| 9 | 2026-11-16 | **Global OOC is ignored for now** (parked, see 04-open-questions). | Players aren't using it yet. It can be added later via thebasics' event without redesign. |
| 10 | 2026-11-16 | Discord output is **plain bot messages**: chat as `**IGN:** text`, system lines as `***emoji text***`. Same look as th3essentials. | Players are used to it (owner's screenshot of their old th3essentials bot). Full spec in 05-message-format.md. |
| 11 | 2026-11-16 | v1 system messages: **join/leave (with online/max), deaths, server start/stop, temporal storms** (approaching / imminent / waning). | Owner's choice. Matches what th3essentials posted. |
| 12 | 2026-11-16 | **No Discord slash commands** in v1. | Owner's choice. No `applications.commands` scope needed. |
| 13 | 2026-11-16 | **Architecture: one server-side mod** (Option B) with Discord.Net bundled. Formatting/escaping/batching sits in a VS-free `GrugBot.Core` library. | PingPerfect can't host a second process, and with no th3essentials there's no Discord.Net clash. th3essentials itself works this way on GSPs. |
| 14 | 2026-11-16 | **The live server's General chat is not proximity** (thebasics `UseGeneralChannelAsProximityChat` = false). The thebasics check stays as a startup guard only. | Owner confirmed. |
| 15 | 2026-11-16 | **MIT license** (same as th3essentials). Credit Th3Dilli and bluelightning32 in the README, with their full MIT notices in `THIRD-PARTY-NOTICES.md`, which **ships in the mod zip**. | MIT requires keeping the original copyright notice with substantial portions of the code. It's also good manners. |
| 16 | 2026-11-16 | **No code copied from thebasics.** Interop is runtime reflection only (reading its proximity group ID). | thebasics is **GPL-2.0**. Copying its code (e.g. `VtmlUtils`) would pull grug-bot under the GPL. |
| 17 | 2026-11-16 | **A new Discord application** for grug-bot. The old th3essentials bot isn't reused. | Owner's choice. Setup steps are in the README. |
| 18 | 2026-11-16 | **The repo is public** on release. | Owner's choice. No tokens, real IDs, personal paths or other players' names in tracked files. `planning/` is tracked too, so the same rule applies there. |
