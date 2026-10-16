#!/usr/bin/env python3
"""Rebuilds the gallery's Chinese font from the words the gallery writes.

Why this exists: a browser build brings no fonts of its own, so the gallery ships one - a subset of Noto Sans SC
narrowed to the characters it actually uses. Adding a Chinese word to `Translations` without running this script
leaves that word undrawable, which `ShellTests.Every_word_the_gallery_says_in_chinese_can_be_drawn` refuses.

What it does, with `fontTools` installed (`pip install fonttools` or `python3-fonttools`):

    python3 tools/subset-chinese-font.py path/to/NotoSansSC[wght].ttf

    * collects every non-ASCII character the demo's `.cs` and `.axaml` files contain (what a reader reads, and the
      punctuation around it - the demo's comments come along, which costs a few glyphs and no thought);
    * instances the variable font at Regular and subsets it to those characters;
    * renames the result, because the SIL Open Font License reserves the name its source was published under and a
      subset is a modified font;
    * writes `src/AvCharts.Demo/Assets/AvChartsChinese-Regular.ttf`, which is what the gallery loads.

The source font is Noto Sans SC (https://github.com/google/fonts/tree/main/ofl/notosanssc), OFL-1.1; the licence
travels with it as `src/AvCharts.Demo/Assets/NotoSansSC-OFL.txt`.
"""

import pathlib
import sys

REPOSITORY = pathlib.Path(__file__).resolve().parent.parent
DEMO = REPOSITORY / "src"
TARGET = REPOSITORY / "src" / "AvCharts.Demo" / "Assets" / "AvChartsChinese-Regular.ttf"
FAMILY = "AvCharts Chinese"


def characters() -> str:
    written = {chr(code) for code in range(0x20, 0x7F)}

    for file in DEMO.rglob("*"):
        if file.suffix.lower() not in {".cs", ".axaml"} or not file.is_file():
            continue

        try:
            text = file.read_text(encoding="utf-8")
        except (UnicodeDecodeError, OSError):
            continue

        written.update(character for character in text if ord(character) > 0x7F)

    return "".join(sorted(written))


def main(source: str) -> int:
    from fontTools import subset
    from fontTools.ttLib import TTFont
    from fontTools.varLib import instancer

    written = characters()
    print(f"{len(written)} characters the gallery writes")

    # One weight, because a gallery with two weights of one language is a gallery with two files to fetch.
    font = instancer.instantiateVariableFont(TTFont(source), {"wght": 400})

    options = subset.Options()
    options.name_IDs = ["*"]
    options.drop_tables += ["DSIG"]

    cutting = subset.Subsetter(options)
    cutting.populate(text=written)
    cutting.subset(font)

    names = font["name"]

    for name_id, value in {1: FAMILY, 2: "Regular", 4: f"{FAMILY} Regular", 6: "AvChartsChinese-Regular"}.items():
        for record in list(names.names):
            if record.nameID == name_id:
                names.names.remove(record)

        names.setName(value, name_id, 3, 1, 0x409)
        names.setName(value, name_id, 1, 0, 0)

    font.save(TARGET)

    missing = [character for character in written if ord(character) not in font.getBestCmap()]

    print(f"wrote {TARGET.relative_to(REPOSITORY)}: {TARGET.stat().st_size // 1024} KB, "
          f"{len(font.getBestCmap())} glyphs, missing: {''.join(missing) or 'none'}")

    return 1 if missing else 0


if __name__ == "__main__":
    if len(sys.argv) != 2:
        print(__doc__)
        raise SystemExit(2)

    raise SystemExit(main(sys.argv[1]))
