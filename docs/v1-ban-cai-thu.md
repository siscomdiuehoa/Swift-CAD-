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
  - Workflow `testflight.yml`: ký tự động, build IPA, tải lên TestFlight (chi tiết dưới). Dừng với lỗi rõ ràng nếu thiếu secret.
- **Ký tự động, không cần máy Mac:** workflow `testflight.yml` dùng khóa App Store Connect API để tạo chứng chỉ Apple Distribution và hồ sơ App Store **tạm thời** ngay trên máy CI (khóa riêng sinh và nằm trên máy CI), build IPA, tải lên TestFlight, rồi xóa chứng chỉ và hồ sơ tạm. Thu hồi chứng chỉ phân phối không ảnh hưởng bản đã tải lên.
- **Owner cần làm (một lần):**
  1. Chốt mã định danh ứng dụng (thay `com.swiftcad.app` tạm). Claude cập nhật vào `SwiftCad.App.csproj`.
  2. developer.apple.com → Certificates, Identifiers & Profiles → Identifiers → đăng ký App ID với đúng mã đó (hoặc để workflow tự đăng ký ở lần chạy đầu).
  3. appstoreconnect.apple.com → Apps → + → New App: nền tảng iOS, tên (phải chưa ai dùng trên App Store, ví dụ "Swift CAD Viewer"; chỉ dùng nội bộ, đổi được sau), ngôn ngữ chính, chọn đúng Bundle ID, SKU tùy ý.
  4. Users and Access → Integrations → App Store Connect API → tạo khóa vai trò **Admin** (cần để tạo chứng chỉ). Ghi Issuer ID, Key ID, tải tệp `.p8` (chỉ tải được một lần).
  5. GitHub → repo → Settings → Secrets and variables → Actions → New repository secret: `ASC_KEY_ID`, `ASC_ISSUER_ID`, `ASC_KEY_P8` (dán nguyên nội dung tệp .p8). Không gửi qua chat hay bình luận.
  6. TestFlight trong App Store Connect: thêm chính mình vào nhóm thử nghiệm nội bộ; cài ứng dụng TestFlight trên iPhone.
- **Chạy:** Claude đẩy nhánh `testflight/<tên>` từ commit cần thử; workflow tự chạy. Sau khi Apple xử lý (thường 5–30 phút), bản thử hiện trong ứng dụng TestFlight.
- Khóa API có thể thu hồi bất cứ lúc nào trong App Store Connect.

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
