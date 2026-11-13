# [Draft] Mở tệp CAD từ Zalo, danh sách Mới tải về, ẩn cảnh báo vàng, ghi chú chữ/mũi tên/khoanh vùng

Nhánh `feat/mo-tep-cad-ghi-chu` → `feat/issue-6-hvac-blocks` (PR xếp chồng trên #7). Không tự merge.

Theo 5 yêu cầu của Owner ngày 13/11/2026 (kèm ảnh chụp trình chọn tệp, màn hình xem, app đối thủ và mục Mở bằng trong Zalo).

## Thay đổi

1. **Trình chọn tệp chỉ hiện tệp AutoCAD.** Android lọc theo các kiểu MIME của DWG/DXF (`image/vnd.dwg`, `application/acad`, `image/vnd.dxf`…) kèm `application/octet-stream` cho máy chưa biết đuôi `.dwg`. PDF, Word, ảnh, APK bị ẩn. iOS dùng kiểu tài liệu `com.autodesk.dwg`/`com.autodesk.dxf` khai báo trong Info.plist. Có liên kết dự phòng "Không thấy tệp? Hiện mọi loại tệp"; tệp vẫn được kiểm tra nội dung trước khi mở.
2. **Mục "Mới tải về" trên màn hình Bản vẽ (Android).** Người dùng cho phép một lần thư mục Tải xuống › Zalo (trình chọn thư mục hệ thống, mở sẵn đúng thư mục). App liệt kê tệp DWG/DXF trong thư mục đó và thư mục con, **mới nhất ở đầu**, chỉ tệp CAD. Thêm/bỏ thư mục qua nút "Thư mục". Không xin quyền đọc toàn bộ bộ nhớ (Android 11+ không cho chọn thẳng thư mục gốc Tải xuống, nên hướng dẫn chọn thư mục con Zalo). Bấm vào tệp: sao chép vào app rồi mở, tệp gốc không đổi.
   - Lưu ý: trình chọn tệp của hệ thống (màn hình "Tệp đã tải xuống" của Android) tự sắp xếp, app không đổi được thứ tự trong đó. Thứ tự "mới nhất đầu tiên" nằm ở mục Mới tải về của app.
3. **Ẩn dòng cảnh báo vàng** đè lên bản vẽ. Nội dung (đối tượng chưa hiển thị, hiển thị gần đúng, xref thiếu, đơn vị chưa rõ) chuyển vào menu ⋮ › "Mức độ hiển thị". Bảng Đo vẫn báo khi bản vẽ chưa ghi đơn vị.
4. **Ghi chú trên bản vẽ**: ba chế độ trong bảng Ghi chú
   - Chữ: chạm vào bản vẽ, nhập chữ; chữ hiện ngay trên bản vẽ (nhãn nền tối viền đỏ, cỡ cố định trên màn hình).
   - Mũi tên: kéo một ngón từ chỗ đặt chữ tới điểm cần chỉ; nhập chữ (có thể bỏ trống).
   - Khoanh vùng: kéo một ngón chéo qua vùng; vẽ khung dạng đám mây (revision cloud); chữ tùy chọn.
   - Hai ngón vẫn di chuyển, thu phóng khi đang ở chế độ vẽ. Danh sách ghi chú có biểu tượng theo loại, chạm để tới vị trí, xóa từng cái.
   - Lưu riêng theo khóa nội dung tệp như trước; tệp ghi chú cũ (ghim đánh số) vẫn đọc và hiển thị được.
5. **Swift CAD hiện trong mục "Mở bằng"** khi bấm tệp DWG/DXF (Zalo gửi kiểu `image/vnd.dwg`, nên trước đây chỉ thấy Bộ sưu tập, Photos). Android: intent filter `VIEW`/`SEND` cho các kiểu MIME CAD; activity chuyển sang `SingleTask` để mở trong cửa sổ Swift CAD đang chạy. iOS: `CFBundleDocumentTypes` + nhận URL qua scene. Tệp không phải CAD (kể cả gửi với kiểu chung) bị từ chối với thông báo rõ ràng. Mở thẳng từ ứng dụng khác thì bỏ qua màn hình chào.

Nhận diện nội dung (`SwiftCad.Core/CadFiles.cs`): DWG theo mã phiên bản `AC10xx`/`AC1.xx`/`AC2.xx`, DXF chữ (nhóm 0 + SECTION, cho phép BOM và dòng 999), DXF nhị phân. Tệp nhận về không có đuôi được thêm `.dwg`/`.dxf` theo nội dung.

## Kiểm thử

- `tests/SwiftCad.Core.Tests`: thêm kiểm thử nhận diện DWG/DXF theo nội dung (kể cả PDF, ZIP, tệp rỗng bị từ chối), đặt tên khi thiếu đuôi, sắp xếp Mới tải về (chỉ CAD, mới nhất đầu, không trùng), ghi chú chữ/mũi tên/khoanh vùng (hai điểm, chữ tùy chọn, không ghi thuộc tính tính toán vào JSON, đọc được tệp ghi chú cũ).
- `tools/ci/flow.py` (Android emulator): trình chọn tệp không hiện `TEST-Not-CAD.pdf` đặt cạnh tệp DXF; không còn cảnh báo đè trên bản vẽ, có trong menu › Display details; bảng Ghi chú; `cmd package query-activities` xác nhận Swift CAD có trong Mở bằng cho `image/vnd.dwg`, `image/vnd.dxf`, `application/acad`, `application/octet-stream` và **không** có cho PDF.
- `tools/ci/shots.py`: ảnh `viewer-notes` giờ có đủ chữ, mũi tên, khoanh vùng.

**Chưa biên dịch tại chỗ**: phiên viết mã không có .NET SDK và không vào được NuGet. Đã kiểm tra cú pháp (tree-sitter) và đối chiếu khóa tài nguyên EN/VI, chữ tiếng Việt viết cứng bằng script. Chỉ ghi "build đạt" khi các job CI `core`, `android`, `ios-simulator` xanh. Điểm dễ lỗi: tên API sự kiện vòng đời iOS (`SceneWillConnect`, `SceneOpenUrl`), API Storage Access Framework trong binding .NET Android.

## Chưa làm

- Kiểm thử luồng chọn thư mục Zalo trên emulator (trình chọn thư mục hệ thống khác nhau theo hãng máy); cần thử trên máy thật của Owner.
- Ghi âm, ảnh, video trong ghi chú; sửa/di chuyển ghi chú đã đặt; xuất ghi chú ra PDF/ảnh.
- iOS: chưa có danh sách Mới tải về (iOS không có thư mục Tải xuống dùng chung); tệp đến qua Mở bằng.
