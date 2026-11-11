#!/usr/bin/env python3
"""Ghim workload iOS theo phien ban Xcode co tren may chay.

Workload iOS moi nhat co the doi Xcode chua co tren may GitHub (vi du .NET for iOS 27.2 doi Xcode 27.2).
Script nay chon goi manifest iOS cung bang SDK, co major.minor khong vuot qua Xcode dang chon,
roi ghi rollback.json de 'dotnet workload install --from-rollback-file' dung.
"""
import json, re, subprocess, sys, urllib.request

xcode = subprocess.run(["xcodebuild", "-version"], capture_output=True, text=True).stdout
xv = tuple(int(x) for x in re.search(r"Xcode (\d+)\.(\d+)", xcode).groups())
sdk = subprocess.run(["dotnet", "--version"], capture_output=True, text=True).stdout.strip()
major, minor, patch = sdk.split("-")[0].split(".")
print(f"::notice::dotnet SDK {sdk}")
versions = None
for feature in range(int(patch[0]), 0, -1):
    band = f"{major}.{minor}.{feature}00"
    pkg = f"microsoft.net.sdk.ios.manifest-{band}"
    url = f"https://api.nuget.org/v3-flatcontainer/{pkg}/index.json"
    try:
        versions = json.load(urllib.request.urlopen(url, timeout=60))["versions"]
        print(f"::notice::Tim thay {pkg}: {len(versions)} phien ban, moi nhat {versions[-1]}")
        break
    except Exception as e:
        print(f"::notice::Khong co {pkg} ({e})")
if versions is None:
    sys.exit("Khong tim thay goi manifest iOS")


def key(v):
    return tuple(int(x) for x in re.findall(r"\d+", v.split("-")[0]))


stable = [v for v in versions if "-" not in v and key(v)[:2] <= xv]
if not stable:
    sys.exit(f"Khong co manifest iOS nao hop voi Xcode {xv} trong {pkg}")
chosen = max(stable, key=key)
json.dump({"microsoft.net.sdk.ios": f"{chosen}/{band}"}, open("rollback.json", "w"))
print(f"::notice::Xcode {xv[0]}.{xv[1]}, SDK {sdk}, ghim workload iOS {chosen} (bang {band})")
