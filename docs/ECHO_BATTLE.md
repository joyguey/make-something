# Echo Battle (easter egg): two headsets, one Wi-Fi

A hidden two-player mode in Little Echoes Version 3, for trying a turn-based mixed-reality music battle on two PICO headsets. It is a separate mode: normal play is unchanged, and the player's own song is set aside (kept, silent) during a battle and plays on when they leave.

## Unlock
In free play (after the lesson, or Skip; a device that has done the lesson starts in free play, and the leaf's Lesson button replays it), pick the three friends in order **Flutter, Bop, Boom, Flutter, Bop, Boom** within 6 seconds: tap them on the leaf, or use the controller's next-character button. The **Echo Battle** card appears in a poof, up and a little left, above the leaf. **Leave** puts it away (during a battle, Leave ends the battle first).

## Look (2026-10-31)
The battle card is styled for teens and adults, separate from the main game's soft look: a street poster pasted on a small brick wall. It has a dark asphalt panel, a hand-sprayed **ECHO//BATTLE** title (回声对决 in Chinese) with short paint drips, tape at the corners, and tilted sticker buttons: **HOST** (pink), **JOIN** (blue), **LEAVE** (orange). The wording is shorter and more direct: "Drop your beat", "Their call. Listen up...", "YOU 4 vs 2 THEM", "Run it back?". Flutter, Bop and Boom are unchanged. The title font, Sedgwick Ave Display (SIL Open Font License), is traced into 3D geometry; the font itself is not shipped (credited in `Fonts/LICENSES.md`). The card is kept small, up and to the left, clear of the furniture tiles you see from the seat (checked by a test).

## Play
1. Both headsets on the **same Wi-Fi network**. On one, tap **HOST**; on the other, tap **JOIN**. They find each other automatically (no IP addresses, no accounts, no internet).
2. The host calls first. A count shows on the card (3, 2, 1), then **GO**: play up to 4 notes in one bar, on any furniture (tap tiles, use the controller, or throw a ball).
3. The other headset hears that phrase on its own furniture, gets its own count, and echoes it in the next bar.
4. Each echoed note with the same instrument and note scores **2** on the same 16th, **1** if one 16th off. Then the players swap.
5. Three rounds each; the card shows the round, the score and the winner. **LEAVE** ends a battle at any time.

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

## Changes after the V4 review (2026-11-16)
The dev team tried it on two headsets: hosting and joining worked but were hard to follow, a second press on JOIN made a player leave, and the listening player could not hear the call.
- **HOST and JOIN only work from the card's menu.** Once pairing or playing they are put away, and LEAVE is the only button. (A second press used to reopen the link, which said goodbye to the other headset.)
- **Clearer words:** "one taps HOST, then the other taps JOIN"; "You are the host. Ask the other player to tap JOIN."; "Joining. Looking for the host..."; "Listen: their N notes are playing".
- **The call is always heard and seen.** It plays at full strength with the furniture's flash and its note band lit. If the listener's room doesn't have the caller's instrument (each player can change theirs), it plays on the listener's furniture in the caller's instrument's sound, and the same note on any of the listener's instruments scores.
- **Not reproduced:** on the real scene with the default instruments, the listening path queues and plays the call (new PlayMode test). The silence on the headsets is therefore most likely the two rooms holding different instruments; that is fixed, but only two headsets can confirm it.

