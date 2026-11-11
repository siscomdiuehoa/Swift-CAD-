"""Ham dung chung cho script chup anh va kiem tra luong tren Android emulator (adb + uiautomator)."""
import os, re, subprocess, time
import xml.etree.ElementTree as ET

PKG = "com.swiftcad.app"
ACT = f"{PKG}/{PKG}.MainActivity"


def adb(*args, check=False, timeout=120):
    r = subprocess.run(["adb", *args], capture_output=True, text=True, timeout=timeout)
    if check and r.returncode != 0:
        raise RuntimeError(f"adb {' '.join(args)} -> {r.returncode}: {r.stderr.strip()}")
    return r.stdout


def screencap(path, settle=1.5):
    time.sleep(settle)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    data = subprocess.run(["adb", "exec-out", "screencap", "-p"], capture_output=True, timeout=60).stdout
    with open(path, "wb") as f:
        f.write(data)


DUMP_ERRORS = []


def dump():
    """Lay cay giao dien. Thu lai va ghi lai thong bao loi cua uiautomator de chan doan."""
    for attempt in range(3):
        adb("shell", "rm", "-f", "/sdcard/ui.xml")
        r = subprocess.run(["adb", "shell", "uiautomator", "dump", "--compressed", "/sdcard/ui.xml"] if attempt == 1
                           else ["adb", "shell", "uiautomator", "dump", "/sdcard/ui.xml"],
                           capture_output=True, text=True, timeout=60)
        xml = adb("shell", "cat", "/sdcard/ui.xml")
        start = xml.find("<?xml")
        if start < 0:
            start = xml.find("<hierarchy")
        if start >= 0:
            try:
                return ET.fromstring(xml[start:])
            except ET.ParseError as e:
                msg = f"parse: {e}"
        else:
            msg = (r.stdout + r.stderr).strip()[:300]
        if len(DUMP_ERRORS) < 10:
            DUMP_ERRORS.append(msg)
        time.sleep(1.5)
    return None


def nodes():
    root = dump()
    return [] if root is None else list(root.iter("node"))


def find(texts, timeout=15, contains=False):
    """Tim phan tu theo text hoac content-desc. texts: chuoi hoac danh sach chuoi."""
    if isinstance(texts, str):
        texts = [texts]
    end = time.time() + timeout
    while time.time() < end:
        for n in nodes():
            vals = [n.get("text") or "", n.get("content-desc") or ""]
            for t in texts:
                if any((t in v) if contains else (v == t) for v in vals):
                    # Tra ve dict thuoc tinh: Element la la (khong co con) co gia tri bool False,
                    # nen "if find(...)" se sai neu tra ve Element.
                    return dict(n.attrib)
        time.sleep(1)
    return None


def center(n):
    x1, y1, x2, y2 = map(int, re.findall(r"\d+", n.get("bounds")))
    return (x1 + x2) // 2, (y1 + y2) // 2


def tap_node(n):
    x, y = center(n)
    adb("shell", "input", "tap", str(x), str(y))


def tap(texts, timeout=15, contains=False):
    n = find(texts, timeout, contains)
    if n is None:
        raise LookupError(f"Khong tim thay: {texts}")
    tap_node(n)
    return n


def focused_package():
    out = adb("shell", "dumpsys", "window")
    m = re.search(r"mCurrentFocus=Window\{[^ ]+ [^ ]+ ([^/ }]+)", out)
    return m.group(1) if m else ""


def screen_texts():
    return [(n.get("text") or "") + "|" + (n.get("content-desc") or "") for n in nodes()]


def setup_device(size, density, font, locale):
    adb("root")
    time.sleep(3)
    adb("wait-for-device")
    current = adb("shell", "getprop", "persist.sys.locale").strip()
    if current != locale:
        # Doi ngon ngu he thong roi khoi dong lai framework.
        adb("shell", "setprop", "persist.sys.locale", locale)
        adb("shell", "setprop", "sys.boot_completed", "0")
        adb("shell", "stop")
        time.sleep(2)
        adb("shell", "start")
        end = time.time() + 240
        while time.time() < end and adb("shell", "getprop", "sys.boot_completed").strip() != "1":
            time.sleep(3)
        time.sleep(15)
    # Dat sau khi doi ngon ngu: khoi dong lai framework lam mat font_scale.
    adb("shell", "wm", "size", size)
    adb("shell", "wm", "density", density)
    adb("shell", "settings", "put", "system", "font_scale", font)
    rotate(0)
    adb("shell", "input", "keyevent", "KEYCODE_WAKEUP")
    adb("shell", "wm", "dismiss-keyguard")
    return adb("shell", "getprop", "persist.sys.locale").strip()


def install(apk_dir):
    import glob
    apks = sorted(glob.glob(os.path.join(apk_dir, "*-Signed.apk"))) or sorted(glob.glob(os.path.join(apk_dir, "*.apk")))
    if not apks:
        raise FileNotFoundError(f"Khong co APK trong {apk_dir}")
    print(adb("install", "-r", "-g", apks[0], check=True, timeout=300))
    return apks[0]


def rotate(r):
    """Khoa huong man hinh: 0 doc, 1 ngang."""
    adb("shell", "settings", "put", "system", "accelerometer_rotation", "0")
    adb("shell", "settings", "put", "system", "user_rotation", str(r))
    adb("shell", "wm", "user-rotation", "lock", str(r))
