"""Generates the game's sound effects procedurally (no third-party audio needed).

Run from the project root:  python Tools/generate_sounds.py
Writes 16-bit mono WAV files into Assets/Sounds/.
"""
import os
import wave

import numpy as np

RATE = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Sounds")


def tone(freq, dur, shape="sine"):
    t = np.arange(int(RATE * dur)) / RATE
    phase = 2 * np.pi * freq * t
    if shape == "square":
        return np.sign(np.sin(phase)) * 0.6
    if shape == "tri":
        return 2 / np.pi * np.arcsin(np.sin(phase))
    return np.sin(phase)


def env(n, attack=0.005, release=0.1):
    e = np.ones(n)
    a = max(1, int(RATE * attack))
    r = max(1, min(n - a, int(RATE * release)))
    e[:a] = np.linspace(0, 1, a)
    e[-r:] *= np.linspace(1, 0, r)
    return e


def decay(n, rate):
    return np.exp(-np.arange(n) / RATE * rate)


def noise(dur):
    return np.random.default_rng(7).uniform(-1, 1, int(RATE * dur))


def save(name, signal, gain=0.8):
    signal = signal / (np.max(np.abs(signal)) + 1e-9) * gain
    data = (signal * 32767).astype(np.int16)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(RATE)
        f.writeframes(data.tobytes())


def ui_click():
    s = tone(1800, 0.04, "tri") * decay(int(RATE * 0.04), 120)
    return s


def lever_pull():
    # Ratchet clicks followed by a spring "thunk".
    out = np.zeros(int(RATE * 0.45))
    for i in range(6):
        start = int(RATE * (0.02 + i * 0.035))
        click = noise(0.012) * decay(int(RATE * 0.012), 400)
        out[start:start + len(click)] += click * 0.6
    thunk = tone(90, 0.2) * decay(int(RATE * 0.2), 18)
    start = int(RATE * 0.24)
    out[start:start + len(thunk)] += thunk
    return out


def reel_spin_loop():
    # Mechanical ticking whirr. Exactly 0.5 s with whole tick periods so it loops seamlessly.
    dur = 0.5
    n = int(RATE * dur)
    out = np.zeros(n)
    ticks = 12
    for i in range(ticks):
        start = int(n * i / ticks)
        tick = noise(0.008) * decay(int(RATE * 0.008), 600) + tone(2400, 0.008) * decay(int(RATE * 0.008), 700) * 0.4
        out[start:start + len(tick)] += tick
    hum = tone(110, dur, "tri") * 0.08 + tone(220, dur) * 0.04
    return out + hum


def reel_stop():
    thunk = tone(140, 0.18) * decay(int(RATE * 0.18), 30)
    click = noise(0.02) * decay(int(RATE * 0.02), 250) * 0.7
    thunk[:len(click)] += click
    return thunk


def arpeggio(notes, step, sustain, shape="square"):
    total = int(RATE * (step * len(notes) + sustain))
    out = np.zeros(total)
    for i, f in enumerate(notes):
        seg = tone(f, sustain, shape) * env(int(RATE * sustain), 0.004, sustain * 0.8) * 0.5
        seg += tone(f * 2, sustain) * env(int(RATE * sustain), 0.004, sustain * 0.8) * 0.2
        start = int(RATE * step * i)
        out[start:start + len(seg)] += seg
    return out


def win_small():
    # C major arpeggio.
    return arpeggio([523.25, 659.25, 783.99, 1046.5], 0.08, 0.3)


def win_big():
    # Longer fanfare with a final held chord.
    run = arpeggio([523.25, 659.25, 783.99, 1046.5, 783.99, 1046.5, 1318.5], 0.09, 0.25)
    chord_len = 1.0
    chord = sum(tone(f, chord_len, "square") * 0.25 for f in (523.25, 659.25, 783.99, 1046.5))
    chord *= env(int(RATE * chord_len), 0.01, 0.7)
    out = np.zeros(len(run) + len(chord))
    out[:len(run)] += run
    out[int(RATE * 0.63):int(RATE * 0.63) + len(chord)] += chord
    return out


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    save("ui_click", ui_click(), 0.5)
    save("lever_pull", lever_pull(), 0.7)
    save("reel_spin_loop", reel_spin_loop(), 0.45)
    save("reel_stop", reel_stop(), 0.8)
    save("win_small", win_small(), 0.6)
    save("win_big", win_big(), 0.7)
    print("Sounds written to", os.path.abspath(OUT))
