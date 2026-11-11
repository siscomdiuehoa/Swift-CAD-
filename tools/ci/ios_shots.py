#!/usr/bin/env python3
"""Chup anh chay that tren iOS simulator bang ShotMode (bien moi truong SWIFTCAD_SHOT qua simctl).

Dung: ios_shots.py <duong_dan_.app>
Tao 2 simulator: iPhone SE (3rd generation) rong 375 pt va mot iPhone thuong rong ~390 pt.
"""
import json, os, subprocess, sys, time

APP = sys.argv[1]
BUNDLE = "com.swiftcad.app"
OUT = "artifacts/ios-shots"
os.makedirs(OUT, exist_ok=True)
log = []


def run(*args, env=None, check=True):
    r = subprocess.run(list(args), capture_output=True, text=True, env=env, timeout=600)
    if check and r.returncode != 0:
        raise RuntimeError(f"{' '.join(args)} -> {r.returncode}: {r.stderr.strip()[:800]}")
    return r.stdout


runtimes = [r for r in json.loads(run("xcrun", "simctl", "list", "runtimes", "-j"))["runtimes"]
            if r.get("isAvailable") and r["identifier"].startswith("com.apple.CoreSimulator.SimRuntime.iOS")]
runtime = sorted(runtimes, key=lambda r: [int(x) for x in r["version"].split(".")])[-1]
# Chi chon loai thiet bi ma runtime nay ho tro (iPhone SE doi dau khong chay iOS moi).
types = runtime.get("supportedDeviceTypes") or json.loads(run("xcrun", "simctl", "list", "devicetypes", "-j"))["devicetypes"]
log.append("ho tro: " + ", ".join(t["name"] for t in types if t["name"].startswith("iPhone")))
log.append(f"runtime={runtime['name']}")


def pick_type(pred):
    for t in reversed(types):
        if pred(t["name"]):
            return t
    return None


import re as _re
small = pick_type(lambda n: n.startswith("iPhone SE")) or pick_type(lambda n: n.endswith(" mini")) or pick_type(lambda n: _re.fullmatch(r"iPhone \d+e", n) is not None)
regular = pick_type(lambda n: _re.fullmatch(r"iPhone \d+", n) is not None)
devices = [("se", small), ("std", regular)]

SHOTS = ["welcome", "drawings", "drawings-empty", "settings", "viewer", "viewer-find", "viewer-measure",
         "viewer-layers", "viewer-notes", "viewer-real", "cloud", "picker"]
LANGS = [("en", "(en)", "en_US"), ("vi", "(vi)", "vi_VN")]

for tag, dtype in devices:
    if dtype is None:
        log.append(f"{tag}: khong co loai thiet bi phu hop")
        continue
    try:
        udid = run("xcrun", "simctl", "create", f"swiftcad-{tag}", dtype["identifier"], runtime["identifier"]).strip()
    except Exception as e:
        log.append(f"LOI tao simulator {dtype['name']}: {e}")
        continue
    run("xcrun", "simctl", "boot", udid)
    run("xcrun", "simctl", "bootstatus", udid, "-b")
    run("xcrun", "simctl", "status_bar", udid, "override", "--time", "9:41", "--batteryLevel", "100", check=False)
    run("xcrun", "simctl", "install", udid, APP)
    log.append(f"{tag}: {dtype['name']} udid={udid}")
    for size_tag, content_size in [("font-default", "large"), ("font-large", "accessibility-medium")]:
        if tag == "std" and size_tag == "font-large":
            continue
        run("xcrun", "simctl", "ui", udid, "content_size", content_size, check=False)
        for lang, langs_arg, locale in LANGS:
            for shot in SHOTS:
                env = dict(os.environ, SIMCTL_CHILD_SWIFTCAD_SHOT=shot)
                try:
                    run("xcrun", "simctl", "launch", "--terminate-running-process", udid, BUNDLE,
                        "-AppleLanguages", langs_arg, "-AppleLocale", locale, env=env)
                    time.sleep(10 if shot in ("picker", "cloud") else 7)
                    name = f"{OUT}/ios-{tag}-{lang}-{size_tag}-{shot}.png"
                    run("xcrun", "simctl", "io", udid, "screenshot", name)
                    log.append(f"ok {os.path.basename(name)}")
                except Exception as e:
                    log.append(f"LOI {tag} {lang} {size_tag} {shot}: {e}")
    run("xcrun", "simctl", "shutdown", udid, check=False)

open(f"{OUT}/log.txt", "w").write("\n".join(log) + "\n")
print("\n".join(log))
