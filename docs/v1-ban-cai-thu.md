# Bản cài thử V1 (Issue #4)

Cập nhật 12/11/2026. Bản cài thử nội bộ, **chưa phát hành** lên Google Play hay App Store, không có quảng cáo.

## 1. Android: APK ký khóa debug

| Mục | Giá trị |
|---|---|
| Nguồn | `main` tại `9320f2e` (merge PR #2 `77ef567` và PR #3) |
| CI trên `main` | run 43157092896: Core, Android APK, iOS simulator đều đạt |
| Artifact | `swiftcad-android-apk` (id 13915734409), hết hạn 10/02/2027 |
| Phiên bản | 0.1.0 (build 1), mã ứng dụng tạm `com.swiftcad.app` |
| Ký | Khóa debug của .NET Android: chỉ để cài thử, Play không nhận |

Artifact của `9320f2e` chứa cả APK chưa ký và APK đã ký; **cài tệp có đuôi `-Signed.apk`**. Từ PR của Issue #4, CI chỉ đóng gói một tệp `SwiftCad-<phiên bản>-build<số>-<commit>-debugsigned.apk` kèm tệp `.sha256`.

Bản bàn giao là bản build thường, không có ShotMode (chế độ chụp ảnh chỉ có khi build với `-p:SwiftCadShots=true`).

**Cách cài**
1. Đăng nhập GitHub, mở trang run ở trên, mục Artifacts, tải `swiftcad-android-apk` (tệp zip), giải nén.
2. Chép tệp APK đã ký sang điện thoại Android 8.0 trở lên.
3. Mở tệp, cho phép "Cài ứng dụng không rõ nguồn" cho ứng dụng dùng để mở (Files hoặc trình duyệt), rồi cài.
4. Gỡ trước nếu máy đã có bản cài khác chữ ký.

## 2. iOS: thiết bị thật và TestFlight

- **Đã có:** build iOS simulator trong CI; ảnh chạy trên simulator iOS 26.5. Simulator **không** phải iPhone thật.
- **Đã chuẩn bị (PR của Issue #4):**
  - Job CI `ios-device`: build Release `ios-arm64` có AOT, không ký, để kiểm tra biên dịch và cảnh báo trimming cho iPhone thật.
  - Workflow `testflight.yml` chạy tay: nạp chứng chỉ vào keychain tạm, build IPA ký, tải lên TestFlight. Workflow dừng với lỗi rõ ràng nếu thiếu secret.
- **Chặn (cần Owner):**
  1. Tài khoản Apple Developer Program (99 USD/năm), chưa được duyệt trong nhật ký quyết định.
  2. Mã định danh ứng dụng chính thức. `com.swiftcad.app` chỉ là tạm và hàm ý sở hữu tên miền swiftcad.com. Đề xuất dùng tên miền Owner sở hữu (ví dụ dạng `vn.<tên-miền>.swiftcad`). Phải chốt trước khi tạo hồ sơ App Store Connect, vì sau đó không đổi được. Mã này cũng sẽ dùng cho Android.
  3. Sau khi có tài khoản: đăng ký App ID, tạo chứng chỉ Apple Distribution và hồ sơ App Store, tạo ứng dụng trong App Store Connect, tạo khóa API vai trò App Manager, thêm nhóm thử nghiệm nội bộ.
  4. Nhập 7 secret liệt kê trong `.github/workflows/testflight.yml` vào Settings → Secrets and variables → Actions. Không gửi khóa qua chat hay bình luận.
- Khi đủ, chạy tay workflow "iOS TestFlight noi bo" trên `main`.

## 3. Thử với bản vẽ MEP thật

Owner đã chốt kiểm thử bằng dữ liệu tạo sinh ở Giai đoạn 0; Issue #4 yêu cầu thêm bản vẽ MEP do Owner cung cấp. **Không đưa bản vẽ công trình lên repo public.** Kênh đề xuất: chia sẻ riêng (thư mục Google Drive riêng tư hoặc gửi trực tiếp trong phiên làm việc), kết quả ghi vào Issue #4 nhưng không kèm nội dung bản vẽ.

Bảng ghi kết quả cho mỗi tệp:

| Mục | Ghi |
|---|---|
| Tệp (tên ẩn danh), DWG/DXF, phiên bản AutoCAD, dung lượng | |
| Mở được / lỗi; thời gian từ "Đang đọc" đến khi hiện bản vẽ | |
| Số đối tượng chưa hiển thị (cảnh báo), xref thiếu, đơn vị | |
| Chữ tiếng Việt (Unicode / VNI / TCVN3 / SHX) | |
| Tìm chữ, đo một kích thước biết trước, bật/tắt layer, ghi chú | |
| Xoay máy, Vừa khung, mở lại từ Gần đây | |
| Máy, hệ điều hành, có bị tắt app không | |

## 4. Hiệu năng tệp lớn (máy Linux CI, không phải điện thoại)

Tệp tạo sinh 300.000 đối tượng (đường, polyline có cung, tròn, chữ, block), đọc trong tiến trình mới:

| Tệp | Dung lượng | Đọc + chuyển đổi | Đối tượng hiển thị | Đỉnh bộ nhớ tiến trình |
|---|---|---|---|---|
| DXF | 53,5 MB | 11,1 s | 360.000 | ~1.040 MB |
| DWG | 6,8 MB | 9,3 s | 360.000 | ~1.200 MB |

Đo trên container 1 nhân CPU khi viết tài liệu này; CI ghi lại số liệu mỗi lần chạy. Mô hình hình học giữ lại sau khi đọc khoảng 43 MB; phần lớn đỉnh bộ nhớ là mô hình đối tượng đầy đủ của ACadSharp trong lúc đọc. **Rủi ro thật trên điện thoại**: iOS/Android có thể tắt app với tệp cỡ này. Cần đo trên máy thật và tối ưu (giải phóng sớm, đọc theo phần, giới hạn/cảnh báo tệp quá lớn).

## 5. Giới hạn hiện tại

- CAD: chưa vẽ hatch, kích thước (dimension), spline, polyline 2D/3D, kiểu nét, độ dày nét, layout/viewport, định dạng trong MTEXT; chữ Aligned/Fit và MTEXT tự xuống dòng hiển thị gần đúng (có cảnh báo). Font SHX và bảng mã cũ VNI/TCVN3 chưa xử lý.
- Icon ứng dụng là **bản tạm**. Nhánh `design/approved-v2-assets-20261112` có vector gần đúng của icon đã duyệt, nhưng tài liệu thiết kế ghi rõ đó không phải bản gốc và không được thay làm bản chính trước khi so sánh; cần tệp PNG gốc đã duyệt.
- Giao diện chưa được nghiệm thu so sánh với màn hình thiết kế đã duyệt (tiêu chí 1 trong tài liệu bàn giao thiết kế).
- Đăng nhập là Demo; Google Drive/Dropbox chỉ qua trình chọn tệp hệ thống.
- Chưa chạy trên máy Android thật hay iPhone thật; chưa thử TalkBack/VoiceOver.
