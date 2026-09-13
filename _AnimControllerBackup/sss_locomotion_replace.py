# -*- coding: utf-8 -*-
"""待机/移动/转向 → Sword_and_Shield FBX（type:3，直接引用，不复制）。
"""
from __future__ import annotations

import re
import shutil
import sys
import time
from pathlib import Path

ROOT = Path(r"C:\Unity\01_Assassin's Creed_Demo")
CONTROLLER = ROOT / "Assets/GameMain/Scripts/Entity/Player/Animations/PlayerMove.controller"
BACKUP_DIR = ROOT / "_AnimControllerBackup"
META_DIR = ROOT / "Assets/Sword_and_Shield_Anims/Art/Animations"

FILE_ID = "1827226128182048838"

CLIP_GUIDS = {
    "Idle_CombatReady": "720e16066d5bc3d448961a3f55bc5fce",
    "Walk_Forward": "3e8e17ffa1132d64a95d96d49ab22e0c",
    "Walk_Left": "e64f199d49ac2f647ae3471066c93dc2",
    "Walk_Right": "1b72086b7ef8d2347940dd0a7a31b5d6",
    "Walk_Back": "79467acbddc19b94a933a6ee4e53382a",
    "Walk_ForwardLeft": "d332ca3bbc8b15145b3738193f66b7d9",
    "Walk_ForwardRight": "9d8f230929f7d604d9b9730d9502bf0c",
    "Run_Forward": "5791d4bd66e41f247b7be5afd651bc5a",
    "Run_Left": "49873e0ffe2ba864c8d7ccef5ceff064",
    "Run_Right": "588ddd5ca73a2ff42b2f4ad26a7537f2",
    "Run_Back": "6f83d267af97a8b45b7b5681f3056aeb",
    "Run_BackLeft": "6b0168ca509ec034cadb8ceff7f0335a",
    "Run_BackRight": "c003b04601c162f4db3ab9c565559669",
    "Turn_Left90": "206d8a56ebf9d2a47bfca369489d0043",
    "Turn_Right90": "ac7670b9ab98dcc4480990a256cbf170",
}

STAND = [
    "Idle_CombatReady",
    "Walk_Forward",
    "Walk_Left",
    "Walk_Right",
    "Walk_ForwardLeft",
    "Walk_ForwardRight",
    "Run_Forward",
    "Run_Left",
    "Run_Right",
    "Run_BackLeft",
    "Run_BackRight",
    "Turn_Left90",
    "Turn_Right90",
    "Turn_Left90",
    "Turn_Right90",
    "Run_Back",
]

LOCKON = [
    "Idle_CombatReady",
    "Walk_Forward",
    "Walk_Left",
    "Walk_Right",
    "Walk_Back",
]


def motion_line(short: str, indent: str = "    ") -> str:
    return f"{indent}m_Motion: {{fileID: {FILE_ID}, guid: {CLIP_GUIDS[short]}, type: 3}}"


def split_blocks(text: str):
    parts = re.split(r"(?m)^(--- !u!\d+ &-?\d+\r?\n)", text)
    header = parts[0]
    blocks = [(None, None, header)]
    i = 1
    while i < len(parts):
        marker = parts[i]
        body = parts[i + 1] if i + 1 < len(parts) else ""
        m = re.match(r"--- !u!(\d+) &(-?\d+)", marker)
        blocks.append((m.group(1), m.group(2), marker + body))
        i += 2
    return blocks


def find_bt(blocks, name: str):
    for idx, (cid, fid, text) in enumerate(blocks):
        if cid != "206":
            continue
        if re.search(rf"(?m)^  m_Name: {re.escape(name)}\r?$", text):
            return idx, text
    return None, None


MOTION_RE = re.compile(
    r"(?m)^    m_Motion: \{fileID: -?\d+(?:, guid: [0-9a-f]{32})?,?\r?\n?      type: \d\}\r?$"
    r"|^    m_Motion: \{fileID: -?\d+(?:, guid: [0-9a-f]{32})?, type: \d\}\r?$"
)


def replace_slots(block: str, shorts: list[str]) -> str:
    found = list(MOTION_RE.finditer(block))
    if len(found) != len(shorts):
        raise RuntimeError(f"slot count {len(found)} != {len(shorts)}")
    out = block
    for match, short in zip(reversed(found), reversed(shorts)):
        rep = motion_line(short)
        if "\r\n" in match.group(0):
            rep = rep.replace("\n", "\r\n")
        out = out[: match.start()] + rep + out[match.end() :]
    return out


def main() -> int:
    # verify metas exist and GUIDs match
    for short, guid in CLIP_GUIDS.items():
        meta = META_DIR / f"SwordAndShield_{short}.fbx.meta"
        if not meta.exists():
            print(f"[FATAL] missing meta {meta}")
            return 1
        raw = meta.read_text(encoding="utf-8")
        m = re.search(r"guid: ([0-9a-f]{32})", raw)
        if not m or m.group(1) != guid:
            print(f"[FATAL] guid mismatch {short}: file={m.group(1) if m else '?'} map={guid}")
            return 1
        if "internalIDToNameTable" not in raw:
            print(f"[WARN] {short}: no internalIDToNameTable")

    ts = time.strftime("%Y%m%d_%H%M%S")
    backup = BACKUP_DIR / f"sss_locomotion_{ts}"
    backup.mkdir(parents=True, exist_ok=True)
    shutil.copy2(CONTROLLER, backup / "PlayerMove.controller.before")
    print(f"[backup] {backup}")

    ctrl = CONTROLLER.read_bytes().decode("utf-8")
    blocks = split_blocks(ctrl)

    try:
        for name, shorts in (("StandState", STAND), ("LockOn", LOCKON)):
            idx, block = find_bt(blocks, name)
            if idx is None:
                raise RuntimeError(f"BlendTree {name} not found")
            blocks[idx] = ("206", blocks[idx][1], replace_slots(block, shorts))
            print(f"[OK] {name} {len(shorts)} slots")
    except Exception as e:
        print(f"[FATAL] {e}")
        shutil.copy2(backup / "PlayerMove.controller.before", CONTROLLER)
        return 1

    ctrl2 = "".join(b[2] for b in blocks)

    # self-check
    blocks2 = split_blocks(ctrl2)
    for name, shorts in (("StandState", STAND), ("LockOn", LOCKON)):
        _, block = find_bt(blocks2, name)
        if block is None:
            print(f"[FATAL] post missing {name}")
            return 1
        for short in shorts:
            if CLIP_GUIDS[short] not in block:
                print(f"[FATAL] {name} missing guid for {short}")
                shutil.copy2(backup / "PlayerMove.controller.before", CONTROLLER)
                return 1
        got = len(MOTION_RE.findall(block))
        if got != len(shorts):
            print(f"[FATAL] {name} motions {got} != {len(shorts)}")
            shutil.copy2(backup / "PlayerMove.controller.before", CONTROLLER)
            return 1
        # all type 3
        if "type: 2" in block:
            print(f"[WARN] {name} still has type:2 refs")
        print(f"[check] {name} ok ({got})")

    CONTROLLER.write_bytes(ctrl2.encode("utf-8"))
    (backup / "PlayerMove.controller.after").write_bytes(ctrl2.encode("utf-8"))
    (backup / "map.txt").write_text(
        "StandState:\n"
        + "\n".join(f"  [{i}] {s}" for i, s in enumerate(STAND))
        + "\nLockOn:\n"
        + "\n".join(f"  [{i}] {s}" for i, s in enumerate(LOCKON))
        + "\n",
        encoding="utf-8",
        newline="\n",
    )
    print(f"[DONE] {CONTROLLER}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
