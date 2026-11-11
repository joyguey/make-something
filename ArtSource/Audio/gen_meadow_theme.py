"""The meadow theme: the title's and the meadow's background music (V3 review: "some background music").

Original synthesis only (no samples): an 8-bar loop at 88 BPM in D major pentatonic, the game's own key and tempo,
so it sits with everything the player plays. A soft two-voice pad, a gentle bass on beats 1 and 3, a music-box melody
and a light shaker on the off-beats. The tails of the last bar wrap into the first, so it loops without a seam.
Fixed seed: running it again writes the same file byte for byte.

  python3 -I ArtSource/Audio/gen_meadow_theme.py Assets/RoomOrchestra/Audio/Music/MeadowTheme.wav
"""
import math
import random
import struct
import sys
import wave

SR = 44100
BPM = 88.0
BEAT = 60.0 / BPM
BARS = 8
LOOP = int(round(BARS * 4 * BEAT * SR))
PEAK = 0.45
TAU = 2 * math.pi
rng = random.Random(20261111)


def hz(name):
    names = {"C": 0, "C#": 1, "D": 2, "D#": 3, "E": 4, "F": 5, "F#": 6, "G": 7, "G#": 8, "A": 9, "A#": 10, "B": 11}
    note, octave = name[:-1], int(name[-1])
    midi = 12 * (octave + 1) + names[note]
    return 440.0 * 2 ** ((midi - 69) / 12)


buf = [0.0] * (LOOP + SR * 4)


def add(start, samples):
    for i, v in enumerate(samples):
        j = start + i
        if j < len(buf):
            buf[j] += v


def pad(chord, start_beat, beats, level=0.06):
    n = int(beats * BEAT * SR)
    attack, release = int(0.6 * SR), int(1.2 * SR)
    out = [0.0] * (n + release)
    for name in chord:
        f = hz(name)
        for detune in (-0.12, 0.12):
            ff = f * 2 ** (detune / 12)
            ph = rng.random() * TAU
            for i in range(n + release):
                env = min(1.0, i / attack) if i < n else max(0.0, 1.0 - (i - n) / release)
                out[i] += level * env * (math.sin(ph + TAU * ff * i / SR) + 0.15 * math.sin(2 * (ph + TAU * ff * i / SR)))
    add(int(start_beat * BEAT * SR), out)


def pluck(f, start_beat, level, decay, partials=((1, 1.0), (2, 0.25), (4, 0.08))):
    n = int(decay * 5 * SR)
    out = [0.0] * n
    for i in range(n):
        t = i / SR
        env = (1 - math.exp(-t / 0.004)) * math.exp(-t / decay)
        out[i] = level * env * sum(a * math.sin(TAU * f * p * t) for p, a in partials)
    add(int(start_beat * BEAT * SR), out)


def shaker(start_beat, level=0.018):
    n = int(0.09 * SR)
    prev = 0.0
    out = [0.0] * n
    for i in range(n):
        t = i / SR
        white = rng.uniform(-1, 1)
        hp = white - prev  # a little brighter than white noise
        prev = white
        out[i] = level * hp * math.exp(-t / 0.025) * min(1.0, t / 0.004)
    add(int(start_beat * BEAT * SR), out)


# Chords from the pentatonic notes only (no G, no C#): D, Bm7, Em7 (no third), A sus4, two bars each
CHORDS = [["D3", "F#3", "A3", "D4"], ["B2", "D3", "F#3", "A3"], ["E3", "B3", "D4", "F#4"], ["A2", "E3", "A3", "D4"]]
ROOTS = ["D2", "B1", "E2", "A1"]
for k, chord in enumerate(CHORDS):
    pad(chord, k * 8, 8)
    for b in (0, 2, 4, 6):
        pluck(hz(ROOTS[k]), k * 8 + b, 0.16, 0.35, partials=((1, 1.0), (2, 0.35), (3, 0.1)))

# The melody: eighth notes, "." holds or rests. A cheerful question and answer in D major pentatonic.
MELODY = [
    "A5 . F#5 A5 B5 . A5 .", "F#5 . E5 D5 E5 . . .",
    "D5 . F#5 A5 B5 . D6 .", "B5 . A5 F#5 A5 . . .",
    "B5 . A5 B5 D6 . B5 .", "A5 . F#5 E5 F#5 . . .",
    "E5 . F#5 A5 F#5 . E5 .", "D5 . . . . . . .",
]
for bar, line in enumerate(MELODY):
    for slot, name in enumerate(line.split()):
        if name != ".":
            pluck(hz(name), bar * 4 + slot * 0.5, 0.11, 0.45, partials=((1, 1.0), (3, 0.12), (4, 0.18)))  # a music box

for beat in range(BARS * 4):
    shaker(beat + 0.5)
    if beat % 4 in (1, 3):
        shaker(beat, level=0.01)

# Wrap the tails past the loop into its start, then level it.
loop = buf[:LOOP]
for i in range(LOOP, len(buf)):
    loop[i - LOOP] += buf[i]
peak = max(abs(v) for v in loop) or 1.0
scale = PEAK / peak
out = sys.argv[1]
with wave.open(out, "wb") as w:
    w.setnchannels(1)
    w.setsampwidth(2)
    w.setframerate(SR)
    w.writeframes(b"".join(struct.pack("<h", int(max(-1.0, min(1.0, v * scale)) * 32767)) for v in loop))
print(f"meadow theme: {LOOP / SR:.2f} s, {BARS} bars at {BPM:.0f} BPM -> {out}")
