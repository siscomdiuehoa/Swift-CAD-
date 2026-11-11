# [Draft] Nền dự án và luồng dùng được theo thiết kế Neumorphism V2

Liên kết #1. Nhánh `feat/v1-foundation` → `main`. Chờ Codex review, Owner duyệt merge. Không tự merge, không phát hành.

## Tóm tắt

Dựng mới hoàn toàn theo đặc tả trong Issue #1: lõi `SwiftCad.Core` (đã biên dịch và kiểm thử), ứng dụng MAUI `SwiftCad.App` (CHƯA biên dịch bằng MAUI, xem bảng trạng thái), tài nguyên dịch Anh–Việt, CI.

## Trạng thái theo nền tảng

| Hạng mục | Trạng thái |
|---|---|
| `SwiftCad.Core` build (.NET SDK 10.0.115, Linux) | Đạt |
| Kiểm thử Core và tài nguyên dịch | Đạt 33/33 |
| Mã `SwiftCad.App` kiểm tra kiểu | Đạt với stub API tự viết (không phải MAUI thật). Chữ ký hàm SkiaSharp 4.155.0 và MAUI 10.0.130 đã đối chiếu với mã nguồn hai thư viện. |
| Build Android | CHƯA chạy. Môi trường viết mã không truy cập được NuGet. Chờ CI. |
| Build iOS simulator | CHƯA chạy. Chờ CI trên máy macOS. |
| Chạy trên emulator / simulator | CHƯA chạy |
| Chạy trên máy thật | CHƯA chạy |
| Ảnh chạy thật (360 và 390, Anh/Việt, font lớn) | CHƯA có. Workflow `screenshots.yml` đã viết nhưng chưa chạy thử lần nào. |

Lần CI đầu rất có thể lộ lỗi biên dịch MAUI. Việc đầu tiên sau khi đẩy nhánh là sửa cho CI xanh, rồi mới chụp ảnh.

## Cách chạy

Xem `README.md`. Không có máy build: tab Actions → workflow `CI` → Artifacts → `swiftcad-android-apk`.

## Đã làm theo đặc tả

- **Hệ thống giao diện**: màu, khoảng cách, góc, cỡ chữ đúng số trong đặc tả, gom ở `Theme`. Thành phần `SoftSurface`, `PrimaryButton`, `SourceTile`, `DrawingRow`, `ToolButton`, `SearchField`. Trạng thái chọn dùng nền xanh nhạt, viền, chữ đậm và dấu kiểm.
- **Chào lần đầu**: chọn ngôn ngữ, Dùng không cần tài khoản, Đăng nhập (Demo). Lựa chọn được lưu.
- **Bản vẽ**: header, ô tìm lọc Gần đây, nút Chọn tệp mở trình chọn hệ thống, hai thẻ Google Drive và Dropbox với trạng thái Kết nối, Gần đây có Xem tất cả và menu từng tệp, Tệp mẫu để riêng có nhãn Demo, thanh dưới Bản vẽ / Đã lưu / Cài đặt.
- **Xem CAD**: thanh trên, vùng CAD tối, Vừa khung và Toàn màn hình ở góc dưới phải, thanh công cụ Tìm / Đo / Layer / Ghi chú luôn có nhãn, bảng công cụ thu gọn, thấp hơn khi xoay ngang.
- **Cài đặt**: tài khoản mẫu, ngôn ngữ (Theo thiết bị / English / Tiếng Việt), đơn vị hiển thị, nền bản vẽ, giấy phép mã nguồn mở.
- **Đa ngôn ngữ**: 112 chuỗi trong `.resx` cho mỗi ngôn ngữ, gồm thông báo lỗi và nhãn trợ năng.

## Hành vi thật

