"""Generates the effect sprites (glow, bulb, coin, sunburst, star) used for game feel.

Run from the project root:  python Tools/generate_fx_sprites.py
Writes PNGs into Assets/UI/Sprites/FX/.
"""
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "UI", "Sprites", "FX")


def radial(size, power):
    y, x = np.mgrid[0:size, 0:size]
    c = (size - 1) / 2
    d = np.sqrt((x - c) ** 2 + (y - c) ** 2) / c
    return np.clip(1 - d, 0, 1) ** power


def glow():
    # Soft white radial gradient, tinted in Unity.
    size = 256
    alpha = (radial(size, 2.2) * 255).astype(np.uint8)
    img = np.zeros((size, size, 4), np.uint8)
    img[..., :3] = 255
    img[..., 3] = alpha
    Image.fromarray(img).save(os.path.join(OUT, "fx_glow.png"))


def bulb():
    # White bulb with a bright core and soft halo, tinted per state in Unity.
    size = 64
    halo = radial(size, 1.6) * 0.55
    core = np.clip((1 - np.sqrt(((np.mgrid[0:size, 0:size] - 31.5) ** 2).sum(0)) / 14), 0, 1) ** 0.6
    alpha = np.clip(np.maximum(halo, core), 0, 1)
    img = np.zeros((size, size, 4), np.uint8)
    img[..., :3] = 255
    img[..., 3] = (alpha * 255).astype(np.uint8)
    Image.fromarray(img).save(os.path.join(OUT, "fx_bulb.png"))


def coin():
    size = 128
    s = 4  # supersample for smooth edges
    big = Image.new("RGBA", (size * s, size * s), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)
    r = size * s // 2
    d.ellipse([4 * s, 4 * s, size * s - 4 * s, size * s - 4 * s], fill=(176, 110, 18, 255))      # dark rim
    d.ellipse([10 * s, 10 * s, size * s - 10 * s, size * s - 10 * s], fill=(255, 196, 48, 255))  # face
    d.ellipse([22 * s, 22 * s, size * s - 22 * s, size * s - 22 * s], outline=(214, 140, 24, 255), width=5 * s)
    # Star-ish shine
    d.ellipse([34 * s, 26 * s, 52 * s, 36 * s], fill=(255, 240, 180, 200))
    # Embossed "$"-free mark: a simple diamond so it reads as a token, not a currency.
    cx = cy = r
    k = 18 * s
    d.polygon([(cx, cy - k), (cx + k * 0.7, cy), (cx, cy + k), (cx - k * 0.7, cy)], fill=(232, 156, 30, 255))
    img = big.resize((size, size), Image.LANCZOS)
    img.save(os.path.join(OUT, "fx_coin.png"))


def rays():
    # Sunburst: 16 soft wedges fading out from the centre. Rotated slowly in Unity.
    size = 512
    y, x = np.mgrid[0:size, 0:size]
    c = (size - 1) / 2
    angle = np.arctan2(y - c, x - c)
    dist = np.sqrt((x - c) ** 2 + (y - c) ** 2) / c
    wedges = (np.cos(angle * 16) * 0.5 + 0.5) ** 1.6
    fade = np.clip(1 - dist, 0, 1) ** 1.2 * np.clip(dist * 4, 0, 1)
    alpha = np.clip(wedges * fade, 0, 1)
    img = np.zeros((size, size, 4), np.uint8)
    img[..., :3] = 255
    img[..., 3] = (alpha * 255).astype(np.uint8)
    Image.fromarray(img).save(os.path.join(OUT, "fx_rays.png"))


def star():
    # Four-point twinkle star with a soft core.
    size = 64
    y, x = np.mgrid[0:size, 0:size]
    c = (size - 1) / 2
    dx, dy = np.abs(x - c) / c, np.abs(y - c) / c
    cross = np.clip(1 - (dx * 8 + dy), 0, 1) + np.clip(1 - (dy * 8 + dx), 0, 1)
    core = np.clip(1 - np.sqrt(dx ** 2 + dy ** 2) * 2.5, 0, 1) ** 1.5
    alpha = np.clip(cross + core, 0, 1)
    img = np.zeros((size, size, 4), np.uint8)
    img[..., :3] = 255
    img[..., 3] = (alpha * 255).astype(np.uint8)
    Image.fromarray(img).save(os.path.join(OUT, "fx_star.png"))


def round_button():
    # Casino-style round button: gold rim, red glossy face. Text is added in Unity.
    size = 256
    y, x = np.mgrid[0:size, 0:size]
    c = (size - 1) / 2
    d = np.sqrt((x - c) ** 2 + (y - c) ** 2) / c
    img = np.zeros((size, size, 4), np.float64)

    rim = (d <= 1.0) & (d > 0.84)
    face = d <= 0.84
    # Gold rim with a vertical light-to-dark gradient.
    shade = 1 - (y / size) * 0.45
    img[rim] = np.stack([255 * shade, 200 * shade, 60 * shade, np.full_like(shade, 255)], -1)[rim]
    # Red face: radial gradient, brighter towards the top.
    light = np.clip(1.15 - d * 0.6 - (y / size) * 0.35, 0, 1)
    img[face] = np.stack([60 + 195 * light, 10 + 40 * light, 30 + 30 * light, np.full_like(light, 255)], -1)[face]
    # Glossy highlight on the upper half.
    gloss = ((x - c) / (c * 0.62)) ** 2 + ((y - c * 0.55) / (c * 0.34)) ** 2 <= 1
    img[gloss, :3] = img[gloss, :3] * 0.6 + 255 * 0.4
    # Anti-aliased outer edge.
    img[..., 3] *= np.clip((1.0 - d) * c, 0, 1)
    Image.fromarray(img.clip(0, 255).astype(np.uint8)).save(os.path.join(OUT, "ui_round_button.png"))


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    glow()
    bulb()
    coin()
    rays()
    star()
    round_button()
    print("FX sprites written to", os.path.abspath(OUT))
