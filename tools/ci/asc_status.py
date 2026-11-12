#!/usr/bin/env python3
"""Chi doc trang thai tren App Store Connect (khong ghi, khong xoa, khong in khoa/chung chi).

Dung: asc_status.py <bundle_id>
In: app ID, cac build (so, phien ban, trang thai xu ly, het han), nhom thu nghiem noi bo va so nguoi trong nhom.
"""
import os, sys
sys.path.insert(0, os.path.dirname(__file__))
from asc_signing import call  # tai su dung ham goi API (khong in token)

bundle_id = sys.argv[1]
apps = [a for a in call("GET", f"/apps?filter[bundleId]={bundle_id}&limit=10")["data"]
        if a["attributes"]["bundleId"] == bundle_id]
if not apps:
    print(f"::error::Khong co ung dung nao co bundle ID {bundle_id}"); sys.exit(1)
app = apps[0]; aid = app["id"]
print(f"::notice::Ung dung: {app['attributes']['name']} | bundle ID {bundle_id} | app ID {aid} | SKU {app['attributes'].get('sku')}")

builds = call("GET", f"/builds?filter[app]={aid}&sort=-uploadedDate&limit=10&fields[builds]=version,uploadedDate,processingState,expired,minOsVersion")["data"]
if not builds:
    print("::warning::Chua co build nao tren Apple cho ung dung nay")
for b in builds:
    at = b["attributes"]
    print(f"::notice::Build {at['version']} | trang thai xu ly: {at.get('processingState')} | tai len: {at.get('uploadedDate')} | het han: {at.get('expired')}")

groups = call("GET", f"/apps/{aid}/betaGroups?limit=50")["data"]
for g in groups:
    ga = g["attributes"]
    testers = call("GET", f"/betaGroups/{g['id']}/betaTesters?limit=200&fields[betaTesters]=email")["data"]
    print(f"::notice::Nhom thu: {ga.get('name')} | noi bo: {ga.get('isInternalGroup')} | so nguoi: {len(testers)} | tu dong them build: {ga.get('hasAccessToAllBuilds')}")

# Nguoi dung trong doi (chi email/vai tro, khong co bi mat) de biet Apple ID cua Owner da la thanh vien chua.
users = call("GET", "/users?limit=200&fields[users]=username,roles,allAppsVisible,provisioningAllowed")["data"]
for u in users:
    ua = u["attributes"]
    # Khong in email/ten thanh vien: log cua repo nay cong khai. Chi dem so nguoi theo vai tro.
    print(f"::notice::Thanh vien doi: vai tro {','.join(ua.get('roles') or [])}")
if not groups:
    print("::warning::Chua co nhom thu nghiem nao cho ung dung nay (can tao nhom noi bo trong TestFlight)")