- Kéo, zoom hai ngón quanh điểm chụm, vừa khung, giữ camera khi xoay máy và khi mở/đóng bảng công cụ.
- Tìm chữ không dấu và không phân biệt hoa thường, đếm kết quả, chuyển trước/sau, đưa camera đến kết quả.
- Đo hai điểm từ tọa độ bản vẽ, bắt điểm đầu mút / trung điểm / tâm có ký hiệu riêng, tâm ngắm lệch lên trên ngón tay, quy đổi mm / m / in, định dạng số theo vùng.
- Layer: tìm tên, bật/tắt, hiện tất cả. Kết quả tìm và bắt điểm bỏ qua layer đã tắt.
- Ghi chú: ghim theo tọa độ bản vẽ, lưu tệp JSON riêng theo khóa nội dung tệp, xóa được.
- Chọn tệp bằng trình chọn hệ thống, kiểm tra đuôi .dwg/.dxf, sao chép vào máy, ghi Gần đây. Hủy thì không đổi gì.
- Gần đây: lọc, đánh dấu Đã lưu, gỡ, chia sẻ bản sao, Chọn lại khi bản sao mất.
- Đổi ngôn ngữ áp dụng ngay và lưu qua lần mở lại.

## Đang mô phỏng hoặc chưa làm

- **Đọc DWG/DXF thật: chưa làm.** Tệp người dùng chọn được sao chép và ghi vào Gần đây, nhưng màn hình xem chỉ báo "Chưa hiển thị nội dung bản vẽ". Không vẽ hình minh họa thay thế.
- **Bản vẽ mẫu** là hình học do ứng dụng tự tạo (`DemoDrawing`), không đọc từ tệp. Có nhãn Demo và mục "Về bản Demo này".
- **Đăng nhập**: danh tính mẫu, không có máy chủ.
- **Google Drive / Dropbox**: chỉ hướng dẫn rồi mở trình chọn tệp hệ thống. Không OAuth, không trạng thái đã kết nối.
- **Thumbnail**: dùng biểu tượng loại tệp.
- Chưa có: trạng thái thiếu font / thiếu xref / đối tượng bỏ qua (phụ thuộc bước đọc CAD thật), bắt điểm giao nhau, xuất PDF.

## Chỗ khác với thiết kế, cần Codex và Owner xem

1. **Biểu tượng ứng dụng** là bản tạm tự vẽ. Chưa có tệp ảnh đã duyệt (`docs/design/swift-cad-approved.png` chưa tồn tại, link ChatGPT cần tài khoản Owner).
2. **Logo Google Drive và Dropbox** không dùng. Thẻ hiện tên dịch vụ và biểu tượng đám mây trung tính, vì logo là nhãn hiệu của bên khác và Owner yêu cầu không dùng tài nguyên dính bản quyền.
3. **Màn hình Chọn tệp** trong ảnh thiết kế không được dựng riêng. Dùng trình chọn tệp hệ thống đúng như phần chữ của đặc tả.
4. **Bóng**: một bóng mềm cộng viền sáng, theo phương án dự phòng trong đặc tả.
5. **Mã định danh ứng dụng** `com.swiftcad.app` là tạm. Owner cần chốt trước khi phát hành vì sau đó không đổi được.
6. Ở chế độ Đo, kéo một ngón dùng để ngắm điểm, nên di chuyển bản vẽ phải dùng hai ngón.

## Kiểm tra đã thực hiện

`dotnet run --project tests/SwiftCad.Core.Tests`: 33 đạt, 0 lỗi. Gồm quy đổi đơn vị, đo 12000 mm và đường chéo 5000 mm biết trước, camera (vừa khung, zoom quanh điểm, xoay máy, tọa độ 1e7), bắt điểm, tìm chữ tiếng Việt, layer, ngôn ngữ dự phòng, lưu cài đặt, Gần đây, ghi chú không đụng tệp gốc, khớp khóa hai ngôn ngữ.

## Kiểm tra CHƯA thực hiện (cần bản chạy được)

Bấm nguồn, hủy chọn tệp, mở lại từ Gần đây, đổi ngôn ngữ trên màn hình nhỏ, chữ không bị cắt ở 360 và font lớn, safe area, xoay màn hình, TalkBack / VoiceOver.

## Chi phí

Repo công khai nên GitHub Actions với máy chạy tiêu chuẩn theo chính sách em biết là miễn phí, kể cả macOS. Cần đối chiếu lại trang giá hiện hành. Chưa bật khoản trả phí nào.
