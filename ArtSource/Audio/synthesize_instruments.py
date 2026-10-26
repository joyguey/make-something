"""
Little Echoes instrument voices, synthesized from scratch (no samples, no downloads), so their
provenance is this script. They are INSPIRED by the udu, mbira and erhu for a game; they are not
recordings and make no claim to sound like a traditional instrument played by a musician.

  udu-inspired   clay-pot "bloop": low tone that swoops down in pitch, soft knock, hollow body
  mbira-inspired plucked metal tine: inharmonic overtones, a second tine beating against it, light buzz
  erhu-inspired  bowed two-string fiddle: nasal, swells in, bow noise, vibrato that blooms

Same notes, names, lengths (udu 0.9 s, mbira 1.6 s, erhu 1.1 s) and format (44.1 kHz, 16-bit mono) as
the prototype set, matched to its average loudness per instrument and never above -6 dBFS peak, so
the game can swap sets without changing the approved mix balance.
Deterministic (fixed seeds): running it again gives byte-identical files.

  python3 ArtSource/Audio/synthesize_instruments.py            -> Assets/RoomOrchestra/Audio/Synth/...
"""
import hashlib
import math
import os
import random
import struct
import wave

RATE = 44100
PEAK = 10 ** (-6 / 20)  # -6 dBFS
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "..", "Assets", "RoomOrchestra", "Audio", "Synth"))

NOTES = {
    "Udu": [("D2", 73.42), ("E2", 82.41), ("Fs2", 92.50), ("A2", 110.00), ("B2", 123.47)],
    "Mbira": [("D4", 293.66), ("E4", 329.63), ("Fs4", 369.99), ("A4", 440.00), ("B4", 493.88)],
    "Erhu": [("D5", 587.33), ("E5", 659.26), ("Fs5", 739.99), ("A5", 880.00), ("B5", 987.77)],
}
LENGTH = {"Udu": 0.9, "Mbira": 1.6, "Erhu": 1.1}


class OnePole:
    """Simple one-pole low-pass (cutoff in Hz)."""

    def __init__(self, cutoff):
        self.a = math.exp(-2 * math.pi * cutoff / RATE)
        self.y = 0.0

    def __call__(self, x):
        self.y = (1 - self.a) * x + self.a * self.y
        return self.y


class Resonator:
    """Two-pole band-pass resonance (a body or formant) at freq Hz with bandwidth bw Hz."""

    def __init__(self, freq, bw):
        r = math.exp(-math.pi * bw / RATE)
        self.b1 = 2 * r * math.cos(2 * math.pi * freq / RATE)
        self.b2 = -r * r
        self.g = 1 - r
        self.y1 = self.y2 = 0.0

    def __call__(self, x):
        y = self.g * x + self.b1 * self.y1 + self.b2 * self.y2
        self.y2, self.y1 = self.y1, y
        return y


def fade_tail(samples, seconds=0.03):
    n = int(seconds * RATE)
    for i in range(n):
        samples[-1 - i] *= i / n
    return samples


def udu(f, length, rng):
    n = int(length * RATE)
    out = []
    phase = phase2 = 0.0
    knock_lp = OnePole(900)
    body = Resonator(f * 3.1, 60)  # hollow pot resonance above the note
    for i in range(n):
        t = i / RATE
        swoop = 1 + 0.35 * math.exp(-t / 0.03)  # the "bloop": starts high, drops onto the note
        phase += 2 * math.pi * f * swoop / RATE
        phase2 += 2 * math.pi * f * 2.02 * swoop / RATE
        env = math.exp(-t / 0.22) * (1 - math.exp(-t / 0.004))
        tone = math.sin(phase) + 0.25 * math.sin(phase2) * math.exp(-t / 0.08)
        knock = knock_lp(rng.uniform(-1, 1)) * math.exp(-t / 0.012) * 2.5
        hollow = body(tone * env) * 6
        out.append(tone * env + knock + hollow * 0.4)
    return out


def mbira(f, length, rng):
    n = int(length * RATE)
    # Tine overtones are not whole-number multiples: they give the metallic, bell-like colour.
    partials = [(1.0, 1.0, 1.4), (1.0035, 0.35, 1.2), (5.93, 0.18, 0.18), (13.2, 0.06, 0.05)]
    phases = [0.0] * len(partials)
    click_lp = OnePole(4000)
    out = []
    for i in range(n):
        t = i / RATE
        s = 0.0
        for k, (ratio, amp, decay) in enumerate(partials):
            phases[k] += 2 * math.pi * f * ratio / RATE
            s += amp * math.sin(phases[k]) * math.exp(-t / decay)
        attack = 1 - math.exp(-t / 0.0015)
        click = click_lp(rng.uniform(-1, 1)) * math.exp(-t / 0.003) * 0.6
        buzz = rng.uniform(-1, 1) * abs(s) * 0.025 * math.exp(-t / 0.5)  # bottle-cap rattle riding the tone
        out.append(s * attack + click + buzz)
    return out


def erhu(f, length, rng):
    n = int(length * RATE)
    formants = [Resonator(1100, 300), Resonator(2600, 500), Resonator(f, 80)]  # nasal snakeskin body
    bow_lp = OnePole(5000)
    phase = 0.0
    out = []
    for i in range(n):
        t = i / RATE
        vib_depth = 0.006 * min(1.0, max(0.0, (t - 0.2) / 0.3))  # vibrato blooms after the bow settles
        freq = f * (1 + vib_depth * math.sin(2 * math.pi * 5.6 * t))
        phase += 2 * math.pi * freq / RATE
        # Bowed string: a soft sawtooth (harmonics falling off), slightly rounded.
        saw = sum(math.sin(phase * h) / h ** 0.9 for h in range(1, 16) if f * h < 10000)  # bright, nasal
        swell = min(1.0, t / 0.09) ** 1.5
        release = min(1.0, (length - t) / 0.18)
        bow = bow_lp(rng.uniform(-1, 1)) * 0.08 * (0.4 + 0.6 * math.exp(-t / 0.15))
        x = (saw * 0.35 + bow) * swell * release
        out.append(x * 0.5 + formants[0](x) * 1.4 + formants[1](x) * 1.6 + formants[2](x) * 0.6)
    return out


# Average loudness of the prototype set the owner approved (dBFS RMS), so the mix balance stays the same.
LOUDNESS = {"Udu": -20.0, "Mbira": -21.5, "Erhu": -13.4}


def normalize(samples, instrument):
    """Match the approved set's loudness, never peaking above -6 dBFS."""
    peak = max(abs(s) for s in samples) or 1.0
    rms = math.sqrt(sum(s * s for s in samples) / len(samples)) or 1.0
    gain = min(PEAK / peak, 10 ** (LOUDNESS[instrument] / 20) / rms)
    return [s * gain for s in samples]


def write(path, samples):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1.0, min(1.0, s)) * 32767)) for s in samples))
    with open(path, "rb") as fh:
        return hashlib.sha256(fh.read()).hexdigest()


def main():
    makers = {"Udu": udu, "Mbira": mbira, "Erhu": erhu}
    for instrument, notes in NOTES.items():
        for index, (name, freq) in enumerate(notes):
            rng = random.Random(f"{instrument}-{index}")  # fixed seed per note
            samples = fade_tail(normalize(makers[instrument](freq, LENGTH[instrument], rng), instrument))
            path = os.path.join(OUT, instrument, f"{instrument}_{index}_{name}.wav")
            digest = write(path, samples)
            print(f"{os.path.relpath(path, os.path.join(HERE, '..', '..'))}  sha256={digest}")


if __name__ == "__main__":
    main()
