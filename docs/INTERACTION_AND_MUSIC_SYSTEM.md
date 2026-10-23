# MUSIC GRAMMAR & REPEATABLE INTERACTION — authoritative implementation rules

## The one-line mapping
**WHAT object** = instrument; **WHICH orb weight** = articulation/timbre; **WHERE vertically/petal** = pitch; **HOW forcefully or intensity control** = dynamics; **WHEN/WHERE around orbit** = rhythm; **HOW many events** = subdivisions/density; **shared Heart only** = tempo.

## Three balls
| Orb | Shape / label | Engine nominal physics | Musical implication | expression |
|---|---|---|---|---|
| Flutter | tall turquoise + sparkle motif | light, floatier + safe speed clamp | airy/bright/shorter envelope | alert, curious |
| Bop | round lavender + single dot motif | medium, elastic bounce | centered/plucked/springy articulation | eager grin |
| Boom | wide amber + ripple motif | heavier, slower-looking + safe clamp | rounder/deeper/longer envelope | content squash |
Different mass is a **virtual affordance** and sound mapping; do not require players to accelerate the heaviest ball harder or calibrate real strength. In tap/place mode choose intensity from accessible discrete presets. Never represent weight merely as color.

## Instruments and pitch
- Table/Udu (Nigeria, commonly associated with Igbo pottery percussion): low open, muted, slap/heel/percussive variations. Label 'udu-inspired synthesized voice' until properly reviewed audio exists. Do not frame it as a D-major keyboard.
- Chair/Mbira (Zimbabwe, often Shona traditions): five initial notes D4 E4 F#4 A4 B4, plucked with audible decay. Authentic mbira tuning may differ from western equal temperament: D pentatonic is the *game's arrangement* not a claim about traditional tuning.
- Lamp/Erhu (China): D5 E5 F#5 A5 B5 or choose stable appropriate register after audio review; bowed sustained decay distinct from mbira. Synthesis is a stand-in.
- On pitched instrument `noteIndex = clamp(floor(normalizedHeight * 5),0,4)`. Top endpoint clamps to index 4. Equivalent inclusive five petal/sector selection for users unable to make height gestures.
- On udu `strikeZone` or accessible 5-pad mapping selects timbre/response, not forced pitched notes.

## Expressivity, not randomness
1) Impact occurs: immediately give subtle non-musical/contact cue and visible squish (<100ms perceptually is target, measure separately); optionally short live pitched preview; record ONE logical event (do not double-play during scheduling).
2) Musical event data: `{ targetId, orbType, noteIndexOrDrumArticulation, velocityTier, stepIndex, createdAtDspTime, stableId }`.
3) Quantize `stepIndex` to 0–15 nearest appropriate orbit position from collision timing against shared beat; each step represents a sixteenth note, four beats per revolution; a new event becomes part of playback on the next scheduled matching step. If the attack sounds late, improve feedback/scheduling rather than doubling audio.
4) Repeated same type on same object: creates another visual mini-face event, not a louder copy played simultaneously unless actually placed same step. If occupied, allow an adjacent step or explicit stack/chord according to clear documented rule. Do NOT change tempo automatically. Cap six per target; on seventh offer replace oldest/choose target rather than silently overwriting surprise.
5) Orbit has a constant visible beat marker; each echo passes it exactly when sample-accurate playback occurs. Static scale and rhythm are deterministic until player edits them.
6) Orbit edit: controller select, drag along ring (or rotate through 16 accessible step presets) to change onset; drag vertical / choose petal for note. Haptics only when real supported controller is used, never assume hands vibrate.
7) A global 4/4 tempo at 88 BPM is authoritative. A tap-heart may set 60–140 BPM later at the NEXT bar boundary, not while half-way through a loop. Faster subdivisions are different from faster BPM.

## Pitch vs rhythm vs harmony semantics
- Same lamp, one Flutter echo: a recurring phrase of ONE chosen pitch on every four-beat bar.
- Same lamp, two Flutter echoes on opposite orbit positions: two equally spaced notes inside that bar. BPM unchanged; if 16 positions apart model exactly.
- Same lamp, second echo raised one petal: second pitch may be a D/E/F#/A/B different melodic interval. If the two notes occur simultaneously this is *two pitches*, but true multi-voice harmony needs compatible samples/voices; if they occur separately, that's melody/interval sequence rather than a chord. Be precise in educational hints.
- Chair+lamp pitched at same timestamp: audible dyad/layered harmony; table supplies rhythm. Variations in balance, timing and articulation matter; D-major pentatonic is not proof every blend is attractive.

## Facial feedback state machine
- Idle: blinking, breathing, minimal motion.
- First successful hit on target: curious expression and quick look at new echo.
- Second same-type echo: two echoes notice/glance/smile at each other.
- Three/four denser echoes: delighted bounce (no incessant rapid flashing).
- After repositioning an echo: nod/puzzled tilt + clear sound preview; no negative fail-face.
- Mute/stop: gentle sleeping, static visual rhythm still visible. Characters react to *state changes*, not evaluated quality/wrong notes.
- User may toggle facial motion down/off while keeping static expressive indicators.

## Sound engine architecture
- One deterministic `MusicClock` uses Unity DSP double timestamps and lookahead; compute beat/bar/step precisely and schedule pooled note voices with `AudioSource.PlayScheduled` or proven equivalent. Distinct visual orbit renderer mirrors event IDs and scheduling timeline; no Update-based `AudioSource.Play()` at orbit crossing.
- Verify loop drift over at least 60 seconds, eventual 5 min soak; edge cases pause/resume, mute, stopping scene, tempo changes at bar, sample rates, suspended app/audio focus, echo delete in lookahead window, frame drops and clock reset.
- Preserve audio limiter/headroom; compare sine/samples at realistic loudness and avoid 3*6 voices clipping. Voice stealing rules deterministic. Missing cultural samples use temporary licensed CC0 synthesized assets with provenance recorded.
- Provide editor-only input simulation and EditMode tests for pure note/rhythm mapping; PlayMode tests for event cycle and visual positions.

## Accessible parity (binding table at architecture level)
| Intended action | physical/playful path | accessible path |
|---|---|---|
| Create note | safe underhand slow toss | choose orb + target, press place |
| Select pitch | hit height / move echo vertically | select one of 5 large pitch petals |
| Choose dynamics | light/firm throw | quiet/medium/strong switch |
| Create rhythm | timed throws | select one of 16 orbit positions / 4 presets |
| Change rhythm | pinch and slide echo | highlight echo, nudge step left/right |
| Tempo | tap Heart beat | large accessible +/- control, hold not required |
| Orchestral volume | hand raise (optional) | controller slider / 3 presets |
| Undo/remove | pick and flick out of orbit | undo button / select remove |
- All controls must work in controller-only, one-hand, seated mode; all features beyond MVP hand tracking optional. Provide clear focus cue, optional labels and no fine motor-only requirements.
