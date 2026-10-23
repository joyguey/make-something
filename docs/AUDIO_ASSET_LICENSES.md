# AUDIO ASSET LICENSE LEDGER — SOUND PROTOTYPES, PROVENANCE NOT VERIFIED
Never claim instrument-recording authenticity or license compliance without a source entry.

**Status (2026-10-23):** every sound in the game is a **sound prototype**. The 15 source WAVs below have no recorded creator, URL or license. They look like they were synthesized during an earlier session (all created 2026-10-15 21:28 in one batch, identical sizes per instrument, no metadata), but that is NOT verified. They must not be presented as recordings of real udu, mbira or erhu, and must be replaced or documented before any public release (Prompt 07).

| Asset filename | Instrument or synthesis | Creator / URL | License & attribution | Commercial use permitted? | Hash / Verified by | Status |
|---|---|---|---|---|---|---|
| Assets/RoomOrchestra/Audio/Udu/Udu_0_D2 … Udu_4_B2.wav (5) | udu-inspired, likely synthesized | unknown | unknown | unknown | not recorded | NOT VERIFIED — prototype |
| Assets/RoomOrchestra/Audio/Mbira/Mbira_0_D4 … Mbira_4_B4.wav (5) | mbira-inspired, likely synthesized | unknown | unknown | unknown | not recorded | NOT VERIFIED — prototype |
| Assets/RoomOrchestra/Audio/Erhu/Erhu_0_D5 … Erhu_4_B5.wav (5) | erhu-inspired, likely synthesized | unknown | unknown | unknown | not recorded | NOT VERIFIED — prototype |
| Runtime-rendered voices (`VoiceDesign`, 15 per instrument) | derived in code from the WAVs above: envelope, filter, high-shelf, udu bend | this project's code | same as source WAVs | same as source WAVs | deterministic; unit-tested | PROTOTYPE |

## Derived voices (what the code does to the samples)
- **Orb timbre:** Flutter = high-shelf brighten (+~9 dB above 1 kHz) and 0.7 s length; Bop = plain sample; Boom = two-pole low-pass at 900 Hz, longer fade. Pitch is never changed for mbira/erhu.
- **Udu articulations** (percussion, not pitched notes): Open (D2 sample), Muted (D2, 0.18 s, low-pass), Slap (A2 sample, brightened, 0.25 s), Bend (D2 played 15% fast gliding to normal over 0.3 s, imitating a hand over the hole), Tap (B2 sample, 0.1 s, brightened, quieter). These are game stand-ins, not a description of traditional Igbo udu technique.

Agent: retain proof/copy of license where legally permitted, and make replacement/credit straightforward. Do not pull assets from search results without checking rights.
