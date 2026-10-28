"""Synthesize the two poof sounds: a soft puff when a character lands on the floor, and a bigger
smoky whoosh when Clear sweeps the rainforest away.

Original synthesis only (filtered noise and a gliding sine); no samples. Fixed seeds, so every run
writes the same bytes. Usage: python3 -I ArtSource/Audio/gen_poof.py <out_dir>
Writes <out_dir>/Poof.wav and <out_dir>/Smoke.wav (mono, 16-bit, 44.1 kHz).
Gentle on purpose: soft attacks, no high hiss, peak -6 dBFS before the game's own volume.
"""
import math, random, struct, sys, wave

SR = 44100
PEAK = 0.5  # about -6 dBFS, like the instrument set
TAU = 2 * math.pi


def write(path, s):
    m = max(abs(x) for x in s) or 1.0
    fade = int(0.02 * SR)
    for i in range(fade):
        s[-1 - i] *= i / fade
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", int(x / m * PEAK * 32767)) for x in s))


def breath(seconds, seed, f_start, f_end, q, attack, decay):
    """Noise through a band-pass whose centre glides down: the 'ffff' of air."""
    rnd = random.Random(seed)
    n = int(seconds * SR)
    low = band = 0.0
    soft1 = soft2 = 0.0
    a = 1 - math.exp(-TAU * 1500 / SR)  # two gentle low-pass stages above the band: air, not hiss
    out = []
    for i in range(n):
        t = i / SR
        f = f_end + (f_start - f_end) * math.exp(-t / (seconds * 0.35))
        k = 2 * math.sin(math.pi * f / SR)  # state-variable filter
        x = rnd.uniform(-1, 1)
        low += k * band
        high = x - low - band / q
        band += k * high
        env = min(1.0, t / attack) * math.exp(-max(0.0, t - attack) / decay)
        soft1 += a * (band - soft1)
        soft2 += a * (soft1 - soft2)
        out.append(soft2 * env)
    return out


def thump(seconds, f_start, f_end, glide, attack, decay):
    """A soft low sine that falls in pitch: the 'puh' under the air."""
    n = int(seconds * SR)
    phase = 0.0
    out = []
    for i in range(n):
        t = i / SR
        f = f_end + (f_start - f_end) * math.exp(-t / glide)
        phase += TAU * f / SR
        env = min(1.0, t / attack) * math.exp(-max(0.0, t - attack) / decay)
        out.append(math.sin(phase) * env)
    return out


def mix(*layers):
    n = max(len(l) for l, _ in layers)
    return [sum(l[i] * g for l, g in layers if i < len(l)) for i in range(n)]


out = sys.argv[1]
# Landing poof: short, round, a little cartoon 'pff'.
write(f"{out}/Poof.wav", mix(
    (breath(0.45, 11, 1800, 450, 1.4, 0.006, 0.08), 1.0),
    (thump(0.45, 170, 90, 0.05, 0.003, 0.05), 0.5)))
# Clear smoke: slower swell (never startling), longer and lower.
write(f"{out}/Smoke.wav", mix(
    (breath(1.2, 23, 1200, 280, 1.0, 0.06, 0.28), 1.0),
    (thump(1.2, 120, 65, 0.12, 0.02, 0.18), 0.35)))
