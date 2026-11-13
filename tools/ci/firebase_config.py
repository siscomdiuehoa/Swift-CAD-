#!/usr/bin/env python3
"""Tao src/SwiftCad.App/Resources/Raw/firebase.json tu GitHub Secrets luc build. Tep nay khong co trong repo (.gitignore).

Bien moi truong: FB_PLIST (noi dung GoogleService-Info.plist), FB_JSON (google-services.json),
GOOGLE_DESKTOP_CLIENT_ID va GOOGLE_DESKTOP_CLIENT_SECRET (OAuth client loai Desktop cho Android, tuy chon).
Thieu cau hinh Firebase thi khong tao tep: ung dung an phan dang nhap. Khong in gia tri bi mat.
"""
import json, os, plistlib, sys

out = "src/SwiftCad.App/Resources/Raw/firebase.json"
plist_txt, json_txt = os.environ.get("FB_PLIST", ""), os.environ.get("FB_JSON", "")
if not plist_txt or not json_txt:
    print("::notice::Khong co Secret Firebase: ban build nay an phan dang nhap")
    sys.exit(0)
p = plistlib.loads(plist_txt.encode("utf-8"))
g = json.loads(json_txt)
api_key = p.get("API_KEY") or g["client"][0]["api_key"][0]["current_key"]
cfg = {
    "apiKey": api_key,
    "projectId": p.get("PROJECT_ID") or g["project_info"]["project_id"],
    "iosClientId": p.get("CLIENT_ID", ""),
    "iosReversedClientId": p.get("REVERSED_CLIENT_ID", ""),
    "desktopClientId": os.environ.get("GOOGLE_DESKTOP_CLIENT_ID", ""),
    "desktopClientSecret": os.environ.get("GOOGLE_DESKTOP_CLIENT_SECRET", ""),
}
assert p.get("BUNDLE_ID") in (None, "vn.siscom.swiftcad"), "GoogleService-Info.plist sai bundle ID"
os.makedirs(os.path.dirname(out), exist_ok=True)
json.dump(cfg, open(out, "w"))
print(f"::notice::Da tao firebase.json (du an {cfg['projectId']}; Google iOS: {'co' if cfg['iosClientId'] else 'khong'}; "
      f"Google Android: {'co' if cfg['desktopClientId'] else 'khong (thieu OAuth client Desktop)'})")
