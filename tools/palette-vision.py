"""Checks the AvCharts palette for red-green colour blindness.

Reads the series colours out of the shipped theme - so the check cannot drift from
what the library actually draws - simulates deuteranopia (the common red-green
deficiency, about 6% of men), and reports how far apart the colours still are.
Also writes a sheet with the original and simulated palettes side by side, which is
the half of this that a number cannot settle.

Two things are measured:
  - how far apart the ten series colours are from each other, in CIE76 dE;
    roughly, 2.3 is just noticeable and 10 is plainly different
  - how much each colour stands out from its own surface, as a contrast ratio
    (a colour that disappears into the background cannot be told apart from
    another colour that also disappears into it)

Run with the bundled Python:
    python tools/palette-vision.py
Output:
    tools/palette-vision/palette-vision.png
"""

import re
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

THEME = Path(__file__).resolve().parent.parent / "src/AvCharts.Controls/Themes/ChartTheme.axaml"

# Machado, Oliveira and Fernandes (2009), deuteranopia at full severity: the matrix
# the simulation projects linear RGB through. Same numbers as every other
# implementation of that paper.
DEUTERANOPIA = [
    [0.367322, 0.860646, -0.227968],
    [0.280085, 0.672501, 0.047413],
    [-0.011820, 0.042940, 0.968881],
]

# Below this the two colours read as the same one; at 10 and up they are plainly apart.
APART = 10.0
# Below this a mark is hard to pick out of the surface it is drawn on.
VISIBLE = 1.6


def read_theme():
    """Pulls the series colours and the surface out of the theme file."""
    variants = {}
    current = None

    for line in THEME.read_text(encoding="utf-8").splitlines():
        header = re.search(r'<ResourceDictionary x:Key="(Light|Dark)"', line)
        if header:
            current = header.group(1)
            variants[current] = {"strokes": {}, "surface": None}
            continue
        if current is None:
            continue

        stroke = re.search(r'x:Key="Series\.Stroke(\d+)" Brush="#([0-9A-Fa-f]{6})"', line)
        if stroke:
            variants[current]["strokes"][int(stroke.group(1))] = stroke.group(2).upper()
            continue

        surface = re.search(r'x:Key="Surface\.Background" Color="#([0-9A-Fa-f]{6})"', line)
        if surface:
            variants[current]["surface"] = surface.group(1).upper()

    if set(variants) != {"Light", "Dark"}:
        sys.exit(f"could not read both variants out of {THEME}")

    return variants


def rgb(value):
    if not isinstance(value, str):
        return value

    value = value.lstrip("#")
    return tuple(int(value[i : i + 2], 16) / 255 for i in (0, 2, 4))


def to_linear(channel):
    return channel / 12.92 if channel <= 0.04045 else ((channel + 0.055) / 1.055) ** 2.4


def to_srgb(channel):
    channel = min(1.0, max(0.0, channel))
    return 12.92 * channel if channel <= 0.0031308 else (1.055 * channel ** (1 / 2.4)) - 0.055


def hexed(colour):
    return "#" + "".join(f"{round(channel * 255):02X}" for channel in colour)


def bytes_of(value):
    return tuple(round(channel * 255) for channel in rgb(value))


def simulate(value):
    """What a deuteranope sees: linear RGB through the projection, back to sRGB."""
    linear = [to_linear(channel) for channel in rgb(value)]
    seen = [sum(row[i] * linear[i] for i in range(3)) for row in DEUTERANOPIA]
    return tuple(to_srgb(channel) for channel in seen)


def luminance(value):
    linear = [to_linear(channel) for channel in rgb(value)]
    return (0.2126 * linear[0]) + (0.7152 * linear[1]) + (0.0722 * linear[2])


def contrast(first, second):
    a, b = luminance(first), luminance(second)
    lighter, darker = max(a, b), min(a, b)
    return (lighter + 0.05) / (darker + 0.05)


def lab(value):
    """CIELAB, so a distance means something perceptual rather than channel-wise."""
    linear = [to_linear(channel) for channel in rgb(value)]

    x = (0.4124 * linear[0] + 0.3576 * linear[1] + 0.1805 * linear[2]) / 0.95047
    y = 0.2126 * linear[0] + 0.7152 * linear[1] + 0.0722 * linear[2]
    z = (0.0193 * linear[0] + 0.1192 * linear[1] + 0.9505 * linear[2]) / 1.08883

    def curve(t):
        return t ** (1 / 3) if t > 0.008856 else (7.787 * t) + (16 / 116)

    fx, fy, fz = curve(x), curve(y), curve(z)
    return ((116 * fy) - 16, 500 * (fx - fy), 200 * (fy - fz))


