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

## Synthesized replacement set (2026-10-26, Prompt 07) — provenance documented
Made from scratch by `ArtSource/Audio/synthesize_instruments.py` (Python standard library only, fixed seeds: re-running gives byte-identical files). No samples, recordings or downloads. **Instrument-INSPIRED game voices, not recordings, no claim of traditional authenticity.** Same names, lengths and format as the prototype set; loudness matched per instrument; pitch measured within ±14 cents of D/E/F#/A/B (autocorrelation).
In game, each instrument's volume is trimmed by the measured average-to-peak ratio so the approved mix balance holds (Editor: `SoundSets.UseSynth()`): mbira −5.2 dB, erhu −1.9 dB, udu +0.3 dB.
**Active set: still the prototype set the owner approved** until the owner compares `Builds/RoomOrchestra-phase7-synth.apk` (new) with `Builds/RoomOrchestra-phase6l.apk` (current) by ear.

| Asset filename | Instrument or synthesis | Creator / URL | License & attribution | Commercial use permitted? | Hash / Verified by | Status |
|---|---|---|---|---|---|---|
| Assets/RoomOrchestra/Audio/Synth/Udu/Udu_0_D2.wav | synthesized (udu-inspired) | this project: ArtSource/Audio/synthesize_instruments.py | project-owned, no third-party material | yes (own work) | 580d1b36bfb6eded… (sha256, full in script output) | CANDIDATE — owner listening comparison pending |
| Assets/RoomOrchestra/Audio/Synth/Udu/Udu_1_E2.wav | synthesized (udu-inspired) | this project: ArtSource/Audio/synthesize_instruments.py | project-owned, no third-party material | yes (own work) | c6c711a429a3b7a6… (sha256, full in script output) | CANDIDATE — owner listening comparison pending |
| Assets/RoomOrchestra/Audio/Synth/Udu/Udu_2_Fs2.wav | synthesized (udu-inspired) | this project: ArtSource/Audio/synthesize_instruments.py | project-owned, no third-party material | yes (own work) | f2a5a7f5a9fa8996… (sha256, full in script output) | CANDIDATE — owner listening comparison pending |
| Assets/RoomOrchestra/Audio/Synth/Udu/Udu_3_A2.wav | synthesized (udu-inspired) | this project: ArtSource/Audio/synthesize_instruments.py | project-owned, no third-party material | yes (own work) | a6a5c9b14985b142… (sha256, full in script output) | CANDIDATE — owner listening comparison pending |
| Assets/RoomOrchestra/Audio/Synth/Udu/Udu_4_B2.wav | synthesized (udu-inspired) | this project: ArtSource/Audio/synthesize_instruments.py | project-owned, no third-party material | yes (own work) | 758c316f4f884a5f… (sha256, full in script output) | CANDIDATE — owner listening comparison pending |
| Assets/RoomOrchestra/Audio/Synth/Mbira/Mbira_0_D4.wav | synthesized (mbira-inspired) | this project: ArtSource/Audio/synthesize_instruments.py | project-owned, no third-party material | yes (own work) | c55d24401dd74a69… (sha256, full in script output) | CANDIDATE — owner listening comparison pending |
| Assets/RoomOrchestra/Audio/Synth/Mbira/Mbira_1_E4.wav | synthesized (mbira-inspired) | this project: ArtSource/Audio/synthesize_instruments.py | project-owned, no third-party material | yes (own work) | acdbfe93d2734d1a… (sha256, full in script output) | CANDIDATE — owner listening comparison pending |
| Assets/RoomOrchestra/Audio/Synth/Mbira/Mbira_2_Fs4.wav | synthesized (mbira-inspired) | this project: ArtSource/Audio/synthesize_instruments.py | project-owned, no third-party material | yes (own work) | 2bdd95a4183602f8… (sha256, full in script output) | CANDIDATE — owner listening comparison pending |
| Assets/RoomOrchestra/Audio/Synth/Mbira/Mbira_3_A4.wav | synthesized (mbira-inspired) | this project: ArtSource/Audio/synthesize_instruments.py | project-owned, no third-party material | yes (own work) | ad0e88846141d9d8… (sha256, full in script output) | CANDIDATE — owner listening comparison pending |
| Assets/RoomOrchestra/Audio/Synth/Mbira/Mbira_4_B4.wav | synthesized (mbira-inspired) | this project: ArtSource/Audio/synthesize_instruments.py | project-owned, no third-party material | yes (own work) | 7b48d0aee46a4007… (sha256, full in script output) | CANDIDATE — owner listening comparison pending |
| Assets/RoomOrchestra/Audio/Synth/Erhu/Erhu_0_D5.wav | synthesized (erhu-inspired) | this project: ArtSource/Audio/synthesize_instruments.py | project-owned, no third-party material | yes (own work) | 1afe3749f72da90f… (sha256, full in script output) | CANDIDATE — owner listening comparison pending |
| Assets/RoomOrchestra/Audio/Synth/Erhu/Erhu_1_E5.wav | synthesized (erhu-inspired) | this project: ArtSource/Audio/synthesize_instruments.py | project-owned, no third-party material | yes (own work) | 21a3c0e9a679daaf… (sha256, full in script output) | CANDIDATE — owner listening comparison pending |
| Assets/RoomOrchestra/Audio/Synth/Erhu/Erhu_2_Fs5.wav | synthesized (erhu-inspired) | this project: ArtSource/Audio/synthesize_instruments.py | project-owned, no third-party material | yes (own work) | 6a93f377d51ea3c6… (sha256, full in script output) | CANDIDATE — owner listening comparison pending |
| Assets/RoomOrchestra/Audio/Synth/Erhu/Erhu_3_A5.wav | synthesized (erhu-inspired) | this project: ArtSource/Audio/synthesize_instruments.py | project-owned, no third-party material | yes (own work) | aa26fe9f613e9f15… (sha256, full in script output) | CANDIDATE — owner listening comparison pending |
| Assets/RoomOrchestra/Audio/Synth/Erhu/Erhu_4_B5.wav | synthesized (erhu-inspired) | this project: ArtSource/Audio/synthesize_instruments.py | project-owned, no third-party material | yes (own work) | 2fb7b8520a8ef97b… (sha256, full in script output) | CANDIDATE — owner listening comparison pending |

