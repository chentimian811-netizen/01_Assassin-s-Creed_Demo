# -*- coding: utf-8 -*-
"""ARPGWarrior → PlayerMove.controller 自动化替换（v2）。
按失败总结 6.3：File.Copy + 自写 .meta；映射来自实际 controller 结构；改前备份、改后自检。
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

CLIPS = {
    "Idle1": True,
    "Idle2": True,
    "Walk_Forward_IPC": True,
    "Walk_Leftward_IPC": True,
    "Walk_Rightward_IPC": True,
    "Walk_Backward_IPC": True,
    "Walk_Forward_Left_IPC": True,
    "Walk_Forward_Right_IPC": True,
    "Run_Forward_IPC": True,
    "Run_Leftward_IPC": True,
    "Run_Rightward_IPC": True,
    "Run_Backward_IPC": True,
    "Run_Backward_Left_IPC": True,
    "Run_Backward_Right_IPC": True,
    "Turn_90L": False,
    "Turn_90R": False,
    "Evade_Forward": False,
    "Evade_Backward": False,
    "Evade_Left": False,
    "Evade_Right": False,
    "Airborne": True,
    "Landing": False,
    "Hit1": False,
    "Death": False,
}


def stable_guid(name: str) -> str:
    return hashlib.md5(f"ARPG_Set/{name}".encode("utf-8")).hexdigest()


def motion_line(guid: str) -> str:
    return f"    m_Motion: {{fileID: 7400000, guid: {guid}, type: 2}}"


def split_blocks(text: str) -> list[tuple[str, str, str]]:
    """Return list of (class_id, file_id, block_text) for each --- !u! document."""
    parts = re.split(r"(?m)^(--- !u!\d+ &-?\d+\r?\n)", text)
    # parts[0] is header (%YAML), then pairs of (marker, body)
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


def find_block(blocks, class_id: str, name: str):
    for idx, (cid, fid, text) in enumerate(blocks):
        if cid != class_id:
            continue
        if re.search(rf"(?m)^  m_Name: {re.escape(name)}\r?$", text):
            return idx, text
    return None, None


def replace_motions_in_block(block: str, shorts: list[str], guids: dict[str, str]) -> str:
    """Replace each m_Motion (single- or two-line) with ARPG_Set reference."""
    # Match one- or two-line m_Motion entries under childs / state
    pattern = re.compile(
        r"(?m)^    m_Motion: \{fileID: -?\d+(?:, guid: [0-9a-f]{32})?,?\r?\n?      type: 3\}\r?$"
        r"|^    m_Motion: \{fileID: -?\d+(?:, guid: [0-9a-f]{32})?, type: [23]\}\r?$"
    )
    found = list(pattern.finditer(block))
    if len(found) != len(shorts):
        # try a looser match for debugging
        loose = list(re.finditer(r"(?m)^    m_Motion: ", block))
        raise RuntimeError(
            f"motion pattern: strict={len(found)} loose={len(loose)} expected={len(shorts)}"
        )
    out = block
    for match, short in zip(reversed(found), reversed(shorts)):
        replacement = motion_line(guids[short])
        # preserve original line ending style if CRLF
        if "\r\n" in block[match.start() : match.end()]:
            replacement = replacement.replace("\n", "\r\n")
        out = out[: match.start()] + replacement + out[match.end() :]
    return out


def replace_state_motion(block: str, short: str, guids: dict[str, str]) -> str:
    pattern = re.compile(
        r"(?m)^  m_Motion: \{fileID: -?\d+(?:, guid: [0-9a-f]{32})?,?\r?\n?    type: 3\}\r?$"
        r"|^  m_Motion: \{fileID: -?\d+(?:, guid: [0-9a-f]{32})?, type: [23]\}\r?$"
    )
    m = pattern.search(block)
    if not m:
        raise RuntimeError("state m_Motion not found")
    replacement = f"  m_Motion: {{fileID: 7400000, guid: {guids[short]}, type: 2}}"
    if "\r\n" in m.group(0):
        replacement = replacement.replace("\n", "\r\n")
    return block[: m.start()] + replacement + block[m.end() :]


def main() -> int:
    ts = time.strftime("%Y%m%d_%H%M%S")
    backup = BACKUP_DIR / f"auto_replace_{ts}"
    backup.mkdir(parents=True, exist_ok=True)
    shutil.copy2(CONTROLLER, backup / "PlayerMove.controller.before")
    print(f"[backup] {backup}")

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    guids: dict[str, str] = {}
    report: list[str] = []

    for short, loop in CLIPS.items():
        src = ARPG_DIR / f"ARPG_Warrior_{short}.anim"
        if not src.exists():
            print(f"[FATAL] missing source {src}")
            return 1
        dst = OUT_DIR / f"ARPG_Warrior_{short}.anim"
        shutil.copy2(src, dst)
        guid = stable_guid(short)
        guids[short] = guid

        raw = dst.read_bytes()
        # keep original bytes; only flip loop flag as ascii replace
        text = raw.decode("utf-8")
        orig_size = len(raw)

        if "m_EditorCurves: []" not in text:
            print(f"[FAIL] {short}: m_EditorCurves not empty after copy")
            return 1

        if loop:
            text2, n = re.subn(
                r"(?m)^(\s+)m_LoopTime: 0\r?$",
                r"\1m_LoopTime: 1",
                text,
                count=1,
            )
            if n != 1:
                print(f"[FAIL] {short}: m_LoopTime not patched")
                return 1
            text = text2

        new_raw = text.encode("utf-8")
        if len(new_raw) > orig_size * 1.5:
            print(f"[FAIL] {short}: size ballooned {orig_size}->{len(new_raw)}")
            return 1
        dst.write_bytes(new_raw)

        meta = dst.with_suffix(".anim.meta")
        meta.write_text(
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
        report.append(f"OK copy {short} loop={int(loop)} guid={guid}")
        print(f"[OK] {short} loop={int(loop)} guid={guid}")

    # Read controller preserving CRLF
    ctrl_raw = CONTROLLER.read_bytes().decode("utf-8")
    use_crlf = "\r\n" in ctrl_raw

    stand = [
        "Idle1",
        "Walk_Forward_IPC",
        "Walk_Leftward_IPC",
        "Walk_Rightward_IPC",
        "Walk_Forward_Left_IPC",
        "Walk_Forward_Right_IPC",
        "Run_Forward_IPC",
        "Run_Leftward_IPC",
        "Run_Rightward_IPC",
        "Run_Backward_Left_IPC",
        "Run_Backward_Right_IPC",
        "Turn_90L",
        "Turn_90R",
        "Turn_90L",
        "Turn_90R",
        "Run_Backward_IPC",
    ]
    lockon = [
        "Idle1",
        "Walk_Forward_IPC",
        "Walk_Leftward_IPC",
        "Walk_Rightward_IPC",
        "Walk_Backward_IPC",
    ]
    air = ["Airborne"] * 12 + ["Landing"]
    states = {
        "RollForward": "Evade_Forward",
        "RollBackward": "Evade_Backward",
        "RollLeft": "Evade_Left",
        "RollRight": "Evade_Right",
        "Melee_Impact": "Hit1",
        "Melee_FallBackDeath": "Death",
    }

    blocks = split_blocks(ctrl_raw)
    try:
        for bt_name, shorts in (("StandState", stand), ("LockOn", lockon), ("AirState", air)):
            idx, block = find_block(blocks, "206", bt_name)
            if idx is None:
                raise RuntimeError(f"BlendTree {bt_name} not found")
            new_block = replace_motions_in_block(block, shorts, guids)
            blocks[idx] = ("206", blocks[idx][1], new_block)
            print(f"[OK] BlendTree {bt_name} {len(shorts)} slots")

        for st, short in states.items():
            idx, block = find_block(blocks, "1102", st)
            if idx is None:
                raise RuntimeError(f"State {st} not found")
            new_block = replace_state_motion(block, short, guids)
            blocks[idx] = ("1102", blocks[idx][1], new_block)
            print(f"[OK] state {st} -> {short}")
    except Exception as e:
        print(f"[FATAL] patch failed: {e}")
        shutil.copy2(backup / "PlayerMove.controller.before", CONTROLLER)
        return 1

    ctrl = "".join(b[2] for b in blocks)

    # Self-checks
    used = set(stand) | set(lockon) | set(air) | set(states.values())
    dead = "58e8c7e467d271a4fbb5005b10e78708"
    if dead in ctrl:
        print("[FATAL] dead AirState guid still present")
        shutil.copy2(backup / "PlayerMove.controller.before", CONTROLLER)
        return 1
    for short in used:
        if guids[short] not in ctrl:
            print(f"[FATAL] used guid missing: {short}")
            shutil.copy2(backup / "PlayerMove.controller.before", CONTROLLER)
            return 1

    blocks2 = split_blocks(ctrl)
    for name, n in (("StandState", 16), ("LockOn", 5), ("AirState", 13)):
        _, block = find_block(blocks2, "206", name)
        if block is None:
            print(f"[FATAL] post-check missing BT {name}")
            shutil.copy2(backup / "PlayerMove.controller.before", CONTROLLER)
            return 1
        # count only childs section motions (indent 4)
        got = len(re.findall(r"(?m)^    m_Motion: ", block))
        if got != n:
            print(f"[FATAL] {name} motion count {got} != {n}")
            shutil.copy2(backup / "PlayerMove.controller.before", CONTROLLER)
            return 1
        print(f"[check] {name} motions={got}")

    for st in states:
        _, block = find_block(blocks2, "1102", st)
        if block is None or "ARPG" not in block and "type: 2" not in block:
            # type: 2 is the marker of our standalone anim ref
            if block is None or "type: 2" not in block:
                print(f"[FATAL] state {st} not pointing at ARPG_Set")
                shutil.copy2(backup / "PlayerMove.controller.before", CONTROLLER)
                return 1

    CONTROLLER.write_bytes(ctrl.encode("utf-8"))
    (backup / "PlayerMove.controller.after").write_bytes(ctrl.encode("utf-8"))
    (backup / "ARPG_Set_map.txt").write_text(
        "\n".join(report)
        + "\n\nStandState:\n"
        + "\n".join(f"  [{i}] {s}" for i, s in enumerate(stand))
        + "\nLockOn:\n"
        + "\n".join(f"  [{i}] {s}" for i, s in enumerate(lockon))
        + "\nAirState:\n"
        + "\n".join(f"  [{i}] {s}" for i, s in enumerate(air))
        + "\nStates:\n"
        + "\n".join(f"  {k} -> {v}" for k, v in states.items())
        + f"\nUnused copies: {sorted(set(guids) - used)}\n",
        encoding="utf-8",
        newline="\n",
    )
    print(f"[DONE] {CONTROLLER}")
    print(f"[map]  {backup / 'ARPG_Set_map.txt'}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
