# Swift CAD

Ứng dụng iOS/Android giúp kỹ sư mở, xem, tìm, đo và ghi chú bản vẽ DWG/DXF tại công trường. Không quảng cáo. Tệp trên thiết bị mở được mà không cần tài khoản.

Trạng thái: đang phát triển, chưa phát hành. Đặc tả và phân công nằm ở Issue #1.

## Cấu trúc

- `src/SwiftCad.Core`: mô hình hình học, camera, tìm, đo, đơn vị, layer, ghi chú, gần đây, ngôn ngữ. Không phụ thuộc UI, không dùng gói ngoài.
- `src/SwiftCad.App`: ứng dụng .NET MAUI (Android, iOS), vẽ bằng SkiaSharp.
- `tests/SwiftCad.Core.Tests`: kiểm thử Core và tài nguyên dịch.
- `tools/typecheck`: kiểm tra kiểu mã App bằng stub API khi không có workload MAUI (không thay cho build thật).
- `tools/ci`: script chụp ảnh trên emulator.
- `docs`: kiến trúc, danh mục thư viện, mô tả PR.

## Cách chạy

Cần .NET SDK 10 (xem `global.json`).

```
# Kiểm thử Core (Linux, macOS, Windows; không cần workload MAUI)
dotnet run --project tests/SwiftCad.Core.Tests

# Android (cần workload và Android SDK)
dotnet workload install maui-android
dotnet build src/SwiftCad.App -f net10.0-android -t:Run

# iOS simulator (chỉ trên macOS có Xcode)
dotnet workload install maui-ios
dotnet build src/SwiftCad.App -f net10.0-ios -t:Run
```

Không có máy build: mở tab Actions trên GitHub, workflow `CI` tạo tệp APK thử trong mục Artifacts.

## Giấy phép bên thứ ba

Xem `THIRD-PARTY-NOTICES.md` và `docs/thu-vien-ben-thu-ba.md`.
