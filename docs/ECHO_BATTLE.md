# Echo Battle (easter egg): two headsets, one Wi-Fi

A hidden two-player mode in Little Echoes Version 3, for trying a turn-based mixed-reality music battle on two PICO headsets. It is a separate mode: normal play is unchanged, and the player's own song is set aside (kept, silent) during a battle and plays on when they leave.

## Unlock
In free play (after the lesson, or Skip), pick the three friends in order **Flutter, Bop, Boom, Flutter, Bop, Boom** within 6 seconds: tap them on the leaf, or use the controller's next-character button. The **Echo Battle** card appears left of the leaf. The same secret hides it again.

## Play
1. Both headsets on the **same Wi-Fi network**. On one, tap **Host**; on the other, tap **Join**. They find each other automatically (no IP addresses, no accounts, no internet).
2. The host calls first. A count shows on the card (3, 2, 1), then **GO**: play up to 4 notes in one bar, on any furniture (tap tiles, use the controller, or throw a ball).
3. The other headset hears that phrase on its own furniture, gets its own count, and echoes it in the next bar.
4. Each echoed note with the same instrument and note scores **2** on the same 16th, **1** if one 16th off. Then the players swap.
5. Three rounds each; the card shows the round, the score and the winner. **Leave** ends a battle at any time.

## How it works (for evaluating feasibility)
- **Turn-based on purpose:** each headset judges its own player against its own music clock, so Wi-Fi delay never decides a point. Only the phrase (e.g. `M2@0,M1@4`) and the scores travel.
- **Network:** plain UDP on port **47777**. The host broadcasts a small "here I am" once a second; the guest answers. Important messages (start, call, score, bye) are numbered and resent until acknowledged; duplicates are dropped. Code: `Scripts/EchoBattleLink.cs`, rules: `Core/EchoBattle.cs`, the mode: `Scripts/EchoBattleDirector.cs`.
- **Android:** the app takes a Wi-Fi multicast lock while hosting or joining (Android drops broadcast packets otherwise); permissions in `Plugins/Android/LittleEchoesNetwork.androidlib`.
- **Privacy:** nothing leaves the local network; no names, room scans or recordings are sent; a random session id tells the two headsets apart.

## Tested so far
- Automated: the rules, the turn order, the message format, two links pairing and exchanging messages over loopback, and a full hosted turn on the real scene against a simulated second headset (EditMode `EchoBattleTests`, PlayMode `EchoBattlePlayModeTests`).
- **Not yet tested on two real headsets.** Things to watch: networks that block broadcast (guest networks, some office Wi-Fi: if Join keeps "looking", try a phone hotspot), the multicast lock on PICO OS 6, and how the count and the replayed call feel.

## Known limits
- Same-room play shares no anchors: each player sees their own furniture; the other player's call is replayed on yours.
- Two players only; the first guest to answer pairs with the host.
