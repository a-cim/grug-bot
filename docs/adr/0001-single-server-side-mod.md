# One server-side mod, not a shim plus a standalone bridge

Vintage Story has no network API for chat, so something has to run inside the server. We first planned a thin in-game shim talking over WebSocket to a separate bridge app that holds the Discord connection. We chose a single server-side mod with Discord.Net bundled instead. Game-server hosts such as PingPerfect run only the VS process, so the bridge would have needed its own always-on machine and a `wss://` link across the internet. And since th3essentials isn't installed alongside, there's no Discord.Net version clash to isolate. Formatting, escaping and batching live in the VS-free `GrugBot.Core` library, so they stay unit-testable and a split remains possible later.

## Consequences

- A server crash produces no "server stopped" message. A separate bridge could have noticed the dropped socket.
- The Discord token lives on the game server (see decision #27 in `planning/decisions.md` for how it's read).
