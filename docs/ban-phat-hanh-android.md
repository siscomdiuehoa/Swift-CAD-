# Phát hành Android (Google Play) — cách tạo khóa ký

Khóa ký phát hành phải giữ suốt đời ứng dụng. Mất khóa là không cập nhật được app trên Play.

## 1. Tạo khóa (làm trên máy của Owner, một lần)
Mở Terminal (macOS/Linux) hoặc Command Prompt có Java (Windows):

    keytool -genkeypair -v -keystore swiftcad-release.jks -alias swiftcad -keyalg RSA -keysize 2048 -validity 10000

Trả lời các câu hỏi: đặt mật khẩu kho khóa và mật khẩu khóa (ghi lại trong trình quản lý mật khẩu), tên/tổ chức: SISCOM.

## 2. Đưa vào GitHub Secrets (repo → Settings → Secrets and variables → Actions)
- `ANDROID_KEYSTORE_BASE64`: mã base64 của tệp .jks.
  - macOS/Linux: `base64 -i swiftcad-release.jks | pbcopy` (macOS) hoặc `base64 swiftcad-release.jks`
  - Windows (PowerShell): `[Convert]::ToBase64String([IO.File]::ReadAllBytes("swiftcad-release.jks"))`
- `ANDROID_KEYSTORE_PASSWORD`: mật khẩu kho khóa.
- `ANDROID_KEY_ALIAS`: `swiftcad`.
- `ANDROID_KEY_PASSWORD`: mật khẩu khóa.

Sao lưu tệp .jks sang nơi an toàn (ổ mã hóa, không gửi qua chat hay email).

## 3. Build AAB
Repo → Actions → "Android phat hanh (AAB ky khoa phat hanh)" → Run workflow. Tải artifact `swiftcad-android-aab`.

## 4. Đăng ký Google Play Console
- Tài khoản nhà phát triển (đề xuất loại tổ chức). Phí đăng ký một lần; kiểm tra mức phí hiện hành khi đăng ký.
- Tạo ứng dụng: tên Swift CAD Viewer Pro, mã `vn.siscom.swiftcad`.
