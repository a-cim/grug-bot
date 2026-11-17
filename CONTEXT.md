# grug-bot

grug-bot relays chat between a Vintage Story server's General chat group and a Discord channel. It also posts system messages about server events.

## Relaying

**Relay**:
Copying messages between General and the chat channel, in either direction.
_Avoid_: bridge, mirror, sync

**Game → Discord** / **Discord → Game**:
The two directions of the relay, always named by source and destination.
_Avoid_: inbound, outbound, upstream, downstream

## Vintage Story side

**Chat group**:
A Vintage Story chat group that a message is said in, such as General, Proximity or a player-made group.
_Avoid_: channel, chat channel

**General**:
The chat group every player sees (ID 0). It is the only chat group grug-bot relays.
_Avoid_: global chat, main chat

**Proximity**:
thebasics' chat group for local speech that only nearby players hear. It is never relayed.
_Avoid_: local chat

**Global OOC**:
thebasics' out-of-character speech. Every player hears it, but it travels in the Proximity chat group, so it is not relayed.
_Avoid_: global chat

**Server event**:
Something on the game server that grug-bot reports: a join, a leave, a death, the server starting or stopping, or a temporal storm phase.
_Avoid_: notice, announcement

## Names

**Player name**:
A player's Vintage Story account name. It never changes.
_Avoid_: IGN, username, account name

**Nickname**:
A roleplay name a player sets through thebasics.
_Avoid_: alias, display name

**Shown name**:
The name other players see for that player in a given place in game. Every name grug-bot puts on Discord is a shown name.
_Avoid_: IGN, display name

**Discord name**:
The name grug-bot shows in game for a Discord user: their server nickname, else their global name, else their username.
_Avoid_: display name, Discord nickname

## Discord side

**Channel**:
A Discord channel. Never used for a Vintage Story chat group.

**Chat channel**:
The Discord channel paired with General.

**System channel**:
The Discord channel that receives system messages. It is the chat channel unless configured otherwise.

**System message**:
The bold-italic Discord line grug-bot posts for a server event.
_Avoid_: system line, notice, announcement
