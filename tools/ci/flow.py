#!/usr/bin/env python3
"""Kiem tra luong bang thao tac that (uiautomator) tren Android emulator, khong dung ShotMode.

Ghi DAT/LOI tung buoc vao artifacts/flow/results.txt va chup anh moi buoc.
Dung: flow.py <thu_muc_apk>
"""
import os, sys, time

SELFTEST = os.environ.get("SWIFTCAD_SELFTEST", "none")
sys.path.insert(0, os.path.dirname(__file__))
from android_common import (ACT, DUMP_ERRORS, PKG, adb, drawn_ratio, find, focused_package, install, rotate, screen_texts,
                            screencap, setup_device, tap)

APK_DIR = sys.argv[1]
OUT = "artifacts/flow"
os.makedirs(OUT, exist_ok=True)
results = []
step_no = [0]


def step(name, fn):
    step_no[0] += 1
    tag = f"{step_no[0]:02d}"
    try:
        detail = fn() or ""
        results.append(f"DAT  {tag} {name} {detail}")
    except Exception as e:  # ghi lai, tiep tuc cac buoc sau de du anh chan doan; cuoi script tra ma loi 1
        results.append(f"LOI  {tag} {name}: {e}")
        print(f"::error::Kiem tra luong buoc {tag} {name}: {e}", flush=True)
        # Chan doan: ghi cay giao dien hien tai.
        try:
            adb("shell", "uiautomator", "dump", "/sdcard/ui.xml")
            open(f"{OUT}/{tag}-{name}-ui.xml", "w").write(adb("shell", "cat", "/sdcard/ui.xml"))
            texts = [t for t in screen_texts() if t != "|"]
            results.append("     man hinh: " + " ; ".join(texts[:25]))
        except Exception as e2:
            results.append(f"     khong ghi duoc cay giao dien: {e2}")
    screencap(f"{OUT}/{tag}-{name}.png", settle=1.0)
    print(results[-1], flush=True)


def launch():
    adb("shell", "am", "start", "-W", "-n", ACT)
    time.sleep(6)


def restart():
    adb("shell", "am", "force-stop", PKG)
    launch()


def must(cond, msg):
    if not cond:
        raise AssertionError(msg)


actual = setup_device("1080x2340", "480", "1.0", "en-US")
install(APK_DIR)
results.append(f"INFO locale={actual}")

# Tep DXF kiem thu cho trinh chon tep (noi dung toi thieu, khong phai ban ve that).
dxf = "/sdcard/Download/TEST-Flow-Plan.dxf"
# Tep DXF do ACadSharp ghi tu ban ve kiem thu tu tao (tests/SwiftCad.Cad.Tests), co chu tieng Viet va doi tuong chua ho tro.
adb("push", os.path.join(os.path.dirname(__file__), "fixtures", "TEST-Flow-Plan.dxf"), dxf)
adb("shell", "am", "broadcast", "-a", "android.intent.action.MEDIA_SCANNER_SCAN_FILE", "-d", "file://" + dxf)
adb("shell", "content", "call", "--uri", "content://media/external/file", "--method", "scan_volume", "--arg", "external_primary")
time.sleep(3)

launch()
step("welcome-hien-lan-dau", lambda: must(find("Use without an account", 20), "khong thay man hinh chao"))
step("bam-dung-khong-tai-khoan", lambda: (tap("Use without an account"), must(find("Choose file", 15), "khong vao Ban ve")))
step("khong-co-trang-thai-da-ket-noi", lambda: must(len(screen_texts()) > 0, "khong doc duoc cay giao dien") or must(not any(("Connected" in t or "Syncing" in t) for t in screen_texts()), "co chu Connected/Syncing"))


def drive_cancel():
    tap("Open a drawing from Google Drive")
    must(find("Open file picker", 10), "khong thay huong dan Drive")
    tap("Open file picker")
    time.sleep(4)
    pkg = focused_package()
    must("documentsui" in pkg, f"khong mo trinh chon tep he thong (focus={pkg})")
    screencap(f"{OUT}/drive-picker-he-thong.png")
    adb("shell", "input", "keyevent", "KEYCODE_BACK")
    time.sleep(3)
    must(PKG in focused_package(), "khong quay ve app sau khi huy")
    must(find("Choose file", 10), "khong o man hinh Ban ve sau khi huy")
    must(not any("Connected" in t for t in screen_texts()), "hien Connected sau khi huy")
    return f"(picker={pkg})"


step("drive-mo-trinh-chon-he-thong-va-huy", drive_cancel)


def dropbox_cancel_alert():
    tap("Open a drawing from Dropbox")
    must(find("Open file picker", 10), "khong thay huong dan Dropbox")
    tap("Cancel")
    time.sleep(2)
    must(find("Choose file", 10), "khong quay ve sau khi huy huong dan")


step("dropbox-huy-o-buoc-huong-dan", dropbox_cancel_alert)


def tap_text_entry_and_type(text):
    tap("Find text")
    time.sleep(1)
    adb("shell", "input", "text", text)
    time.sleep(2)
    # Dong ban phim de khong che thanh tren (nut Quay lai).
    adb("shell", "input", "keyevent", "KEYCODE_BACK")
    time.sleep(1)