def distance(first, second):
    a, b = lab(first), lab(second)
    return sum((a[i] - b[i]) ** 2 for i in range(3)) ** 0.5


def report_variant(name, strokes, surface):
    seen = [simulate(colour) for colour in strokes.values()]

    pairs = []
    for i in range(len(seen)):
        for j in range(i + 1, len(seen)):
            pairs.append((distance(seen[i], seen[j]), i + 1, j + 1))
    pairs.sort()

    # What matters most in practice is the series next to each other in the palette: those are the two a
    # reader compares without moving their eye far.
    neighbours = sorted((distance(seen[i], seen[i + 1]), i + 1, i + 2) for i in range(len(seen) - 1))

    contrasts = [(contrast(colour, surface), slot) for slot, colour in strokes.items()]
    contrasts.sort()

    print(f"\n{name} (surface {surface})")
    print("  closest neighbours once simulated (slot, slot, dE):")
    for d, first, second in neighbours[:3]:
        mark = "  <-- too close" if d < APART else ""
        print(f"    {first:2d} {second:2d}  {d:5.1f}{mark}")
    print("  closest of any two slots once simulated:")
    for d, first, second in pairs[:4]:
        mark = "  <-- too close" if d < APART else ""
        print(f"    {first:2d} {second:2d}  {d:5.1f}{mark}")
    print(f"  minimum {pairs[0][0]:.1f}, minimum contrast {contrasts[0][0]:.2f} (slot {contrasts[0][1]})")

    return neighbours[0][0], pairs[0][0], contrasts[0][0], seen


def rows(name, strokes, seen, cell_w=62, cell_h=34):
    """Two rows of swatches - what the palette is, and what it looks like simulated."""
    width = cell_w * len(strokes)
    image = Image.new("RGB", (width, cell_h * 2 + 4), "#FFFFFF")
    draw = ImageDraw.Draw(image)
    font = ImageFont.load_default()

    for index, (slot, colour) in enumerate(strokes.items()):
        x = index * cell_w
        draw.rectangle([x, 0, x + cell_w - 3, cell_h], fill=bytes_of(colour))
        draw.rectangle([x, cell_h + 2, x + cell_w - 3, (cell_h * 2) + 2], fill=bytes_of(seen[index]))
        draw.text((x + 4, 4), f"{name}{slot}", font=font, fill="#FFFFFF" if luminance(colour) < 0.4 else "#222222")
        draw.text((x + 4, cell_h + 6), hexed(seen[index]), font=font, fill="#222222")

    return image


def build():
    variants = read_theme()
    height = 0
    parts = []

    for name in ("Light", "Dark"):
        strokes = variants[name]["strokes"]
        if sorted(strokes) != list(range(1, 11)):
            sys.exit(f"{name} does not hold ten series colours")

        neighbours, minimum, minimum_contrast, seen = report_variant(name, strokes, variants[name]["surface"])
        parts.append((name, neighbours, minimum, minimum_contrast, rows(name[0], strokes, seen)))

    width = max(part.width for _, _, _, _, part in parts) + 20
    height = 34 + sum(part.height + 26 for _, _, _, _, part in parts)
    sheet = Image.new("RGB", (width, height), "#FFFFFF")
    draw = ImageDraw.Draw(sheet)
    title = ImageFont.load_default()
    draw.text((10, 9), "AvCharts palette under deuteranopia (top row: the colours, bottom: simulated)", font=title, fill="#111111")

    y = 30
    for name, neighbours, minimum, minimum_contrast, part in parts:
        sheet.paste(part, (10, y))
        y += part.height + 4
        draw.text(
            (10, y),
            f"{name}: closest neighbours dE {neighbours:.1f}, closest pair dE {minimum:.1f}, "
            f"closest contrast {minimum_contrast:.2f}",
            font=title,
            fill="#111111",
        )
        y += 22

    return sheet


if __name__ == "__main__":
    out = Path(__file__).parent / "palette-vision"
    out.mkdir(exist_ok=True)
    target = out / "palette-vision.png"
    build().save(target)
    print(f"\n{target}")
