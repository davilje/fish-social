"""Builds FishCozyRounded-Medium.ttf from Resource Han Rounded CN Medium (SIL OFL 1.1).

Unity's legacy Text sizes the first line as hhea ascent + descent and every further line as
ascent + descent + lineGap, and truncates lines that do not fit. The upstream face is 1.448 em
per line, while the authored layouts were sized with Microsoft YaHei (about 1.07 em first line,
1.32 em line pitch), so the vertical metrics are set to match YaHei. OFL requires a modified
version to drop the original font name, so the name table is rewritten as well.

Usage: python Tools/cozy_font.py <ResourceHanRoundedCN-Medium.ttf>
"""
import os
import sys

from fontTools.ttLib import TTFont

ASCENT, DESCENT, LINE_GAP = 900, 170, 250
FAMILY = "Fish Cozy Rounded"
OUTPUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Desktop", "Theme", "Cozy", "fonts",
                      "FishCozyRounded-Medium.ttf")


def main(source):
    font = TTFont(source)
    hhea, os2 = font["hhea"], font["OS/2"]
    hhea.ascent, hhea.descent, hhea.lineGap = ASCENT, -DESCENT, LINE_GAP
    os2.usWinAscent, os2.usWinDescent = ASCENT, DESCENT
    os2.sTypoAscender, os2.sTypoDescender, os2.sTypoLineGap = ASCENT, -DESCENT, LINE_GAP
    for record in font["name"].names:
        text = record.toUnicode()
        renamed = text.replace("Resource Han Rounded", FAMILY).replace("Resource-Han-Rounded", FAMILY.replace(" ", ""))
        if renamed != text:
            record.string = renamed
    font.save(OUTPUT)
    print("wrote", os.path.normpath(OUTPUT))


if __name__ == "__main__":
    main(sys.argv[1])
