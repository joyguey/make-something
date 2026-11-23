# Little Echoes v4.3 (app 4.3.0, build 41): Echo Battle test build

A build for the dev team to try the Echo Battle (two headsets) as soon as possible. It is v4.2 plus the multiplayer fixes and the V4 review fixes below. The new icons and meadow trees shown in renders are **not** in this build.

**Install:** `LittleEchoes-v4.3.apk`, on both headsets. It has the same package as v4.2 (`com.littleechoes.app`), so it should install over it; if the headset refuses, uninstall v4.2 first. Both headsets must run v4.3: the battle's network messages changed, so v4.2 and v4.3 will not find each other.

## What changed
**Echo Battle**
- **The host chooses the instruments.** When the host taps HOST, the instruments on their three pieces of furniture are locked in, and the card names them.
- **The guest matches them.** After JOIN, the guest's card lists the host's instruments still missing from their room. Tapping Change on a piece of furniture gives it the next missing one. Play starts when both rooms hold the same instruments.
- **Nobody can change instruments during a battle.** A tap on Change says so. After the battle (or LEAVE), the guest gets their own instruments back.
- **Each call is always heard** on the other headset, and pressing HOST or JOIN twice no longer leaves the battle.

**V4 review fixes**
- No gap between the sky and the ground at the horizon.
- The title sign stands at 85 cm (was 74 cm).
- Each instrument's name has a second line saying what it is ("mbira / thumb piano", "udu / clay pot drum"), on the furniture, on the spots over real furniture and in the lesson. English only: the Chinese names already describe the instrument.

## How to try the Echo Battle
1. Put both headsets on the same Wi-Fi. Guest and office networks often block the broadcast it uses to find the other headset; a phone hotspot is the quickest fix.
2. On both: finish or skip the lesson. Then tap the three friends on the leaf in the order Flutter (turquoise), Bop (purple), Boom (orange), Flutter, Bop, Boom within 6 seconds. The Echo Battle card appears.
3. Headset 1 taps **HOST**. Headset 2 taps **JOIN**.
4. On headset 2, tap Change on the furniture until the card stops listing missing instruments. The 3-2-1-GO starts on its own.
5. The host plays up to 4 notes in one bar; the guest hears it and echoes it in the next bar. Players swap, three rounds each. **LEAVE** ends it at any time.

**Please tell us:** did the headsets find each other? Was it clear what the guest had to do? Could each player hear the other's phrase? Did the scores match on both headsets? Did each player get their own instruments back afterwards?

It uses plain UDP on port 47777 on the local network only. Nothing goes to the internet.

## Tested
- Automated: 263 EditMode and 115 PlayMode tests passed on the committed code.
- **Not yet tried on two real headsets.** The two-headset flow was checked only by the automated tests and an editor storyboard.
