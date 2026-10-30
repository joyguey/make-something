"""Synthesize each character's own bounce: the sound a ball makes when it bounces off a wall (owner, V3 list:
"balls make their own sound when they hit/bounce off walls"). Floor landings stay silent (owner 2026-10-28) and
instrument hits play the instrument, so these are only for walls and ceilings.

Original synthesis only (a pitch-dropping sine with a soft body and a short click); no samples. Deterministic:
every run writes the same bytes. Usage: python3 -I ArtSource/Audio/gen_bounce.py <out_dir>
Writes <out_dir>/Bounce_Flutter.wav (light, high "plink"), Bounce_Bop.wav (round "bop"), Bounce_Boom.wav (low
"bwomp"): mono, 16-bit, 44.1 kHz, peak about -6 dBFS, soft attack, short (no ringing).
"""
import math, struct, sys, wave

SR = 44100
PEAK = 0.5
TAU = 2 * math.pi

# name: (start Hz, end Hz, seconds, drop time constant (s), body (2nd harmonic share), click share)
VOICES = {
    "Flutter": (1320.0, 880.0, 0.12, 0.025, 0.15, 0.10),
    "Bop": (520.0, 330.0, 0.17, 0.04, 0.30, 0.06),
    "Boom": (190.0, 95.0, 0.26, 0.07, 0.45, 0.04),
}


def voice(f0, f1, seconds, tau, body, click):
    n = int(seconds * SR)
    out, phase = [], 0.0
    for i in range(n):
        t = i / SR
        f = f1 + (f0 - f1) * math.exp(-t / tau)  # the "boing": pitch falls as it squashes and lets go
        phase += TAU * f / SR
        attack = min(1.0, t / 0.004)              # 4 ms: soft, never a hard tick
        env = attack * math.exp(-t / (seconds * 0.28))
        s = math.sin(phase) + body * math.sin(2 * phase + 0.6)
        s += click * math.sin(TAU * 2600 * t) * math.exp(-t / 0.006)  # a tiny contact click
        out.append(s * env)
    return out


def write(path, s):
    m = max(abs(x) for x in s) or 1.0
    fade = int(0.01 * SR)
    for i in range(fade):
        s[-1 - i] *= i / fade
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", int(x / m * PEAK * 32767)) for x in s))


if __name__ == "__main__":
    out = sys.argv[1]
    for name, params in VOICES.items():
        write(f"{out}/Bounce_{name}.wav", voice(*params))
        print(f"{out}/Bounce_{name}.wav")
