# -*- coding: utf-8 -*-
"""StandState 走/跑：IPC → Rootmotion。
非锁定移动吃 Animator.deltaPosition，必须用带位移的 Rootmotion 变体。
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

# StandState 槽位（与 controller m_Childs 顺序一致）
STAND = [
    "Idle1",                       # 0 idle
    "Walk_Forward_Rootmotion",     # 1
    "Walk_Leftward_Rootmotion",    # 2
    "Walk_Rightward_Rootmotion",   # 3
    "Walk_Forward_Left_Rootmotion",# 4
    "Walk_Forward_Right_Rootmotion",# 5
    "Run_Forward_Rootmotion",      # 6
    "Run_Leftward_Rootmotion",     # 7
    "Run_Rightward_Rootmotion",    # 8
    "Run_Backward_Left_Rootmotion",# 9
    "Run_Backward_Right_Rootmotion",# 10
    "Turn_90L",                    # 11
    "Turn_90R",                    # 12
    "Turn_90L",                    # 13
    "Turn_90R",                    # 14
    "Run_Backward_Rootmotion",     # 15
]

NEW_CLIPS = sorted(set(STAND) - {"Idle1", "Turn_90L", "Turn_90R"})


def stable_guid(name: str) -> str:
    return hashlib.md5(f"ARPG_Set/{name}".encode("utf-8")).hexdigest()


def main() -> int:
    ts = time.strftime("%Y%m%d_%H%M%S")
    backup = BACKUP_DIR / f"stand_rootmotion_{ts}"
    backup.mkdir(parents=True, exist_ok=True)
    shutil.copy2(CONTROLLER, backup / "PlayerMove.controller.before")
    print(f"[backup] {backup}")

    guids: dict[str, str] = {}

    # 1) 复制 Rootmotion clips + meta + loop
    for short in NEW_CLIPS:
        src = ARPG_DIR / f"ARPG_Warrior_{short}.anim"
        dst = OUT_DIR / f"ARPG_Warrior_{short}.anim"
        if not src.exists():
            print(f"[FATAL] missing {src}")
            return 1
        shutil.copy2(src, dst)
        guid = stable_guid(short)
        guids[short] = guid

        text = dst.read_bytes().decode("utf-8")
        if "m_EditorCurves: []" not in text:
            print(f"[FATAL] {short}: EditorCurves not empty")
            return 1
        text2, n = re.subn(r"(?m)^(\s+)m_LoopTime: 0\r?$", r"\1m_LoopTime: 1", text, count=1)
        if n != 1:
            print(f"[FATAL] {short}: loop patch failed")
            return 1
        dst.write_bytes(text2.encode("utf-8"))

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
        print(f"[copy] {short} guid={guid}")

    # 已有 clips 的 guid（从现有 meta 读）
    for short in ("Idle1", "Turn_90L", "Turn_90R"):
        meta = (OUT_DIR / f"ARPG_Warrior_{short}.anim.meta").read_text(encoding="utf-8")
        m = re.search(r"guid: ([0-9a-f]{32})", meta)
        if not m:
            print(f"[FATAL] no guid in meta for {short}")
            return 1
        guids[short] = m.group(1)

    # 2) 只改 StandState 的 16 个 m_Motion
    ctrl = CONTROLLER.read_bytes().decode("utf-8")

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

    blocks = split_blocks(ctrl)
    idx, block = find_bt(blocks, "StandState")
    if idx is None:
        print("[FATAL] StandState not found")
        return 1

    pattern = re.compile(
        r"(?m)^    m_Motion: \{fileID: -?\d+(?:, guid: [0-9a-f]{32})?,?\r?\n?      type: \d\}\r?$"
        r"|^    m_Motion: \{fileID: -?\d+(?:, guid: [0-9a-f]{32})?, type: \d\}\r?$"
    )
    found = list(pattern.finditer(block))
    if len(found) != 16:
        print(f"[FATAL] StandState motions={len(found)} expected 16")
        return 1

    out = block
    for match, short in zip(reversed(found), reversed(STAND)):
        rep = f"    m_Motion: {{fileID: 7400000, guid: {guids[short]}, type: 2}}"
        if "\r\n" in match.group(0):
            rep = rep.replace("\n", "\r\n")
        out = out[: match.start()] + rep + out[match.end() :]
    blocks[idx] = ("206", blocks[idx][1], out)

    ctrl2 = "".join(b[2] for b in blocks)

    # 自检
    blocks2 = split_blocks(ctrl2)
    _, block2 = find_bt(blocks2, "StandState")
    got = re.findall(r"(?m)^    m_Motion: \{fileID: 7400000, guid: ([0-9a-f]{32}), type: 2\}", block2)
    if len(got) != 16:
        print(f"[FATAL] post-count {len(got)}")
        return 1
    for short, g in zip(STAND, got):
        if g != guids[short]:
            print(f"[FATAL] slot mismatch want {short}={guids[short]} got {g}")
            return 1
    # 确认 StandState 不再引用旧 IPC guid（Idle/Turn 除外）
    for short in NEW_CLIPS:
        old_ipc_guid = None
        ipc_name = short.replace("_Rootmotion", "_IPC")
        ipc_meta = OUT_DIR / f"ARPG_Warrior_{ipc_name}.anim.meta"
        if ipc_meta.exists():
            old_ipc_guid = re.search(
                r"guid: ([0-9a-f]{32})", ipc_meta.read_text(encoding="utf-8")
            ).group(1)
            # 旧 IPC 只应出现在 LockOn，不应在 StandState
            stand_text = block2
            if old_ipc_guid in stand_text:
                print(f"[FATAL] StandState still refs IPC {ipc_name}")
                return 1

    CONTROLLER.write_bytes(ctrl2.encode("utf-8"))
    (backup / "PlayerMove.controller.after").write_bytes(ctrl2.encode("utf-8"))
    (backup / "stand_map.txt").write_text(
        "StandState slots:\n"
        + "\n".join(f"  [{i}] {s}" for i, s in enumerate(STAND))
        + "\n",
        encoding="utf-8",
        newline="\n",
    )
    print("[DONE] StandState walk/run → Rootmotion")
    return 0


if __name__ == "__main__":
    sys.exit(main())
