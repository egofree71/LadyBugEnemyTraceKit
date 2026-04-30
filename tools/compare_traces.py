#!/usr/bin/env python3
"""Small external JSONL trace comparator.

Usage:
    python compare_traces.py mame_trace.jsonl godot_candidate_trace.jsonl
"""

from __future__ import annotations

import json
import sys
from pathlib import Path
from typing import Any


def load_jsonl(path: Path) -> list[dict[str, Any]]:
    frames: list[dict[str, Any]] = []
    with path.open("r", encoding="utf-8") as f:
        for line_no, line in enumerate(f, 1):
            line = line.strip()
            if not line:
                continue
            try:
                frames.append(json.loads(line))
            except json.JSONDecodeError as exc:
                print(f"warning: {path}:{line_no}: {exc}")
    return frames


def get(frame: dict[str, Any], dotted: str) -> Any:
    value: Any = frame
    for part in dotted.split("."):
        if "[" in part and part.endswith("]"):
            name, index_text = part[:-1].split("[")
            value = value.get(name, [])
            value = value[int(index_text)] if int(index_text) < len(value) else None
        else:
            value = value.get(part) if isinstance(value, dict) else None
    return value


FIELDS = [
    "pc",
    "r",
    "player.raw",
    "player.x",
    "player.y",
    "player.currentDir",
    "player.turnTargetX",
    "player.turnTargetY",
    "enemies[0].raw",
    "enemies[0].dir",
    "enemies[0].x",
    "enemies[0].y",
    "enemies[0].collisionActive",
    "enemies[1].raw",
    "enemies[1].x",
    "enemies[1].y",
    "enemyWork.tempDir",
    "enemyWork.tempX",
    "enemyWork.tempY",
    "enemyWork.rejectedMask",
    "enemyWork.fallbackMask",
    "enemyWork.preferred[0]",
    "enemyWork.preferred[1]",
    "enemyWork.preferred[2]",
    "enemyWork.preferred[3]",
    "enemyWork.chaseTimers[0]",
    "enemyWork.chaseTimers[1]",
    "enemyWork.chaseTimers[2]",
    "enemyWork.chaseTimers[3]",
    "enemyWork.chaseRoundRobin",
    "timers.61B6",
    "timers.61B8",
    "timers.61B9",
    "timers.freeze61E1",
]


def main() -> int:
    if len(sys.argv) != 3:
        print(__doc__.strip())
        return 2

    mame = load_jsonl(Path(sys.argv[1]))
    candidate = load_jsonl(Path(sys.argv[2]))
    count = min(len(mame), len(candidate))

    print(f"MAME frames:      {len(mame)}")
    print(f"Candidate frames: {len(candidate)}")
    print(f"Compared frames:  {count}")

    diffs = 0
    max_print = 300
    for i in range(count):
        sample = mame[i].get("sample", i)
        for field in FIELDS:
            a = get(mame[i], field)
            b = get(candidate[i], field)
            if a != b:
                diffs += 1
                if diffs <= max_print:
                    print(f"sample {sample:04} | {field}: MAME={a} Candidate={b}")

    if len(mame) != len(candidate):
        diffs += 1
        print(f"trace.length: MAME={len(mame)} Candidate={len(candidate)}")

    print(f"Total differences: {diffs}")
    return 1 if diffs else 0


if __name__ == "__main__":
    raise SystemExit(main())
