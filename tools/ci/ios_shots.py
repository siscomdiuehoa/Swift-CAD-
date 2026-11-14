#!/usr/bin/env python3
"""Chup anh chay that tren iOS simulator bang ShotMode (bien moi truong SWIFTCAD_SHOT qua simctl).

Dung: ios_shots.py <duong_dan_.app>
Ma tran bat buoc: iPhone SE (rong 375 pt) va iPhone thuong (~390 pt); tieng Anh va tieng Viet;
co chu mac dinh (ca hai may) va co chu lon accessibility-medium (iPhone SE).
Script tra ma loi 1 neu thieu bat ky anh nao trong ma tran, khong tao/khoi dong duoc simulator,
khong dat duoc co chu, hoac co loi chup. Log luon duoc ghi truoc khi thoat.

Kiem thu am (chi dung de chung minh job do khi co loi): SWIFTCAD_SELFTEST=ios-create hoac ios-shot.
"""
import json, os, re, subprocess, sys, time

APP = sys.argv[1]
BUNDLE = "vn.siscom.swiftcad"
OUT = "artifacts/ios-shots"
SELFTEST = os.environ.get("SWIFTCAD_SELFTEST", "none")
os.makedirs(OUT, exist_ok=True)
log, failures = [], []

SHOTS = ["welcome", "drawings", "drawings-empty", "settings", "viewer", "viewer-find", "viewer-measure",
         "viewer-layers", "viewer-notes", "viewer-real", "picker",
         # PR #3: ban ve DXF that doc qua ACadSharp
         "viewer-cad", "viewer-cad-find", "viewer-cad-measure", "viewer-cad-layers"]
LANGS = [("en", "(en)", "en_US"), ("vi", "(vi)", "vi_VN")]
# (thiet bi, co chu, gia tri content_size)
CONFIGS = [("se", "font-default", "large"), ("se", "font-large", "accessibility-medium"), ("std", "font-default", "large")]


def shot_name(tag, lang, size_tag, shot):
    return f"{OUT}/ios-{tag}-{lang}-{size_tag}-{shot}.png"


EXPECTED = [shot_name(t, l, s, sh) for t, s, _ in CONFIGS for l, _, _ in LANGS for sh in SHOTS]


def fail(msg):
    failures.append(msg)
    log.append("LOI " + msg)
    print(f"::error::{msg}", flush=True)


def run(*args, env=None, check=True):
    r = subprocess.run(list(args), capture_output=True, text=True, env=env, timeout=600)
    if check and r.returncode != 0:
        raise RuntimeError(f"{' '.join(args)} -> {r.returncode}: {r.stderr.strip()[:800]}")
    return r.stdout


def finish():
    present = [p for p in EXPECTED if os.path.isfile(p) and os.path.getsize(p) > 10_000]
    missing = [os.path.basename(p) for p in EXPECTED if p not in present]
    log.append(f"ma tran: {len(present)}/{len(EXPECTED)} anh")
    if missing:
        fail(f"thieu {len(missing)}/{len(EXPECTED)} anh bat buoc, vi du: {', '.join(missing[:6])}")
    open(f"{OUT}/log.txt", "w").write("\n".join(log) + "\n")
    print("\n".join(log))
    sys.exit(1 if failures else 0)


try:
    runtimes = [r for r in json.loads(run("xcrun", "simctl", "list", "runtimes", "-j"))["runtimes"]
                if r.get("isAvailable") and r["identifier"].startswith("com.apple.CoreSimulator.SimRuntime.iOS")]
    runtime = sorted(runtimes, key=lambda r: [int(x) for x in r["version"].split(".")])[-1]
    log.append(f"runtime={runtime['name']}")
    types = runtime.get("supportedDeviceTypes") or json.loads(run("xcrun", "simctl", "list", "devicetypes", "-j"))["devicetypes"]
    log.append("ho tro: " + ", ".join(t["name"] for t in types if t["name"].startswith("iPhone")))
except Exception as e:
    fail(f"khong doc duoc runtime iOS: {e}")
    finish()


def pick_type(pred):
    for t in reversed(types):
        if pred(t["name"]):
            return t
    return None


devices = {
    "se": pick_type(lambda n: n == "iPhone SE (3rd generation)") or pick_type(lambda n: n.startswith("iPhone SE")),
    "std": pick_type(lambda n: n in ("iPhone 16", "iPhone 17", "iPhone 15")) or pick_type(lambda n: re.fullmatch(r"iPhone \d+", n) is not None),
}

for tag in ("se", "std"):
    dtype = devices[tag]
    if dtype is None:
        fail(f"{tag}: khong co loai thiet bi phu hop trong runtime")
        continue
    type_id = dtype["identifier"] + (".selftest-khong-ton-tai" if SELFTEST == "ios-create" and tag == "se" else "")
    try:
        udid = run("xcrun", "simctl", "create", f"swiftcad-{tag}", type_id, runtime["identifier"]).strip()
        run("xcrun", "simctl", "boot", udid)
        run("xcrun", "simctl", "bootstatus", udid, "-b")
        run("xcrun", "simctl", "install", udid, APP)
    except Exception as e:
        fail(f"{tag}: khong tao/khoi dong/cai dat duoc simulator {dtype['name']}: {e}")
        continue
    run("xcrun", "simctl", "status_bar", udid, "override", "--time", "9:41", "--batteryLevel", "100", check=False)
    log.append(f"{tag}: {dtype['name']} udid={udid}")
    # Lan mo dau tien cham hon (khoi tao runtime), chay khoi dong truoc de anh dau khong bi man hinh cho.
    run("xcrun", "simctl", "launch", udid, BUNDLE, check=False)
    time.sleep(20)
    for ctag, size_tag, content_size in CONFIGS:
        if ctag != tag:
            continue
        try:
            run("xcrun", "simctl", "ui", udid, "content_size", content_size)
            actual = run("xcrun", "simctl", "ui", udid, "content_size").strip()
            if actual != content_size:
                raise RuntimeError(f"content_size thuc te '{actual}', mong doi '{content_size}'")
            log.append(f"{tag} {size_tag}: content_size={actual}")
        except Exception as e:
            fail(f"{tag} {size_tag}: khong dat duoc co chu: {e}")
            continue
        for lang, langs_arg, locale in LANGS:
            for shot in SHOTS:
                env = dict(os.environ, SIMCTL_CHILD_SWIFTCAD_SHOT=shot)
                name = shot_name(tag, lang, size_tag, shot)
                if SELFTEST == "ios-shot" and shot == "settings" and tag == "std" and lang == "vi":
                    name = "/khong-ghi-duoc/" + os.path.basename(name)
                try:
                    run("xcrun", "simctl", "launch", "--terminate-running-process", udid, BUNDLE,
                        "-AppleLanguages", langs_arg, "-AppleLocale", locale, env=env)
                    time.sleep(10 if shot in ("picker", "cloud") or shot.startswith("viewer-cad") else 7)
                    run("xcrun", "simctl", "io", udid, "screenshot", name)
                    log.append(f"ok {os.path.basename(name)}")
                except Exception as e:
                    fail(f"{tag} {lang} {size_tag} {shot}: {e}")
    run("xcrun", "simctl", "shutdown", udid, check=False)

finish()
