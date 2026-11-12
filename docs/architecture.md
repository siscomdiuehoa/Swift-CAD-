# Kiến trúc Swift CAD (ghi chú quyết định)

Cập nhật: 11/11/2026. Áp dụng cho PR `feat/v1-foundation` (Issue #1).

## Phiên bản đã ghim

| Thành phần | Phiên bản | Lý do |
|---|---|---|
| .NET SDK | 10.0.x (`global.json`, rollForward latestFeature) | .NET 10 là bản hỗ trợ dài hạn. .NET 11 vừa phát hành, chưa chọn. |
| .NET MAUI | 10.0.130 | Bản vá mới nhất của nhánh 10 trên trang phát hành `dotnet/maui` ngày 11/11/2026. |
| SkiaSharp.Views.Maui.Controls | 4.155.0 | Bản ổn định mới nhất; mã nguồn bản này khai báo hỗ trợ `net10.0`. |
| Android tối thiểu | API 26 (Android 8.0) | Theo đề xuất Giai đoạn 0. |
| iOS tối thiểu | 15.0 | Theo mẫu dự án chính thức của MAUI 10. |

Chưa xác minh: số phiên bản gói trên NuGet (môi trường viết mã không truy cập được NuGet), phiên bản workload mà CI cài, và ảnh máy macOS/Xcode phù hợp. Ghi lại sau lần CI đầu tiên.

## Phân lớp

```
SwiftCad.App  (MAUI: trang, thành phần giao diện, CadCanvas, tài nguyên dịch, dịch vụ tệp)
      |
SwiftCad.Core (DrawingModel, ViewTransform, TextSearch, SnapEngine, Units, LayerState,
               NoteStore, RecentStore, SettingsStore, LanguageResolver)
```

- **Mô hình hình học trung gian** (`DrawingModel`): đường, polyline, tròn, cung, chữ, layer, đơn vị. Bộ vẽ, tìm, đo chỉ biết mô hình này.
- **Thư viện đọc CAD nằm sau adapter**: dự án `SwiftCad.Cad` (ACadSharp → `DrawingModel`) CHƯA tạo trong PR này vì chưa có việc dùng. Bản vẽ mẫu do `DemoDrawing` sinh ra, gắn cờ `IsDemo`.
- **Camera** (`ViewTransform`): mọi phép đổi tọa độ, zoom quanh điểm, giữ tâm khi xoay máy nằm ở Core và có kiểm thử.
- **Đo**: tính từ tọa độ bản vẽ kiểu `double`. Đơn vị nguồn chưa biết thì không quy đổi, yêu cầu người dùng xác nhận.
- **Lưu trữ**: tệp JSON trong vùng dữ liệu của ứng dụng, dùng `System.Text.Json` sinh mã lúc biên dịch (không reflection, phù hợp AOT). Ghi qua tệp tạm rồi thay thế.
- **Ghi chú**: lưu theo khóa SHA-256 của nội dung tệp, nên gắn đúng phiên bản tệp và không bao giờ ghi vào tệp gốc.
- **Tệp của người dùng**: chọn qua trình chọn tệp hệ thống, sao chép vào vùng dữ liệu của ứng dụng. Mở lại từ Gần đây dùng bản sao này, nên không phụ thuộc quyền truy cập nguồn. Bản sao mất thì đề nghị Chọn lại.

## Giao diện

- Viết bằng C#, không XAML, để mọi thành phần đi qua trình biên dịch C#.
- Màu, kích thước, bóng nằm ở `Theme`. Thành phần dùng chung: `SoftSurface`, `PrimaryButton`, `SourceTile`, `DrawingRow`, `ToolButton`, `SearchField`, `OptionRow`.
- Bóng: MAUI chỉ hỗ trợ một bóng cho mỗi phần tử, nên dùng viền sáng cộng một bóng mềm phía dưới phải, đúng phương án dự phòng trong đặc tả.
- Biểu tượng: nét tự vẽ trên lưới 24 (`Icons`). Không dùng bộ biểu tượng hay logo bên ngoài.
- Font: font hệ thống. Không khóa chiều cao phần tử có chữ, chỉ đặt chiều cao tối thiểu.

## Đa ngôn ngữ

- Chuỗi giao diện, thông báo lỗi, nhãn trợ năng: `Resources/Localization/AppResources.resx` (tiếng Anh, mặc định) và `AppResources.vi.resx`.
- Thêm ngôn ngữ: thêm `AppResources.<mã>.resx`, thêm mã vào `LanguageResolver.Supported`, thêm một dòng lựa chọn trong Cài đặt.
- Ngôn ngữ thiết bị chưa hỗ trợ thì dùng tiếng Anh. Số và ngày định dạng theo vùng của thiết bị khi cùng ngôn ngữ với giao diện.
- Không dịch: tên tệp, chữ và tên layer trong bản vẽ, ký hiệu đơn vị, tên riêng (Swift CAD, Google Drive, Dropbox), tên ngôn ngữ.
- Kiểm thử tự động bảo đảm hai tệp cùng bộ khóa, mọi khóa trong mã đều tồn tại, và mã giao diện không chứa chữ tiếng Việt viết cứng.

## Việc sẽ xem lại

- Tạo `SwiftCad.Cad` và kiểm chứng ACadSharp trên thiết bị (AOT iOS, bộ nhớ).
- Chuyển `DrawingModel` sang buffer phẳng khi đo được hiệu năng với tệp lớn.
- Chỉ mục không gian cho bắt điểm khi số đối tượng lớn (hiện duyệt tuyến tính).
