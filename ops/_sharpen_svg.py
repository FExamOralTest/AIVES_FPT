#!/usr/bin/env python3
"""
Make a PlantUML sequence SVG crisp and BOLD for slide use.

Three problems are fixed here:

1. PlantUML's own skinparams do not cover every element of a sequence diagram:
   notes keep the default yellow, alt/loop frames keep the default grey, and
   the lifelines are drawn as 0.5px dashed hairlines. On a projector those
   read as "faded".

2. The SVG's intrinsic size is under 1000px wide. When the image is scaled
   down onto a slide, every stroke shrinks with it - a "3px" lifeline becomes
   under 3 real pixels and still looks thin. The weights are therefore
   multiplied so the lines survive the downscale.

3. A flat recolour-to-blue destroys the semantic colour code of the diagram
   (request / response / database / internal). This script therefore keeps
   every colour PlantUML already assigned and only adjusts *weights* plus the
   few elements PlantUML still leaves in its default palette.

  * lifelines   -> solid, medium, muted grey-blue (a guide, NOT a message)
  * messages    -> keep their own colour, just make them thicker
  * note boxes  -> light blue instead of the default yellow
  * alt frames  -> light blue instead of the default grey
  * legend      -> white card with a soft grey-blue border

Usage:
    python ops/_sharpen_svg.py docs/architecture/simplified/DIAGRAM_05_SEQUENCE_EN.svg ...
"""
import pathlib
import re
import sys

# Multiplier applied to every stroke width (the SVG is scaled down on a slide,
# so the drawn weight has to be proportionally larger to stay visible).
WEIGHT = 1.8
MIN_WIDTH = 2.8

# The lifelines are guides, not messages. They stay clearly readable but are
# thinner than the arrows so the eye reads the arrows first.
LIFELINE_WIDTH = 3.2

# Visual Paradigm look: the life line stays a DASHED guide, but it must still
# be visible on a projector -> 1.4px dashed (2.5x PNG => 3.5px).
DASHED_LIFELINE_WIDTH = 1.4

BLUE = "#1F5FBF"
PURPLE = "#7C3AED"
MUTED = "#8FA6C4"
NOTE_FILL = "#EAF2FE"
GROUP_FILL = "#F4F7FB"
WHITE = "#FFFFFF"

TAG_RE = re.compile(r"<(rect|path|line|polyline|polygon|ellipse)\b[^>]*>")
STYLE_RE = re.compile(r'style="([^"]*)"')
FILL_RE = re.compile(r'fill="([^"]*)"')
WIDTH_RE = re.compile(r"stroke-width:([0-9.]+)")


def widen(mm):
    """Widen one `stroke-width:X` token."""
    new = round(float(mm.group(1)) * WEIGHT, 2)
    if new < MIN_WIDTH:
        new = MIN_WIDTH
    return f"stroke-width:{new}"


def fix_style(style: str, fill: str, keep_dashed: bool = False) -> str:
    is_lifeline = "stroke-dasharray:5.0,5.0" in style

    if is_lifeline:
        if keep_dashed:
            # VP look: the life line stays a thin DASHED guide, but it still has
            # to survive the downscale -> widen it, keep the dash pattern.
            style = WIDTH_RE.sub(f"stroke-width:{DASHED_LIFELINE_WIDTH}", style)
            return style
        # Guide line: solid, medium, keep the muted colour PlantUML gave it.
        style = style.replace(";stroke-dasharray:5.0,5.0", "")
        style = WIDTH_RE.sub(f"stroke-width:{LIFELINE_WIDTH}", style)
        return style

    # Default PlantUML borders -> theme colours.
    style = style.replace("stroke:#000000", f"stroke:{MUTED}")
    if "stroke:#181818" in style:
        # Note border vs. database-icon inner arc.
        style = style.replace(
            "stroke:#181818",
            f"stroke:{BLUE if fill in (NOTE_FILL, '#FEFFDD') else PURPLE}",
        )

    # Re-weight every stroke for projection.
    return WIDTH_RE.sub(widen, style)


def fix_fill(fill: str) -> str:
    return {
        "#FEFFDD": NOTE_FILL,   # default yellow note
        "#EEEEEE": GROUP_FILL,   # default grey alt / loop frame
        "#E2E2F0": WHITE,        # default grey actor / database
        "#DDDDDD": WHITE,        # legend card
    }.get(fill, fill)


def restyle(tag_match, keep_dashed: bool = False):
    tag = tag_match.group(0)
    style_m = STYLE_RE.search(tag)
    fill_m = FILL_RE.search(tag)
    fill = fill_m.group(1) if fill_m else ""
    if style_m:
        new_style = fix_style(style_m.group(1), fill, keep_dashed)
        tag = tag[:style_m.start(1)] + new_style + tag[style_m.end(1):]
    if fill_m:
        new_fill = fix_fill(fill)
        if new_fill != fill:
            tag = tag[:fill_m.start(1)] + new_fill + tag[fill_m.end(1):]
    return tag


def sharpen(path: pathlib.Path, keep_dashed: bool = False) -> bool:
    text = path.read_text(encoding="utf-8", errors="ignore")
    before = text
    text = TAG_RE.sub(lambda m: restyle(m, keep_dashed), text)
    if text == before:
        return False
    path.write_text(text, encoding="utf-8")
    return True


def main():
    args = sys.argv[1:]
    keep_dashed = "--dashed-lifelines" in args
    if keep_dashed:
        args.remove("--dashed-lifelines")
    targets = [pathlib.Path(a) for a in args]
    if not targets:
        targets = sorted(pathlib.Path("docs/architecture/simplified").glob("DIAGRAM_05*.svg"))
    for t in targets:
        if not t.exists():
            print("missing:", t)
            continue
        print(("sharpened " if sharpen(t, keep_dashed) else "unchanged ") + str(t))


if __name__ == "__main__":
    main()
