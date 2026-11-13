"""Ham dung chung cho script chup anh va kiem tra luong tren Android emulator (adb + uiautomator)."""
import os, re, subprocess, time
import xml.etree.ElementTree as ET

PKG = "vn.siscom.swiftcad"
ACT = f"{PKG}/com.swiftcad.app.MainActivity"  # ten lop Java giu nguyen


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


def screen_height():
    m = re.findall(r"(\d+)x(\d+)", adb("shell", "wm", "size"))
    return int(m[-1][1]) if m else 1920


def swipe_page(direction):
    """Cuon trang: 'down' = xem phan ben duoi, 'up' = ve phan ben tren."""
    h = screen_height()
    y0, y1 = (int(h * 0.7), int(h * 0.3)) if direction == "down" else (int(h * 0.3), int(h * 0.7))
    adb("shell", "input", "swipe", "540", str(y0), "540", str(y1), "350")
    time.sleep(1.2)


def find_scrolling(texts, timeout=15, contains=False):
    """Tim phan tu; neu khong co tren man hinh thi cuon len dau trang roi cuon dan xuong de tim.
    Chi doi vi tri nhin, khong bo qua dieu kien kiem tra."""
    n = find(texts, min(timeout, 6), contains)
    if n is not None:
        return n
    for _ in range(4):
        swipe_page("up")
    n = find(texts, 2, contains)
    for _ in range(8):
        if n is not None:
            return n
        swipe_page("down")
        n = find(texts, 2, contains)
    return n


def tap(texts, timeout=15, contains=False):
    n = find_scrolling(texts, timeout, contains)
    if n is None:
        raise LookupError(f"Khong tim thay: {texts}")
    # Phan tu nam sat day man hinh co the bi thanh dieu huong che: cuon len roi tim lai.
    h = screen_height()
    for _ in range(3):
        if center(n)[1] < h * 0.72:
            break
        adb("shell", "input", "swipe", "540", str(int(h * 0.7)), "540", str(int(h * 0.3)), "400")
        time.sleep(1.5)
        n = find(texts, 5, contains) or n
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


def png_rgba(path):
    """Giai ma PNG 8-bit RGB/RGBA khong xen ke (anh screencap) bang zlib, khong can thu vien ngoai."""
    import struct, zlib
    data = open(path, "rb").read()
    pos, idat, w = 8, b"", 0
    while pos < len(data):
        ln, typ = struct.unpack(">I4s", data[pos:pos + 8])
        chunk = data[pos + 8:pos + 8 + ln]
        if typ == b"IHDR":
            w, h, depth, ctype = struct.unpack(">IIBB", chunk[:10])
        elif typ == b"IDAT":
            idat += chunk
        pos += 12 + ln
    bpp = 4 if ctype == 6 else 3
    raw = zlib.decompress(idat)
    stride = w * bpp
    rows, prev, i = [], bytearray(stride), 0
    for _ in range(h):
        f = raw[i]; line = bytearray(raw[i + 1:i + 1 + stride]); i += 1 + stride
        for x in range(stride):
            a = line[x - bpp] if x >= bpp else 0
            b = prev[x]; c = prev[x - bpp] if x >= bpp else 0
            if f == 1: line[x] = (line[x] + a) & 255
            elif f == 2: line[x] = (line[x] + b) & 255
            elif f == 3: line[x] = (line[x] + (a + b) // 2) & 255
            elif f == 4:
                p = a + b - c; pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                line[x] = (line[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        rows.append(bytes(line)); prev = line
    return w, h, bpp, rows


def drawn_ratio(path, x0, y0, x1, y1, bg=(0x11, 0x1C, 0x25), step=6):
    """Ti le diem anh khac nen CAD trong vung (ti le 0..1 theo kich thuoc anh). Dung de phat hien khung CAD trong."""
    w, h, bpp, rows = png_rgba(path)
    total = diff = 0
    for y in range(int(y0 * h), int(y1 * h), step):
        row = rows[y]
        for x in range(int(x0 * w), int(x1 * w), step):
            r, g, b = row[x * bpp], row[x * bpp + 1], row[x * bpp + 2]
            total += 1
            if abs(r - bg[0]) + abs(g - bg[1]) + abs(b - bg[2]) > 40:
                diff += 1
    return diff / max(total, 1)
