# -*- coding: utf-8 -*-
"""P1 战斗动画：Attack1/2/3 + Impact + Death。
状态名不变，只换 m_Motion；Combo2 跳过（EditorCurves 污染），用 Heavy2 顶替。
"""
from __future__ import annotations

import hashlib
import re
import shutil
import sys
import time
from pathlib import Path

ROOT = Path(r"C:\Unity\01_Assassin's Creed_Demo")
ARPG_DIR = ROOT / "Assets/ARPGWarrior/Animations/Humanoid"
OUT_DIR = ROOT / "Assets/GameMain/Scripts/Entity/Player/Animations/ARPG_Set"
CONTROLLER = ROOT / "Assets/GameMain/Scripts/Entity/Player/Animations/PlayerMove.controller"
BACKUP_DIR = ROOT / "_AnimControllerBackup"

# 状态名 → ARPG 短名
STATES = {
    "Melee_Attack_1": "Attack_Combo1",
    "Melee_Attack_2": "Attack_Heavy2",
    "Melee_Attack_3": "Attack_Combo3",
    "Melee_Impact": "Hit1",
    "Melee_FallBackDeath": "Death",
}

# 攻击出场 ExitTime（clip 更短，收到 0.85；Impact 保持 ~0.65）
EXIT_TIMES = {
    "3222333634159062077": 0.85,   # Attack_1 → Empty
    "-1106385746990259488": 0.85,  # Attack_2 → Empty
    "2494462185291423075": 0.85,   # Attack_3 → Empty
    "-1333818737957605408": 0.65,  # Impact → Empty
}


def stable_guid(name: str) -> str:
    return hashlib.md5(f"ARPG_Set/{name}".encode("utf-8")).hexdigest()


def ensure_clip(short: str) -> str:
    """Copy if needed; always verify EditorCurves empty. No loop. Return guid."""
    src = ARPG_DIR / f"ARPG_Warrior_{short}.anim"
    dst = OUT_DIR / f"ARPG_Warrior_{short}.anim"
    if not src.exists():
        raise RuntimeError(f"missing source {src}")

    if not dst.exists():
        shutil.copy2(src, dst)
        text = dst.read_bytes().decode("utf-8")
        if "m_EditorCurves: []" not in text:
            raise RuntimeError(f"{short}: EditorCurves not empty")
        # 攻击/受击/死亡不循环
        dst.write_bytes(text.encode("utf-8"))
        guid = stable_guid(short)
        dst.with_suffix(".anim.meta").write_text(
            "fileFormatVersion: 2\n"
            f"guid: {guid}\n"
            "NativeFormatImporter:\n"
            "  externalObjects: {}\n"
            "  mainObjectFileID: 7400000\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n",
            encoding="utf-8",
            newline="\n",
        )
        print(f"[copy] {short}")
    else:
        text = dst.read_bytes().decode("utf-8")
        if "m_EditorCurves: []" not in text:
            raise RuntimeError(f"{short}: existing copy EditorCurves not empty")
        meta = dst.with_suffix(".anim.meta")
        m = re.search(r"guid: ([0-9a-f]{32})", meta.read_text(encoding="utf-8"))
        if not m:
            raise RuntimeError(f"{short}: meta missing guid")
        guid = m.group(1)
        print(f"[reuse] {short} guid={guid}")
    return guid


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


def find_state(blocks, name: str):
    for idx, (cid, fid, text) in enumerate(blocks):
        if cid != "1102":
            continue
        if re.search(rf"(?m)^  m_Name: {re.escape(name)}\r?$", text):
            return idx, text
    return None, None


def replace_state_motion(block: str, guid: str) -> str:
    pattern = re.compile(
        r"(?m)^  m_Motion: \{fileID: -?\d+(?:, guid: [0-9a-f]{32})?,?\r?\n?    type: \d\}\r?$"
        r"|^  m_Motion: \{fileID: -?\d+(?:, guid: [0-9a-f]{32})?, type: \d\}\r?$"
    )
    m = pattern.search(block)
    if not m:
        raise RuntimeError("m_Motion not found")
    rep = f"  m_Motion: {{fileID: 7400000, guid: {guid}, type: 2}}"
    if "\r\n" in m.group(0):
        rep = rep.replace("\n", "\r\n")
    return block[: m.start()] + rep + block[m.end() :]


def main() -> int:
    ts = time.strftime("%Y%m%d_%H%M%S")
    backup = BACKUP_DIR / f"combat_p1_{ts}"
    backup.mkdir(parents=True, exist_ok=True)
    shutil.copy2(CONTROLLER, backup / "PlayerMove.controller.before")
    print(f"[backup] {backup}")

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    guids: dict[str, str] = {}
    for short in STATES.values():
        guids[short] = ensure_clip(short)

    ctrl = CONTROLLER.read_bytes().decode("utf-8")
    blocks = split_blocks(ctrl)

    try:
        for st, short in STATES.items():
            idx, block = find_state(blocks, st)
            if idx is None:
                raise RuntimeError(f"state {st} not found")
            new_block = replace_state_motion(block, guids[short])
            blocks[idx] = ("1102", blocks[idx][1], new_block)
            print(f"[state] {st} -> {short}")

        # ExitTime
        for i, (cid, fid, text) in enumerate(blocks):
            if cid == "1101" and fid in EXIT_TIMES:
                new_et = EXIT_TIMES[fid]
                new_text, n = re.subn(
                    r"(?m)^  m_ExitTime: [\d.]+\r?$",
                    f"  m_ExitTime: {new_et}",
                    text,
                    count=1,
                )
                if n != 1:
                    raise RuntimeError(f"ExitTime not patched for {fid}")
                blocks[i] = (cid, fid, new_text)
                print(f"[exit] transition {fid} ExitTime={new_et}")
    except Exception as e:
        print(f"[FATAL] {e}")
        shutil.copy2(backup / "PlayerMove.controller.before", CONTROLLER)
        return 1

    ctrl2 = "".join(b[2] for b in blocks)

    # self-check
    blocks2 = split_blocks(ctrl2)
    for st, short in STATES.items():
        _, block = find_state(blocks2, st)
        if block is None or guids[short] not in block:
            print(f"[FATAL] post-check {st}")
            shutil.copy2(backup / "PlayerMove.controller.before", CONTROLLER)
            return 1
        print(f"[check] {st} ok")

    CONTROLLER.write_bytes(ctrl2.encode("utf-8"))
    (backup / "PlayerMove.controller.after").write_bytes(ctrl2.encode("utf-8"))
    (backup / "map.txt").write_text(
        "P1 combat map:\n"
        + "\n".join(f"  {k} -> {v}  guid={guids[v]}" for k, v in STATES.items())
        + "\nExitTimes:\n"
        + "\n".join(f"  {k} -> {v}" for k, v in EXIT_TIMES.items())
        + "\n",
        encoding="utf-8",
        newline="\n",
    )
    print(f"[DONE] {CONTROLLER}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
