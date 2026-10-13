"""Renders a preview sheet for the AvCharts palette candidates.

Standalone decision aid: draws the light and dark candidate palettes as swatch
rows plus a small chart mock (major/minor grid, axis band, five series lines,
legend chips) so the colours can be judged in context instead of as hex codes.

Run with the bundled Python:
    python tools/palette-preview.py
Output:
    tools/palette-preview/palette-preview.png
"""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

LIGHT_STROKES = [
    "#F48420", "#AE408E", "#30BC9A", "#209FD9", "#264B93",
    "#7A9A2E", "#C25E5E", "#7C6BB8", "#B08A3E", "#5E7387",
]
DARK_STROKES = [
    "#FF9E45", "#D06BAE", "#45D6B2", "#4CB6E8", "#6C8FD8",
    "#A8C05C", "#E08080", "#9E90D8", "#D8B45E", "#8C9CAC",
]

# SciChart uses stroke @ 0x77 (47%) for the matching fill role.
FILL_ALPHA = 0x77

LIGHT = {
    "surface": "#F9F9F9",
    "band": "#AAAAAA",
    "band_alpha": 0.035,
    "major": "#CFCFCF",
    "minor": "#CFCFCF",
    "minor_alpha": 0.47,
    "text": "#333333",
    "axis": "#333333",
    "axis_alpha": 0.60,
    "title": "#777777",
    "legend": "#333333",
    "legend_alpha": 0.20,
}
DARK = {
    "surface": "#1C1C1E",
    "band": "#202123",
    "band_alpha": 0.88,
    "major": "#323539",
    "minor": "#232426",
    "minor_alpha": 1.0,
    "text": "#A6A7AC",
    "axis": "#5A5B5B",
    "axis_alpha": 1.0,
    "title": "#C8C7C3",
    "legend": "#26272B",
    "legend_alpha": 1.0,
}


def rgb(value: str):
    value = value.lstrip("#")
    return tuple(int(value[i : i + 2], 16) for i in (0, 2, 4))


def blend(top: str, bottom: str, alpha: float):
    a, b = rgb(top), rgb(bottom)
    return tuple(round(a[i] * alpha + b[i] * (1 - alpha)) for i in range(3))


def font(size: int):
    for candidate in ("C:/Windows/Fonts/segoeui.ttf", "C:/Windows/Fonts/arial.ttf"):
        if Path(candidate).exists():
            return ImageFont.truetype(candidate, size)
    return ImageFont.load_default()


