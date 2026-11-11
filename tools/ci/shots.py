#!/usr/bin/env python3
"""Chup anh chay that tren Android emulator bang adb. Tim nut theo chu hoac nhan tro nang, khong dung toa do co dinh.

CHUA duoc chay thu lan nao. Dung: shots.py <thu_muc_apk> <ten> <WxH> <density> <locale> <font_scale>
"""
import glob, os, re, subprocess, sys, time
import xml.etree.ElementTree as ET

APK_DIR, NAME, SIZE, DENSITY, LOCALE, FONT = sys.argv[1:7]
PKG = "com.swiftcad.app"
OUT = "artifacts/shots"
os.makedirs(OUT, exist_ok=True)
T = {
    "en-US": dict(guest="Use without an account", find="Find", measure="Measure", layers="Layers", notes="Notes", settings="Settings"),
    "vi-VN": dict(guest="Dùng không cần tài khoản", find="Tìm", measure="Đo", layers="Layer", notes="Ghi chú", settings="Cài đặt"),
}[LOCALE]


def adb(*args, check=True):
    return subprocess.run(["adb", *args], check=check, capture_output=True, text=True).stdout


def shot(label):
    time.sleep(1.5)
    with open(f"{OUT}/{NAME}-{label}.png", "wb") as f:
        f.write(subprocess.run(["adb", "exec-out", "screencap", "-p"], check=True, capture_output=True).stdout)


def tap(text, timeout=20):
    """Cham vao phan tu co text hoac content-desc bang dung chuoi cho truoc."""
    end = time.time() + timeout
    while time.time() < end:
        adb("shell", "uiautomator", "dump", "/sdcard/ui.xml", check=False)
        xml = adb("shell", "cat", "/sdcard/ui.xml", check=False)
        try:
            for node in ET.fromstring(xml).iter("node"):
                if text in (node.get("text"), node.get("content-desc")):
                    x1, y1, x2, y2 = map(int, re.findall(r"\d+", node.get("bounds")))
                    adb("shell", "input", "tap", str((x1 + x2) // 2), str((y1 + y2) // 2))
                    return
        except ET.ParseError:
            pass
        time.sleep(1)
    shot("LOI-khong-tim-thay")
    raise SystemExit(f"Khong tim thay phan tu: {text}")


adb("root", check=False)
time.sleep(2)
adb("shell", "wm", "size", SIZE)
adb("shell", "wm", "density", DENSITY)
adb("shell", "settings", "put", "system", "font_scale", FONT)
adb("shell", "setprop", "persist.sys.locale", LOCALE)
adb("shell", "stop"); adb("shell", "start")
adb("wait-for-device")
while adb("shell", "getprop", "sys.boot_completed", check=False).strip() != "1":
    time.sleep(2)
time.sleep(5)

apk = sorted(glob.glob(os.path.join(APK_DIR, "*-Signed.apk")) or glob.glob(os.path.join(APK_DIR, "*.apk")))[0]
adb("install", "-r", apk)
adb("shell", "monkey", "-p", PKG, "-c", "android.intent.category.LAUNCHER", "1")
time.sleep(8)

shot("01-welcome")
tap(T["guest"]); shot("02-drawings")
tap("MEP-Floor-01.demo"); time.sleep(2); shot("03-viewer")
for i, key in enumerate(["find", "measure", "layers", "notes"], start=4):
    tap(T[key]); shot(f"{i:02d}-{key}")
adb("shell", "settings", "put", "system", "user_rotation", "1")
adb("shell", "settings", "put", "system", "accelerometer_rotation", "0")
shot("08-viewer-landscape")
adb("shell", "settings", "put", "system", "user_rotation", "0")
adb("shell", "input", "keyevent", "KEYCODE_BACK"); shot("09-back-to-drawings")
tap(T["settings"]); shot("10-settings")
print("Xong:", sorted(os.listdir(OUT)))
