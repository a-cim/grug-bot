# planning/

Working notes for designing grug-bot. **Not part of any release.** This folder is marked `export-ignore` in `.gitattributes`, so `git archive` and GitHub/GitLab source zips skip it, and no build target packages it. Delete it whenever it stops being useful.

| File | What's in it |
|---|---|
| [01-architecture.md](01-architecture.md) | The options considered, the chosen shape, data flow, config and lifecycle |
| [02-gotchas.md](02-gotchas.md) | Known traps: .NET 10, Discord intents, echo loops, thebasics proximity IDs, and more |
| [03-reference-mods.md](03-reference-mods.md) | What we learned from Discord Bot, th3essentials and thebasics |
| [04-open-questions.md](04-open-questions.md) | Things to decide before coding, plus parked ideas (Global OOC) |
| [05-message-format.md](05-message-format.md) | Exact Discord/in-game text formats, matching th3essentials' look |
| [06-test-plan.md](06-test-plan.md) | The v1 checklist: done means all of it passes |
| [decisions.md](decisions.md) | Running log of settled decisions |

The glossary is [`../CONTEXT.md`](../CONTEXT.md) and the hard-to-reverse decisions are ADRs in [`../docs/adr/`](../docs/adr/). Both are public and outlive this folder.

## Reference material (local downloads, not in repo)

- th3essentials source: `%USERPROFILE%\Downloads\th3essentials-main.zip` (v2.17.2, VS 1.22.0-rc.5)
- thebasics source: `%USERPROFILE%\Downloads\Vintage-Story-Mods-main.zip` (`mods-dll/thebasics`, v5.9.1)
- Discord Bot source: `%USERPROFILE%\Downloads\discordbot-master(1).zip` (v1.0.4, VS 1.15.5)
- VS install: `%APPDATA%\Vintagestory-v120-main\` (actually **1.22.7**)
- VS data: `%APPDATA%\VintagestoryData`
