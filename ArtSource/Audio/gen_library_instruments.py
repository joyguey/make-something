"""Synthesize the V4 instrument library: five lesser-known instruments that sit with the udu, mbira and erhu.

Owner, 2026-11-11: "a library of 5 more instruments that may sound harmonious together, include the ocarina and other
lesser-known instruments". All on D major pentatonic (D E F# A B), like the first three, so any of them can play with
any other; each in its own register and colour of sound:

  Ocarina   D5-B5   clay vessel flute: a pure, breathy, rounded tone with a soft vibrato (melody)
  Handpan   D4-B4   steel hand drum, tuned: a bell-like strike with its octave and compound-fifth partials, long ring
  Guzheng   D4-B4   Chinese plucked zither: a bright string pluck (additive harmonics) with a little press-bend
  Balafon   D4-B4   West African wooden xylophone: a dry wooden bar with the gourd's buzz underneath
  SlitDrum  D3-B3   hollow log (slit) drum: a warm, woody, short "tok" (bass percussion, pitched)

Original synthesis only; no samples. Fixed seeds: running it again reproduces the files byte for byte.
  python3 -I ArtSource/Audio/gen_library_instruments.py Assets/RoomOrchestra/Audio/Library
Writes <out>/<Instrument>/<Instrument>_<n>_<Note>.wav, n = 0..4 ascending (the layout SoundSets.Clips reads).
"""
import math
import os
import random
import struct
import sys
import wave

SR = 44100
PEAK = 0.5
TAU = 2 * math.pi
NAMES = ["D", "E", "Fs", "A", "B"]
SEMIS = [0, 2, 4, 7, 9]
D2, D3, D4, D5 = 73.42, 146.83, 293.66, 587.33


def scale(d):
    return [(NAMES[i], d * 2 ** (SEMIS[i] / 12)) for i in range(5)]


def write(path, s):
    m = max(abs(x) for x in s) or 1.0
    fade = int(0.02 * SR)
    for i in range(fade):
        s[-1 - i] *= i / fade
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", int(x / m * PEAK * 32767)) for x in s))


def lowpass(s, cutoff):
    a = 1 - math.exp(-TAU * cutoff / SR)
    y = 0.0
    out = []
    for x in s:
        y += a * (x - y)
        out.append(y)
    return out


def ocarina(f0, seed):
    # A breath-led vessel flute: soft onset, mostly the fundamental with a whisper of the 2nd and 3rd harmonics,
    # breath noise that fades as the tone settles, and a gentle vibrato that arrives late.
    random.seed(seed)
    n = int(1.4 * SR)
    noise = lowpass([random.uniform(-1, 1) for _ in range(n)], 2400)
    phase = 0.0
    s = []
    for i in range(n):
        t = i / SR
        vib = 1 + 0.006 * min(1.0, max(0.0, t - 0.25) / 0.3) * math.sin(TAU * 5.2 * t)
        phase += TAU * f0 * vib / SR
        env = min(1.0, t / 0.06) * (1.0 if t < 1.0 else max(0.0, 1 - (t - 1.0) / 0.4))
        tone = math.sin(phase) + 0.08 * math.sin(2 * phase) + 0.05 * math.sin(3 * phase)
        breath = noise[i] * (0.25 * math.exp(-t * 6) + 0.04)
        s.append(env * (tone + breath))
    return s


def handpan(f0, seed):
    # A tuned steel note field struck with the fingertip: fundamental, octave and compound fifth (the handpan's
    # tuning), a soft metallic attack and a long, slowly beating ring.
    random.seed(seed)
    n = int(2.6 * SR)
    partials = [(1.0, 1.0, 1.1), (2.0, 0.45, 1.6), (3.0, 0.22, 2.4), (1.003, 0.35, 1.0)]  # the last beats gently
    s = []
    for i in range(n):
        t = i / SR
        tone = sum(a * math.sin(TAU * f0 * r * t) * math.exp(-t * d) for r, a, d in partials)
        tap = random.uniform(-1, 1) * math.exp(-t * 300) * 0.25
        s.append(min(1.0, t / 0.003) * tone + tap)
    return s


def guzheng(f0, seed):
    # A plucked string, built from its harmonics so it is exactly in tune: plucked near the bridge (a bright comb of
    # harmonics), higher harmonics dying faster, a small pick noise, and the left hand's press-bend up into the note
    # (a guzheng's signature), fading over a second and a half.
    random.seed(seed)
    n = int(1.6 * SR)
    harmonics = [(k, (1.0 / k ** 1.1) * abs(math.sin(k * math.pi * 0.18)), 1.6 + 0.9 * k) for k in range(1, 14)]
    phases = [0.0] * len(harmonics)
    s = []
    for i in range(n):
        t = i / SR
        bend = 2 ** (-0.6 * math.exp(-t / 0.09) / 12)  # from a little flat up to the note
        v = 0.0
        for j, (k, a, d) in enumerate(harmonics):
            f = f0 * k * bend
            if f > SR * 0.45:
                continue
            phases[j] += TAU * f / SR
            v += a * math.sin(phases[j]) * math.exp(-t * d)
        pick = random.uniform(-1, 1) * math.exp(-t * 500) * 0.3
        s.append(min(1.0, t / 0.0015) * v + pick)
    return s


def balafon(f0, seed):
    # A rosewood bar over a gourd: the bar's fundamental with its inharmonic overtones (about 3.9 and 9.2 times),
    # a short dry decay, and the spider-silk membrane's buzz riding on the first moments.
    random.seed(seed)
    n = int(0.9 * SR)
    partials = [(1.0, 1.0, 5.0), (3.93, 0.3, 14.0), (9.2, 0.08, 30.0)]
    s = []
    for i in range(n):
        t = i / SR
        tone = sum(a * math.sin(TAU * f0 * r * t) * math.exp(-t * d) for r, a, d in partials)
        buzz = random.uniform(-1, 1) * 0.18 * math.exp(-t * 9) * (1 if math.sin(TAU * f0 * t) > 0.3 else 0)
        mallet = random.uniform(-1, 1) * math.exp(-t * 220) * 0.4
        s.append(min(1.0, t / 0.001) * tone + buzz + mallet)
    return lowpass(s, 6000)


def slit_drum(f0, seed):
    # A hollow log with a slit: a woody "tok", the tongue's tone with a quick drop in pitch as it is struck, a hollow
    # body resonance a twelfth above, short and warm.
    random.seed(seed)
    n = int(0.8 * SR)
    knock = lowpass([random.uniform(-1, 1) for _ in range(n)], 1500)
    phase = 0.0
    s = []
    for i in range(n):
        t = i / SR
        f = f0 * (1 + 0.06 * math.exp(-t * 40))
        phase += TAU * f / SR
        body = math.sin(phase) * math.exp(-t * 7) + 0.22 * math.sin(3 * phase) * math.exp(-t * 18)
        s.append(min(1.0, t / 0.002) * body + 1.2 * knock[i] * math.exp(-t * 90))
    return s


INSTRUMENTS = [
    ("Ocarina", ocarina, D5),
    ("Handpan", handpan, D4),
    ("Guzheng", guzheng, D4),
    ("Balafon", balafon, D4),
    ("SlitDrum", slit_drum, D3),
]

out = sys.argv[1]
for name, voice, root in INSTRUMENTS:
    folder = os.path.join(out, name)
    os.makedirs(folder, exist_ok=True)
    for n, (note, f) in enumerate(scale(root)):
        path = os.path.join(folder, f"{name}_{n}_{note}.wav")
        write(path, voice(f, seed=len(name) * 100 + n))
        print(f"{name} {note}: {path}")
