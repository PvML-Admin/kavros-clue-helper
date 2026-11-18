"""Import the home/lodestone group from the attributed teleport_data.ts.

Usage: python import_lodestones.py teleport_data.ts ../assets/compass/positions.tsv
The input is the Clue Trainer revision documented in THIRD_PARTY.txt.
"""
from pathlib import Path
import re
import sys


def import_lodestones(source):
    text = source.read_text(encoding="utf-8-sig")
    start = text.index("spots: [") + len("spots: [")
    group = text[start:text.index("\n    ],", start)]
    blocks = []
    depth = 0
    for i, char in enumerate(group):
        if char == "{":
            if depth == 0:
                first = i
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                blocks.append(group[first:i + 1])
    rows = []
    for block in blocks:
        name = re.search(r'name:\s*"([^"]+)"', block)
        position = re.search(
            r'target:\s*\{\s*"?origin"?:\s*\{\s*"?x"?:\s*(\d+),'
            r'\s*"?y"?:\s*(\d+),\s*"?level"?:\s*(\d+)', block)
        if name is None or position is None:
            raise ValueError("Unrecognised lodestone object; refusing a partial catalog")
        label = name[1].replace("\u00b4", "'").replace("`", "'")
        label = {"Draynor": "Draynor Village", "Wilderness": "Wilderness Crater"}.get(label, label)
        rows.append((label + " lodestone", int(position[1]), int(position[2]), 1))
    if len(rows) != 28 or len({row[0] for row in rows}) != 28:
        raise ValueError("Expected all 28 destinations from the pinned baseline")
    if not all(row[0].isascii() for row in rows):
        raise ValueError("Unexpected name encoding")
    return sorted(rows)


if __name__ == "__main__":
    rows = import_lodestones(Path(sys.argv[1]))
    Path(sys.argv[2]).write_text(
        "\n".join("\t".join(map(str, row)) for row in rows) + "\n", encoding="utf-8")
