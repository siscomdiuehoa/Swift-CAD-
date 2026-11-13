#!/usr/bin/env python3
"""Ky iOS bang App Store Connect API, khong can may Mac cua Owner.

  asc_signing.py setup  <bundle_id> <thu_muc_ra>   tao chung chi Apple Distribution va ho so App Store tam thoi
  asc_signing.py cleanup <thu_muc_ra>              xoa ho so va chung chi da tao (sau khi tai len TestFlight)

Can bien moi truong ASC_KEY_ID, ASC_ISSUER_ID, ASC_KEY_P8 (noi dung tep .p8). Khong in khoa ra log.
Moi lan chay tao mot chung chi moi roi xoa sau khi tai len, de khong cham gioi han so chung chi cua tai khoan.
Thu hoi chung chi phan phoi khong anh huong ban da tai len App Store/TestFlight (Apple ky lai khi phan phoi).
"""
import base64, json, os, subprocess, sys, time, urllib.error, urllib.request

import jwt  # PyJWT, cai trong workflow

API = "https://api.appstoreconnect.apple.com/v1"


def token():
    now = int(time.time())
    return jwt.encode({"iss": os.environ["ASC_ISSUER_ID"], "iat": now, "exp": now + 1100, "aud": "appstoreconnect-v1"},
                      os.environ["ASC_KEY_P8"].replace("\\n", "\n"), algorithm="ES256",
                      headers={"kid": os.environ["ASC_KEY_ID"], "typ": "JWT"})


def call(method, path, body=None):
    req = urllib.request.Request(API + path, method=method, data=json.dumps(body).encode() if body else None,
                                 headers={"Authorization": "Bearer " + token(), "Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            raw = r.read()
            return json.loads(raw) if raw else {}
    except urllib.error.HTTPError as e:
        detail = e.read().decode(errors="replace")[:800]
        print(f"::error::App Store Connect {method} {path} -> {e.code}: {detail}", flush=True)
        sys.exit(1)


def sh(*args):
    subprocess.run(args, check=True, capture_output=True)


def setup(bundle_id, out):
    os.makedirs(out, exist_ok=True)
    # Kiem tra truoc: phai co ung dung tren App Store Connect dung bundle ID nay. Khong tu dang ky ID moi,
    # de khong tao/dung nham dinh danh cua ung dung khac.
    apps = [a for a in call("GET", f"/apps?filter[bundleId]={bundle_id}&limit=50")["data"]
            if a["attributes"]["bundleId"] == bundle_id]
    if not apps:
        print(f"::error::Chua co ung dung nao tren App Store Connect dung bundle ID {bundle_id}. "
              "Dang ky App ID va chon dung Bundle ID trong App Information truoc.", flush=True)
        sys.exit(1)
    print(f"::notice::Ung dung App Store Connect: {apps[0]['attributes']['name']} ({bundle_id})")
    found = [b for b in call("GET", f"/bundleIds?filter[identifier]={bundle_id}&limit=200")["data"]
             if b["attributes"]["identifier"] == bundle_id]
    if not found:
        print(f"::error::Khong tim thay App ID {bundle_id} trong Certificates, Identifiers & Profiles", flush=True)
        sys.exit(1)
    bid = found[0]["id"]

    # Khoa rieng va CSR tao ngay tren may CI, khong roi khoi may.
    ossl = "/usr/bin/openssl"  # LibreSSL cua macOS: tep .p12 tuong thich voi 'security import'
    sh(ossl, "req", "-new", "-newkey", "rsa:2048", "-nodes", "-keyout", f"{out}/key.pem", "-out", f"{out}/csr.pem",
       "-subj", "/CN=Swift CAD CI/O=Swift CAD")
    csr = open(f"{out}/csr.pem").read()
    cert = call("POST", "/certificates", {"data": {"type": "certificates", "attributes": {
        "certificateType": "DISTRIBUTION", "csrContent": csr}}})["data"]
    open(f"{out}/cert.cer", "wb").write(base64.b64decode(cert["attributes"]["certificateContent"]))
    sh(ossl, "x509", "-inform", "DER", "-in", f"{out}/cert.cer", "-out", f"{out}/cert.pem")
    pw = base64.b64encode(os.urandom(18)).decode()
    sh(ossl, "pkcs12", "-export", "-inkey", f"{out}/key.pem", "-in", f"{out}/cert.pem", "-out", f"{out}/dist.p12",
       "-passout", f"pass:{pw}")
    os.remove(f"{out}/key.pem")

    prof = call("POST", "/profiles", {"data": {"type": "profiles",
        "attributes": {"name": f"Swift CAD CI {int(time.time())}", "profileType": "IOS_APP_STORE"},
        "relationships": {"bundleId": {"data": {"type": "bundleIds", "id": bid}},
                          "certificates": {"data": [{"type": "certificates", "id": cert["id"]}]}}}})["data"]
    open(f"{out}/app.mobileprovision", "wb").write(base64.b64decode(prof["attributes"]["profileContent"]))
    json.dump({"certificate_id": cert["id"], "profile_id": prof["id"], "p12_password": pw},
              open(f"{out}/ids.json", "w"))
    print(f"::notice::Da tao chung chi {cert['id']} va ho so {prof['id']} tam thoi")


def cleanup(out):
    try:
        ids = json.load(open(f"{out}/ids.json"))
    except OSError:
        return
    call("DELETE", f"/profiles/{ids['profile_id']}")
    call("DELETE", f"/certificates/{ids['certificate_id']}")
    print("::notice::Da xoa chung chi va ho so tam thoi")


def list_apps():
    """In ten, bundle ID, SKU cua cac ung dung trong tai khoan (khong co du lieu bi mat)."""
    for a in call("GET", "/apps?limit=200&fields[apps]=name,bundleId,sku")["data"]:
        at = a["attributes"]
        # Chi in ten va bundle ID ung dung cua chinh doanh nghiep nay; khong in SKU hay ung dung khac vao log cong khai.
        if at["bundleId"] == os.environ.get("EXPECT_BUNDLE_ID", ""):
            print(f"::notice::App Store Connect: {at['name']} | bundle ID {at['bundleId']}")
        else:
            print("::notice::Co them ung dung khac tren tai khoan (khong hien thi trong log cong khai)")


if __name__ == "__main__":
    if sys.argv[1] == "apps":
        list_apps()
    elif sys.argv[1] == "setup":
        setup(sys.argv[2], sys.argv[3])
    else:
        cleanup(sys.argv[2])
