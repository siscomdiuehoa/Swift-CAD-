#!/usr/bin/env python3
"""Chup anh chay that tren Android emulator bang ShotMode (APK build voi -p:SwiftCadShots=true).

Dung: shots.py <thu_muc_apk> <ten> <WxH> <density> <locale> <font_scale>
"""
import os, sys, time
sys.path.insert(0, os.path.dirname(__file__))
from android_common import ACT, PKG, adb, install, rotate, screencap, setup_device, focused_package

APK_DIR, NAME, SIZE, DENSITY, LOCALE, FONT = sys.argv[1:7]
OUT = "artifacts/shots"
os.makedirs(OUT, exist_ok=True)

actual = setup_device(SIZE, DENSITY, FONT, LOCALE)
install(APK_DIR)
log = [f"locale yeu cau={LOCALE} thuc te={actual}",
       f"wm size={adb('shell','wm','size').strip()} density={adb('shell','wm','density').strip()} font_scale={adb('shell','settings','get','system','font_scale').strip()}"]

SHOTS = [
    ("01-welcome", "welcome", 0),
    ("02-drawings", "drawings", 0),
    ("03-drawings-empty", "drawings-empty", 0),
    ("04-saved", "saved", 0),
    ("05-settings", "settings", 0),
    ("06-viewer", "viewer", 0),
    ("07-viewer-find", "viewer-find", 0),
    ("08-viewer-measure", "viewer-measure", 0),
    ("09-viewer-layers", "viewer-layers", 0),
    ("10-viewer-notes", "viewer-notes", 0),
    ("11-viewer-fullscreen", "viewer-fullscreen", 0),
    ("12-viewer-real-file", "viewer-real", 0),
    ("16-cad-viewer", "viewer-cad", 0),
    ("17-cad-find", "viewer-cad-find", 0),
    ("18-cad-measure", "viewer-cad-measure", 0),
    ("19-cad-layers", "viewer-cad-layers", 0),
    ("20-cad-landscape", "viewer-cad-measure", 1),
    ("13-viewer-measure-landscape", "viewer-measure", 1),
    ("14-cloud-guide", "cloud", 0),
    ("15-system-picker", "picker", 0),
]
for label, shot, rotation in SHOTS:
    adb("shell", "am", "force-stop", PKG)
    rotate(rotation)
    adb("shell", "am", "start", "-W", "-n", ACT, "--es", "swiftcad_shot", shot)
    if rotation:
        time.sleep(3)
        rotate(rotation)  # dat lai sau khi mo app; khoa truoc khi mo khong phai luc nao cung giu
    time.sleep(9 if shot in ("picker", "cloud") or shot.startswith("viewer-cad") else 6)
    screencap(f"{OUT}/{NAME}-{label}.png")
    log.append(f"{label}: focus={focused_package()}")
rotate(0)

crash = adb("logcat", "-d", "-b", "crash")
log.append("logcat crash buffer: " + ("trong" if not crash.strip() else "\n" + crash[-4000:]))
# Kiem tra du anh bat buoc va khong crash; tra ma loi 1 neu thieu.
missing = [label for label, _, _ in SHOTS
           if not os.path.isfile(f"{OUT}/{NAME}-{label}.png") or os.path.getsize(f"{OUT}/{NAME}-{label}.png") < 10_000]
problems = [f"thieu/rong anh: {', '.join(missing)}"] if missing else []
if actual != LOCALE:
    problems.append(f"ngon ngu thiet bi khong dat {LOCALE} (thuc te {actual})")
if crash.strip():
    problems.append("app crash (logcat crash buffer khong trong)")
if FONT != "1.0" and adb("shell", "settings", "get", "system", "font_scale").strip() != FONT:
    problems.append(f"font_scale khong dat {FONT}")
log.append(f"anh: {len(SHOTS) - len(missing)}/{len(SHOTS)}")
for p in problems:
    log.append("LOI " + p)
    print(f"::error::{NAME}: {p}", flush=True)
open(f"{OUT}/{NAME}-log.txt", "w").write("\n".join(log) + "\n")
print("\n".join(log))
sys.exit(1 if problems else 0)