def pick_file():
    tap("Choose file")
    time.sleep(4)
    must("documentsui" in focused_package(), "khong mo trinh chon tep")
    if not find("TEST-Flow-Plan.dxf", 6):
        # Mo danh sach vi tri va chon Downloads.
        try:
            tap(["Show roots", "Hiện gốc"], 6, contains=True)
            tap(["Downloads", "Download"], 8)
        except LookupError:
            pass
    tap("TEST-Flow-Plan.dxf", 15)
    time.sleep(5)
    must(PKG in focused_package(), "khong quay ve app sau khi chon tep")
    # Tep that duoc doc bang ACadSharp: co thanh cong cu, co canh bao hien thi don gian hoa, khong co nhan Demo.
    must(find("Find", 30), "khong mo duoc man hinh xem sau khi doc tep")
    must(find("Simplified display", 5, contains=True), "thieu canh bao hien thi don gian hoa")
    must(not find("not shown yet: 0", 2, contains=True), "canh bao neu 0 doi tuong")
    must(find("TEST-Flow-Plan.dxf", 5, contains=True), "khong thay ten tep vua chon")
    must(not find("Demo", 2), "tep that bi gan nhan Demo")
    tap("Find")
    tap_text_entry_and_type("phong")
    must(find("Result 1 of", 10, contains=True), "khong tim thay chu trong ban ve that")


step("chon-tep-dxf-tu-trinh-chon", pick_file)
step("quay-lai-thay-trong-gan-day", lambda: (tap("Back"), must(find("Open TEST-Flow-Plan.dxf", 10), "khong co trong Gan day")))
step("mo-lai-app-khong-hien-chao", lambda: (restart(), must(find("Choose file", 15) and not find("Use without an account", 2), "man hinh chao hien lai")))
step("mo-lai-tu-gan-day", lambda: (tap("Open TEST-Flow-Plan.dxf"), must(find("Simplified display", 30, contains=True), "khong mo duoc tu Gan day"), tap("Back")))


def picker_cancel():
    tap("Choose file")
    time.sleep(4)
    must("documentsui" in focused_package(), "khong mo trinh chon tep")
    adb("shell", "input", "keyevent", "KEYCODE_BACK")
    time.sleep(3)
    must(find("Choose file", 10), "khong o Ban ve sau khi huy")
    must(not find("Could not open the file", 2), "bao loi khi nguoi dung huy")


step("huy-chon-tep-khong-bao-loi", picker_cancel)
step("mo-tep-mau", lambda: (tap("Open MEP-Floor-01.demo"), must(find("Demo", 10), "khong thay nhan Demo")))
for tool, check in [("Find", "Search text in the drawing"), ("Measure", "Pick the first point"),
                    ("Layers", "Show all"), ("Notes", "Tap the drawing to pin a note")]:
    step(f"cong-cu-{tool.lower()}", lambda tool=tool, check=check: (tap(tool), must(find(check, 8), f"bang {tool} khong mo")))
def landscape_check():
    rotate(1)
    time.sleep(4)
    must(find("Notes", 8), "mat thanh cong cu khi xoay")
    shot = f"{OUT}/xoay-ngang-kiem-tra.png"
    screencap(shot, settle=1.0)
    # Vung CAD o ben trai khi xoay ngang: phai co net ve, khong duoc trong.
    # Nguong 0,03: anh dung do 0,107-0,121; anh khung trong (loi 7cd5f34) do 0,000.
    ratio = drawn_ratio(shot, 0.05, 0.35, 0.35, 0.75)
    must(ratio > 0.03, f"khung CAD trong sau khi xoay (ti le net ve {ratio:.4f})")
    return f"(ti le net ve {ratio:.3f})"


step("xoay-ngang", landscape_check)
rotate(0)
time.sleep(2)
step("quay-lai-ban-ve", lambda: (tap("Back"), must(find("Choose file", 10), "khong ve Ban ve")))
step("doi-sang-tieng-viet", lambda: (tap("Settings"), tap("Tiếng Việt", 10), must(find("Ngôn ngữ", 10), "giao dien chua doi sang tieng Viet")))
step("ngon-ngu-giu-sau-khi-mo-lai", lambda: (restart(), must(find("Chọn tệp", 15), "khong giu tieng Viet sau khi mo lai")))
step("doi-lai-theo-thiet-bi", lambda: (tap("Cài đặt"), tap("Theo thiết bị", 10), must(find("Language", 10), "khong ve tieng Anh theo thiet bi")))

if SELFTEST == "android-flow":
    # Kiem thu am: buoc co y that bai de chung minh job do khi co buoc loi.
    step("kiem-thu-am-co-y-loi", lambda: must(find("Chuoi khong ton tai trong app", 3), "co y that bai (SWIFTCAD_SELFTEST=android-flow)"))

crash = adb("logcat", "-d", "-b", "crash")
results.append("INFO loi uiautomator dump: " + (" || ".join(DUMP_ERRORS) or "khong co"))
results.append("INFO logcat crash: " + ("trong" if not crash.strip() else crash[-3000:]))
open(f"{OUT}/results.txt", "w").write("\n".join(results) + "\n")
print("\n".join(results))
fails = [r for r in results if r.startswith("LOI")]
if crash.strip():
    fails.append("logcat crash khong trong")
    print("::error::App crash trong kiem tra luong (xem results.txt)", flush=True)
print(f"\n{len(fails)} loi")
# Anh va results.txt da ghi xong o tren; tra ma loi de job do khi co bat ky buoc nao loi.
sys.exit(1 if fails else 0)
