"""Generates a seamless, upbeat casino-lounge background loop (no third-party audio).

Run from the project root:  python Tools/generate_music.py
Writes Assets/Sounds/bgm_casino_loop.wav (mono, 22.05 kHz, 16 bars at 124 BPM).

Arrangement: swung hi-hats, soft kick, walking bass, electric-piano chord stabs and a
bell melody over a I-vi-ii-V progression in C, twice through with a variation.
Note tails that run past the end are wrapped to the start so the loop has no click.
"""
import os
import wave

import numpy as np

RATE = 22050
BPM = 124
BEAT = 60 / BPM
BARS = 16
LENGTH = int(RATE * BEAT * 4 * BARS)
OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Sounds", "bgm_casino_loop.wav")

rng = np.random.default_rng(3)


def midi(note):
    return 440.0 * 2 ** ((note - 69) / 12)


def adsr(n, a=0.01, d=0.1, s=0.6, r=0.15):
    e = np.full(n, s)
    a_n, d_n, r_n = int(RATE * a), int(RATE * d), int(RATE * r)
    a_n = max(1, min(a_n, n))
    e[:a_n] = np.linspace(0, 1, a_n)
    d_end = min(n, a_n + d_n)
    e[a_n:d_end] = np.linspace(1, s, d_end - a_n)
    r_n = max(1, min(r_n, n))
    e[-r_n:] *= np.linspace(1, 0, r_n)
    return e


def add(track, start_time, signal):
    """Mix a signal into the loop, wrapping anything past the end back to the start."""
    start = int(start_time * RATE) % LENGTH
    end = start + len(signal)
    if end <= LENGTH:
        track[start:end] += signal
    else:
        first = LENGTH - start
        track[start:] += signal[:first]
        track[:len(signal) - first] += signal[first:]


def epiano(freq, dur):
    n = int(RATE * dur)
    t = np.arange(n) / RATE
    tone = np.sin(2 * np.pi * freq * t) + 0.35 * np.sin(2 * np.pi * freq * 2 * t) * np.exp(-t * 6)
    tremolo = 1 + 0.08 * np.sin(2 * np.pi * 5 * t)
    return tone * tremolo * adsr(n, 0.005, 0.25, 0.35, 0.2)


def bass(freq, dur):
    n = int(RATE * dur)
    t = np.arange(n) / RATE
    tone = 2 / np.pi * np.arcsin(np.sin(2 * np.pi * freq * t)) + 0.3 * np.sin(2 * np.pi * freq * 2 * t)
    return tone * adsr(n, 0.005, 0.08, 0.7, 0.05)


def bell(freq, dur):
    n = int(RATE * dur)
    t = np.arange(n) / RATE
    tone = np.sin(2 * np.pi * freq * t) + 0.4 * np.sin(2 * np.pi * freq * 3.01 * t) * np.exp(-t * 8)
    return tone * np.exp(-t * 3.5) * adsr(n, 0.002, 0.05, 1.0, 0.05)


def kick():
    n = int(RATE * 0.22)
    t = np.arange(n) / RATE
    freq = 110 * np.exp(-t * 18) + 45
    return np.sin(2 * np.pi * np.cumsum(freq) / RATE) * np.exp(-t * 14)


def hat(open_hat=False):
    n = int(RATE * (0.12 if open_hat else 0.04))
    noise = rng.uniform(-1, 1, n)
    noise = np.diff(noise, prepend=0)  # crude high-pass
    return noise * np.exp(-np.arange(n) / RATE * (25 if open_hat else 90))


def snap():
    n = int(RATE * 0.08)
    return rng.uniform(-1, 1, n) * np.exp(-np.arange(n) / RATE * 45)


# I - vi - ii - V7 in C. (root, chord tones as MIDI notes)
PROGRESSION = [
    (48, [60, 64, 67, 71]),   # Cmaj7
    (45, [57, 60, 64, 67]),   # Am7
    (50, [62, 65, 69, 72]),   # Dm7
    (43, [59, 62, 65, 67]),   # G7
]

# Two-bar melody phrases (beat offset, midi note, length in beats), chosen to fit each chord.
MELODY_A = [(0, 76, 1), (1, 79, 0.5), (1.5, 76, 0.5), (2, 72, 1.5), (4, 74, 1), (5, 76, 1), (6, 72, 2)]
MELODY_B = [(0, 81, 1), (1, 79, 0.5), (1.5, 77, 0.5), (2, 76, 1), (3, 74, 1), (4, 74, 0.5), (4.5, 76, 0.5),
            (5, 77, 1), (6, 79, 2)]


def swing(beat_offset):
    """Delay every off-beat eighth note slightly for a relaxed swing feel."""
    whole, frac = divmod(beat_offset, 1)
    return whole + (0.62 if abs(frac - 0.5) < 1e-6 else frac)


def build():
    drums = np.zeros(LENGTH)
    bass_track = np.zeros(LENGTH)
    keys = np.zeros(LENGTH)
    lead = np.zeros(LENGTH)

    for bar in range(BARS):
        bar_time = bar * 4 * BEAT
        root, chord = PROGRESSION[bar % 4]
        second_half = bar >= 8

        # Drums: kick on 1 & 3, snap on 2 & 4, swung hats on every eighth.
        for beat in (0, 2):
            add(drums, bar_time + beat * BEAT, kick() * 0.9)
        for beat in (1, 3):
            add(drums, bar_time + beat * BEAT, snap() * 0.25)
        for eighth in range(8):
            offset = swing(eighth * 0.5)
            add(drums, bar_time + offset * BEAT, hat(open_hat=(eighth == 7)) * (0.22 if eighth % 2 else 0.14))

        # Walking bass: root, fifth, octave, approach note into the next chord.
        next_root = PROGRESSION[(bar + 1) % 4][0]
        walk = [root, root + 7, root + 12, next_root + (1 if next_root < root else -1)]
        for beat, note in enumerate(walk):
            add(bass_track, bar_time + beat * BEAT, bass(midi(note), BEAT * 0.9))

        # Chord stabs on the swung "and" of 2 and on 4.
        for beat in (1.5, 3) if not second_half else (0, 1.5, 3):
            start = bar_time + swing(beat) * BEAT
            stab = sum(epiano(midi(n), BEAT * 0.9) for n in chord) / len(chord)
            add(keys, start, stab)

        # Melody: one phrase every two bars, alternating A and B.
        if bar % 2 == 0:
            phrase = MELODY_A if (bar // 2) % 2 == 0 else MELODY_B
            for offset, note, length in phrase:
                pitch = note + (12 if second_half and bar % 4 == 2 else 0)
                add(lead, bar_time + swing(offset) * BEAT, bell(midi(pitch), length * BEAT + 0.3))

    mix = drums * 0.55 + bass_track * 0.5 + keys * 0.45 + lead * 0.3
    mix /= np.max(np.abs(mix)) + 1e-9
    # Gentle soft clip for warmth, then normalise to leave headroom under the sound effects.
    mix = np.tanh(mix * 1.3)
    mix = mix / np.max(np.abs(mix)) * 0.7
    return mix


if __name__ == "__main__":
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    data = (build() * 32767).astype(np.int16)
    with wave.open(OUT, "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(RATE)
        f.writeframes(data.tobytes())
    print("Music written to", os.path.abspath(OUT), f"({LENGTH / RATE:.1f} s)")
