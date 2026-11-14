#!/usr/bin/env python3
"""Chup anh App Store dung kich thuoc: iPhone 6.9 inch va iPad 13 inch, tieng Anh va tieng Viet.

Dung: store_shots.py <duong_dan_.app>. Anh luu o artifacts/store-shots/<thiet-bi>/<ngon-ngu>/NN-<man>.png.
Tra ma loi 1 neu thieu anh hoac anh sai kich thuoc Apple chap nhan.
"""
import json, os, re, struct, subprocess, sys, time

APP = sys.argv[1]
BUNDLE = "vn.siscom.swiftcad"
OUT = "artifacts/store-shots"
SHOTS = ["welcome", "store-home", "store-viewer", "store-find", "store-measure", "store-layers", "store-notes"]
LANGS = [("en", "(en)", "en_US")]  # Ảnh cửa hàng bằng tiếng Anh
# Kich thuoc Apple chap nhan (doc), theo tai lieu Screenshot specifications.
ACCEPT = {"iphone-6.9": {(1320, 2868), (1290, 2796), (1260, 2736)}, "ipad-13": {(2064, 2752), (2048, 2732)}}
log, failures = [], []


def run(*a, env=None, check=True):
    r = subprocess.run(list(a), capture_output=True, text=True, env=env, timeout=600)
    if check and r.returncode != 0:
        raise RuntimeError(f"{' '.join(a)} -> {r.returncode}: {r.stderr.strip()[:500]}")
    return r.stdout


def png_size(path):
    with open(path, "rb") as f:
        head = f.read(24)
    return struct.unpack(">II", head[16:24])


def fail(m):
    failures.append(m); log.append("LOI " + m); print(f"::error::{m}", flush=True)


rt = sorted([r for r in json.loads(run("xcrun", "simctl", "list", "runtimes", "-j"))["runtimes"]
             if r.get("isAvailable") and r["identifier"].startswith("com.apple.CoreSimulator.SimRuntime.iOS")],
            key=lambda r: [int(x) for x in r["version"].split(".")])[-1]
types = [t["name"] for t in rt.get("supportedDeviceTypes", [])]
types_full = {t["name"]: t["identifier"] for t in rt.get("supportedDeviceTypes", [])}


def pick(patterns):
    for p in patterns:
        for n in reversed(types):
            if re.fullmatch(p, n):
                return n
    return None


devices = {
    "iphone-6.9": pick([r"iPhone 17 Pro Max", r"iPhone 16 Pro Max", r"iPhone 1\d Pro Max"]),
    "ipad-13": pick([r"iPad Pro 13-inch \(M5\).*", r"iPad Pro 13-inch \(M4\).*", r"iPad Pro 13-inch.*", r"iPad Air 13-inch.*"]),
}
log.append(f"runtime {rt['name']}; thiet bi {devices}")

for tag, name in devices.items():
    if not name:
        fail(f"{tag}: khong co simulator phu hop"); continue
    try:
        udid = run("xcrun", "simctl", "create", f"store-{tag}", types_full[name], rt["identifier"]).strip()
        run("xcrun", "simctl", "boot", udid); run("xcrun", "simctl", "bootstatus", udid, "-b")
        run("xcrun", "simctl", "install", udid, APP)
    except Exception as e:
        fail(f"{tag}: {e}"); continue
    run("xcrun", "simctl", "status_bar", udid, "override", "--time", "9:41", "--batteryLevel", "100",
        "--batteryState", "charged", "--cellularBars", "4", "--wifiBars", "3", check=False)
    run("xcrun", "simctl", "launch", udid, BUNDLE, check=False); time.sleep(20)
    for lang, larg, loc in LANGS:
        d = f"{OUT}/{tag}/{lang}"; os.makedirs(d, exist_ok=True)
        for i, shot in enumerate(SHOTS, start=1):
            path = f"{d}/{i:02d}-{shot.replace('store-', '')}.png"
            try:
                run("xcrun", "simctl", "launch", "--terminate-running-process", udid, BUNDLE, "-AppleLanguages", larg, "-AppleLocale", loc,
                    env=dict(os.environ, SIMCTL_CHILD_SWIFTCAD_SHOT=shot))
                time.sleep(11)
                run("xcrun", "simctl", "io", udid, "screenshot", path)
                w, h = png_size(path)
                if (w, h) not in ACCEPT[tag]:
                    fail(f"{path}: {w}x{h} khong thuoc kich thuoc Apple chap nhan {sorted(ACCEPT[tag])}")
                else:
                    log.append(f"ok {path} {w}x{h}")
            except Exception as e:
                fail(f"{tag} {lang} {shot}: {e}")
    run("xcrun", "simctl", "shutdown", udid, check=False)

os.makedirs(OUT, exist_ok=True)
open(f"{OUT}/log.txt", "w").write("\n".join(log) + "\n")
print("\n".join(log))
sys.exit(1 if failures else 0)
