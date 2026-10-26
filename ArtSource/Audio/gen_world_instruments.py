"""Synthesize Udu, Mbira, and Erhu one-shots on D major pentatonic (D E F# A B).

Each instrument gets its own register so the three never share a pitch:
  Udu   D2-B2 (bass)   Mbira D4-B4 (middle)   Erhu D5-B5 (melody)
Original synthesis only; no samples. Usage: python3 -I gen_world_instruments.py <out_dir>
Recovered 2026-10-26 from the 2026-10-15 Claude Code session that first made the game's sounds
(session 0ea50867, final version after a later erhu edit was reverted). Running it reproduces all
15 active WAVs in Assets/RoomOrchestra/Audio/ byte for byte (fixed seeds), so this script is their source.
Example: python3 -I ArtSource/Audio/gen_world_instruments.py /tmp/out
Writes <out_dir>/<Instrument>/<Instrument>_<n>_<Note>.wav, n = 0..4 ascending.
"""
import math, os, random, struct, sys, wave

SR = 44100
PEAK = 0.5  # about -6 dBFS
TAU = 2 * math.pi
out = sys.argv[1]

NAMES = ["D", "E", "Fs", "A", "B"]
SEMIS = [0, 2, 4, 7, 9]  # D major pentatonic


def scale(d_freq):
    return [(NAMES[i], d_freq * 2 ** (SEMIS[i] / 12)) for i in range(5)]


def write(path, s):
    m = max(abs(x) for x in s) or 1.0
    fade = int(0.01 * SR)
    for i in range(fade):
        s[-1 - i] *= i / fade
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", int(x / m * PEAK * 32767)) for x in s))


def lowpass(s, cutoff):
    a = 1 - math.exp(-TAU * cutoff / SR); y = 0.0; o = []
    for x in s:
        y += a * (x - y); o.append(y)
    return o


def udu(f0, seed):
    # Clay pot "bloop": side-hole resonance glides up into the note, a pot-body mode
    # an octave and a fifth above, and a soft low-passed palm slap.
    random.seed(seed); n = int(0.9 * SR); p1 = p2 = 0.0; s = []
    slap = lowpass([random.uniform(-1, 1) for _ in range(n)], 900)
    for i in range(n):
        t = i / SR
        f = f0 * (0.72 + 0.28 * (1 - math.exp(-t * 18)))
        p1 += TAU * f / SR; p2 += TAU * f * 3.0 / SR
        body = math.sin(p1) * math.exp(-t * 4.5) + 0.25 * math.sin(p2) * math.exp(-t * 11)
        s.append(body + 1.4 * slap[i] * math.exp(-t * 60))
    return s


def mbira(f0, seed):
    # Plucked steel tine with its octave partner, fast-dying inharmonic overtones,
    # a pluck click, and the bottle-cap buzz (machachara).
    random.seed(seed); n = int(1.6 * SR); s = []
    partials = [(1.0, 1.0, 2.4), (5.93, 0.28, 14.0), (2.0, 0.4, 3.0), (11.86, 0.08, 30.0)]
    for i in range(n):
        t = i / SR
        tone = sum(a * math.sin(TAU * f0 * r * t) * math.exp(-t * d) for r, a, d in partials)
        buzz = random.uniform(-1, 1) * 0.12 * math.exp(-t * 2.4) * (1 if math.sin(TAU * f0 * t) > 0.6 else 0)
        click = random.uniform(-1, 1) * math.exp(-t * 400) * 0.6
        s.append(min(1.0, t / 0.001) * tone + buzz + click)
    return s


def erhu(f0, seed):
    # One bowed note: slides up from ~1.5 semitones below (typical erhu ornament),
    # delayed vibrato, nasal formant emphasis on harmonics 2-4, and bow-hair noise.
    random.seed(seed); n = int(1.1 * SR); phase = 0.0; s = []
    for i in range(n):
        t = i / SR
        f = f0 * 2 ** (-1.5 / 12 * math.exp(-t / 0.035))
        vib = 1 + 0.012 * min(1.0, max(0.0, t - 0.15) / 0.2) * math.sin(TAU * 5.6 * t)
        phase += TAU * f * vib / SR
        h = sum(math.sin(k * phase) / k * (1.6 if 2 <= k <= 4 else 1.0) for k in range(1, 9))
        env = min(1.0, t / 0.07) * (1.0 if t < 0.85 else max(0.0, 1 - (t - 0.85) / 0.25))
        s.append(env * (h + random.uniform(-1, 1) * 0.06))
    return lowpass(s, 3500)


INSTRUMENTS = [("Udu", 73.42, 2, udu), ("Mbira", 293.66, 4, mbira), ("Erhu", 587.33, 5, erhu)]
for name, d_freq, octave, fn in INSTRUMENTS:
    os.makedirs(f"{out}/{name}", exist_ok=True)
    for i, (note, f) in enumerate(scale(d_freq)):
        write(f"{out}/{name}/{name}_{i}_{note}{octave}.wav", fn(f, i + 1))
print("ok")
