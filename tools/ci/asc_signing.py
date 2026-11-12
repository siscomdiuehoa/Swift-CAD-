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
    found = [b for b in call("GET", f"/bundleIds?filter[identifier]={bundle_id}&limit=200")["data"]
             if b["attributes"]["identifier"] == bundle_id]
    if found:
        bid = found[0]["id"]
    else:
        bid = call("POST", "/bundleIds", {"data": {"type": "bundleIds", "attributes": {
            "identifier": bundle_id, "name": "Swift CAD", "platform": "IOS"}}})["data"]["id"]
        print(f"::notice::Da dang ky bundle ID {bundle_id}")

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


if __name__ == "__main__":
    if sys.argv[1] == "setup":
        setup(sys.argv[2], sys.argv[3])
    else:
        cleanup(sys.argv[2])
