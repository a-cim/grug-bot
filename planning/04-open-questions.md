# 04 — Open questions

Once settled, move each answer into [`decisions.md`](decisions.md).

## Answered

- ~~Architecture / hosting~~ → one server-side mod on PingPerfect; test locally first (#6, #13)
- ~~Language~~ → C# / .NET 10 (#5)
- ~~Proximity chat policy~~ → only General goes to Discord (#7); General is not proximity on the live server (#14)
- ~~th3essentials~~ → won't be installed (#8)
- ~~Global OOC~~ → parked (#9; see below)
- ~~Discord presentation~~ → plain bot posts, th3essentials look (#10, [05-message-format](05-message-format.md))
- ~~v1 extras~~ → joins/leaves, deaths, start/stop, storms. No slash commands (#11, #12)
- ~~License~~ → MIT, with credits and notices (#15, #16)
- ~~Discord app~~ → a new application (#17)
- ~~Visibility~~ → public repo (#18)

## Still open

Nothing is blocking. Minor items to settle along the way:

- The `LICENSE` copyright line currently reads "grug-bot contributors". Change it if you'd rather it name a person or handle.
- Discord Bot's upstream repo URL for `THIRD-PARTY-NOTICES.md`.

## Parked

- **Global OOC (thebasics `((…))`).** It reaches every player in game but travels in the *proximity* group, so the General-only rule excludes it. Owner (2026-11-16): ignore for now, since players don't use it yet. **If it's wanted later:** subscribe to thebasics' `ProximityChatMessageProcessed` (soft dependency via reflection) and forward only `Kind == GlobalOoc`, formatted like General chat (maybe with a `(OOC)` tag). Gotchas #21–#23 then apply. It's a small, additive change.