def chart_mock(theme: dict, strokes, width: int, height: int) -> Image.Image:
    image = Image.new("RGB", (width, height), rgb(theme["surface"]))
    draw = ImageDraw.Draw(image, "RGBA")
    pad = 34

    # alternating axis bands, one per major interval, driven by the theme token
    cell = 60
    band = blend(theme["band"], theme["surface"], theme["band_alpha"])
    for i in range(0, width // cell + 1):
        if i % 2:
            draw.rectangle([i * cell, 0, (i + 1) * cell, height], fill=band)

    minor = blend(theme["minor"], theme["surface"], theme["minor_alpha"])
    for x in range(0, width + 1, 12):
        draw.line([(x, 0), (x, height)], fill=minor, width=1)
    for y in range(0, height + 1, 12):
        draw.line([(0, y), (width, y)], fill=minor, width=1)
    for x in range(0, width + 1, cell):
        draw.line([(x, 0), (x, height)], fill=rgb(theme["major"]), width=1)
    for y in range(0, height + 1, cell):
        draw.line([(0, y), (width, y)], fill=rgb(theme["major"]), width=1)

    axis = blend(theme["axis"], theme["surface"], theme["axis_alpha"])
    draw.line([(pad, height - pad), (width - 8, height - pad)], fill=axis, width=2)
    draw.line([(pad, 8), (pad, height - pad)], fill=axis, width=2)
    tick_font = font(11)
    for i in range(6):
        x = pad + i * (width - pad - 8) / 5
        draw.line([(x, height - pad), (x, height - pad + 5)], fill=axis, width=1)
        draw.text((x - 10, height - pad + 7), f"{i * 20}", font=tick_font, fill=rgb(theme["text"]))

    series = [
        [0.62, 0.48, 0.55, 0.34, 0.40, 0.22],
        [0.35, 0.42, 0.30, 0.45, 0.52, 0.38],
        [0.78, 0.70, 0.74, 0.60, 0.66, 0.58],
        [0.20, 0.30, 0.24, 0.36, 0.30, 0.44],
        [0.50, 0.62, 0.46, 0.68, 0.58, 0.72],
    ]
    for index, values in enumerate(series):
        colour = rgb(strokes[index])
        points = [
            (pad + i * (width - pad - 8) / 5, 8 + (1 - v) * (height - 2 * pad - 8))
            for i, v in enumerate(values)
        ]
        if index == 0:
            polygon = points + [(points[-1][0], height - pad), (points[0][0], height - pad)]
            draw.polygon(polygon, fill=(*colour, FILL_ALPHA))
        draw.line(points, fill=colour, width=2, joint="curve")
        for x, y in points:
            draw.ellipse([x - 3, y - 3, x + 3, y + 3], fill=rgb(theme["surface"]), outline=colour, width=2)

    chip_x = pad + 4
    chips = blend(theme["legend"], theme["surface"], theme["legend_alpha"])
    for index, colour in enumerate(strokes):
        draw.rectangle([chip_x, 4, chip_x + 12, 12], fill=chips)
        draw.rectangle([chip_x + 2, 6, chip_x + 9, 10], fill=rgb(colour))
        draw.text((chip_x + 16, 3), f"S{index + 1}", font=tick_font, fill=rgb(theme["title"]))
        chip_x += 44
    return image


def swatch_rows(theme: dict, strokes, label: str, cell_w: int = 62, cell_h: int = 40) -> Image.Image:
    width = cell_w * len(strokes)
    image = Image.new("RGB", (width, cell_h * 3 + 6), rgb(theme["surface"]))
    draw = ImageDraw.Draw(image)
    label_font = font(9)
    for index, stroke in enumerate(strokes):
        x = index * cell_w
        draw.rectangle([x, 0, x + cell_w - 2, cell_h], fill=rgb(stroke))
        fill = blend(stroke, theme["surface"], FILL_ALPHA / 255)
        draw.rectangle([x, cell_h + 2, x + cell_w - 2, 2 * cell_h + 2], fill=fill)
        draw.rectangle(
            [x, 2 * cell_h + 4, x + cell_w - 2, 3 * cell_h + 4],
            fill=rgb(theme["surface"]),
            outline=rgb(stroke),
            width=2,
        )
        draw.text((x + 3, 2 * cell_h + 6), f"{label}{index + 1}", font=label_font, fill=rgb(theme["text"]))
    return image


def build() -> Image.Image:
    mock_w, mock_h = 620, 260
    light_mock = chart_mock(LIGHT, LIGHT_STROKES, mock_w, mock_h)
    dark_mock = chart_mock(DARK, DARK_STROKES, mock_w, mock_h)
    light_rows = swatch_rows(LIGHT, LIGHT_STROKES, "L")
    dark_rows = swatch_rows(DARK, DARK_STROKES, "D")

    title_font = font(15)
    header = 34
    height = header + (mock_h + 12) * 2 + light_rows.height + dark_rows.height + 70
    sheet = Image.new("RGB", (mock_w + 20, height), "#FFFFFF")
    draw = ImageDraw.Draw(sheet)
    draw.text((10, 8), "AvCharts - palette candidates (light / dark)", font=title_font, fill="#111111")

    y = header
    sheet.paste(light_mock, (10, y))
    y += mock_h + 12
    sheet.paste(light_rows, (10, y))
    y += light_rows.height + 18
    sheet.paste(dark_mock, (10, y))
    y += mock_h + 12
    sheet.paste(dark_rows, (10, y))
    return sheet


if __name__ == "__main__":
    out = Path(__file__).parent / "palette-preview"
    out.mkdir(exist_ok=True)
    target = out / "palette-preview.png"
    build().save(target)
    print(target)
