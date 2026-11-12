# Thư viện và tài nguyên bên thứ ba — Swift CAD

Cập nhật: 11/11/2026 · Người lập: Claude · Trạng thái: Owner đã chốt nguyên tắc ngày 11/11/2026

## 1. Nguyên tắc của Owner

Chỉ dùng repo và tài nguyên **miễn phí, dùng thương mại được, không vướng giấy phép**.

Cách áp dụng trong dự án:

- Thư viện do dự án **tự chọn** phải là giấy phép **MIT**.
- Không dùng GPL, LGPL, AGPL, giấy phép thương mại, giấy phép "chỉ phi thương mại", hay dịch vụ tính phí.
- Thành phần miễn phí, dùng thương mại được, không phải mua bản quyền (BSD, Apache-2.0) thì **được dùng** khi cần. Owner chốt ngày 11/11/2026: "cái nào cho dùng không cần bản quyền thì cứ sử dụng, cái nào dính đến bản quyền thì không thử".
- Thành phần phải mua bản quyền, hoặc có điều kiện ràng buộc mã nguồn của ứng dụng, thì **không dùng và không thử nghiệm**.
- Mỗi thư viện mới phải được thêm vào tệp này và được Owner duyệt trước khi đưa vào code.

**Lưu ý về MIT:** MIT miễn phí và cho dùng thương mại, nhưng có một nghĩa vụ duy nhất là giữ nguyên dòng bản quyền và nội dung giấy phép khi phân phối. Dự án đáp ứng bằng màn hình "Giấy phép mã nguồn mở" trong Cài đặt và tệp `THIRD-PARTY-NOTICES.md` trong repo.

## 2. Phương pháp kiểm tra

Ngày 11/11/2026, mở trực tiếp tệp LICENSE ở gốc từng repo trên GitHub và xem ngày commit gần nhất. Chưa rà toàn bộ thư viện phụ thuộc gián tiếp của từng gói; các điểm đã phát hiện ghi ở mục 4 và mục 7.

## 3. Danh mục được đề xuất dùng (MIT, đã xác minh)

### 3.1 Đưa vào ứng dụng ở bản đầu

| Thành phần | Repo | Vai trò | Commit gần nhất |
|---|---|---|---|
| ACadSharp 3.8.35 | `DomCR/ACadSharp` | Đọc DWG/DXF, trong dự án `SwiftCad.Cad` (nhánh `feat/cad-reader-acadsharp`) | 28/10/2026 (bản 3.8.35) |
| CSUtilities, CSMath | `DomCR/CSUtilities` | Mã nguồn được biên dịch chung vào gói ACadSharp (shared project), không phải gói riêng | MIT, đã mở LICENSE |
| .NET runtime | `dotnet/runtime` | Nền chạy, gồm System.Text.Json và tài nguyên dịch `.resx` | — |
| .NET MAUI 10.0.130 | `dotnet/maui` | Khung ứng dụng Android/iOS | 11/11/2026 |
| SkiaSharp 4.155.0 | `mono/SkiaSharp` | Vẽ bản vẽ 2D (xem ngoại lệ ở mục 4) | 10/11/2026 |

### 3.2 Chỉ dùng khi phát triển, không đóng gói vào ứng dụng

| Thành phần | Repo | Vai trò |
|---|---|---|
| NUnit | `nunit/nunit` | Kiểm thử tự động. Hiện chưa dùng: PR đầu dùng bộ chạy kiểm thử tự viết, không gói ngoài. |
| IxMilia.Dxf | `ixmilia/dxf` | Đối chiếu kết quả đọc DXF với ACadSharp |

### 3.3 Để dành cho giai đoạn sau (MIT, chưa đưa vào)

| Thành phần | Repo | Vai trò | Ghi chú |
|---|---|---|---|
| CommunityToolkit.Maui | `CommunityToolkit/Maui` | Thành phần giao diện bổ sung | Chỉ thêm khi thật sự cần |
| CommunityToolkit.Mvvm | `CommunityToolkit/dotnet` | MVVM | Chỉ thêm khi thật sự cần |
| RBush | `viceroypenguin/RBush` | Chỉ mục không gian cho bắt điểm | Commit cuối 11/2024 |
| LiteDB | `litedb-org/LiteDB` | Cơ sở dữ liệu thuần .NET | Khi tệp JSON không còn đủ |
| InAppBillingPlugin | `jamesmontemagno/InAppBillingPlugin` | Mua trong ứng dụng | Khi bật thu phí |
| Svg.Skia | `wieslawsoltes/Svg.Skia` | Vẽ SVG | Chưa rà phụ thuộc gián tiếp |
| Fluent UI System Icons | `microsoft/fluentui-system-icons` | Bộ biểu tượng | Chưa dùng, hiện tự vẽ |
| Tabler Icons | `tabler/tabler-icons` | Bộ biểu tượng thay thế | Chưa dùng |

### 3.4 Chỉ tham khảo mã, không dùng làm phụ thuộc

- `Mapsui/Mapsui` (MIT): mẫu về kéo, zoom, vẽ theo ô bằng SkiaSharp.
- `ixmilia/dwg` (MIT): ít hoạt động, chưa kiểm tra phạm vi phiên bản DWG.
- `haplokuon/netDxf` (MIT): chỉ DXF, ngừng cập nhật từ 10/2023.

## 4. Thành phần miễn phí không phải MIT (Owner đã chấp nhận)

Các thành phần dưới đây **không phải MIT** nhưng đi kèm nền tảng. Tất cả miễn phí và dùng thương mại được.

| Thành phần | Giấy phép | Vì sao có mặt | Phương án thay thế |
|---|---|---|---|
| Lõi Skia (bên trong SkiaSharp) | BSD 3 điều khoản (đã xác minh tại `google/skia`) | SkiaSharp là lớp bọc MIT quanh lõi Skia | Dùng `Microsoft.Maui.Graphics` (MIT, trong `dotnet/maui`). Hiệu năng với bản vẽ lớn chưa được kiểm chứng. |
| Thư viện AndroidX của Google | Apache-2.0 (theo hiểu biết chung, chưa mở tệp LICENSE trong phiên này) | MAUI trên Android phụ thuộc vào chúng | Không có. Gần như mọi ứng dụng Android đều dùng. |
| Android SDK, Xcode và SDK của Apple | Điều khoản riêng của Google và Apple, miễn phí | Công cụ build, không phân phối lại | Không có. |

Nghĩa vụ của BSD và Apache-2.0 tương tự MIT: giữ thông báo bản quyền. Dự án gộp chung vào màn hình "Giấy phép mã nguồn mở".

## 5. Tài nguyên không phải mã nguồn

| Tài nguyên | Lựa chọn | Lý do |
|---|---|---|
| Font chữ giao diện và chữ trong bản vẽ | Font hệ thống của thiết bị, không đóng gói font | Android và iOS đều có sẵn font hỗ trợ tiếng Việt. Không phát sinh giấy phép font. |
| Font SHX của AutoCAD | Không đóng gói, thay bằng font hệ thống và hiện cảnh báo | Font SHX đi kèm AutoCAD thuộc bản quyền Autodesk. |
| Biểu tượng | Tự vẽ (nét trên lưới 24) | Thuộc sở hữu dự án. |
| Logo Google Drive, Dropbox | Không dùng. Hiện tên dịch vụ kèm biểu tượng đám mây trung tính | Logo là nhãn hiệu của bên khác, cần tuân thủ hướng dẫn thương hiệu của họ. Chờ Owner quyết. |
| Biểu tượng ứng dụng | Bản tạm do dự án tự vẽ | Thay bằng biểu tượng xanh gấp hình đã duyệt khi có tệp gốc. |
| Bản vẽ mẫu | Tự tạo bằng script trong `samples/` | Thuộc sở hữu dự án, không chứa dữ liệu khách hàng. |
| Lưu Ghi chú, Gần đây, ngôn ngữ | Tệp JSON trong vùng dữ liệu của ứng dụng | Dùng System.Text.Json có sẵn, không thêm thư viện. |
| Bản dịch Anh–Việt | Tự viết, lưu trong `.resx` | Thuộc sở hữu dự án. |

## 6. Danh sách loại trừ

| Thành phần | Giấy phép | Lý do loại |
|---|---|---|
| libredwg | GPLv3 | Buộc mở mã nguồn toàn bộ ứng dụng |
| ODA Drawings SDK, Aspose.CAD | Thương mại | Tính phí |
| `praeclarum/sqlite-net` | MIT, nhưng phụ thuộc SQLitePCLRaw (Apache-2.0) | Kéo theo phụ thuộc không phải MIT. Thay bằng tệp JSON. |
| `toptensoftware/RichTextKit` | Apache-2.0 | Không phải MIT |
| `SixLabors/Fonts` | Apache-2.0 | Không phải MIT |
| `AngusJohnson/Clipper2` | Boost | Không phải MIT |
| `speps/LibTessDotNet` | SGI Free Software License B | Không phải MIT, ngừng từ 2021 |
| Noto Sans và các font đóng gói | OFL | Không phải MIT. Dùng font hệ thống thay thế. |

## 7. Điểm chưa xác minh

1. **CSMath:** đã xác minh 12/11/2026: CSMath nằm trong repo `DomCR/CSUtilities` (thư mục `CSMath`), giấy phép MIT, được biên dịch chung vào ACadSharp.
2. **Phụ thuộc gián tiếp:** chưa chạy công cụ liệt kê toàn bộ gói NuGet mà ứng dụng kéo theo. Cần làm khi app build được, và làm lại trước mỗi lần phát hành.
3. **HarfBuzz:** nếu dùng HarfBuzzSharp để dựng chữ có dấu thì lõi HarfBuzz có giấy phép riêng, chưa kiểm tra. Bản đầu chưa đưa vào.
4. **AndroidX:** giấy phép ghi ở mục 4 chưa được mở tệp để xác nhận trong phiên này.

## 8. Chi phí không tránh được khi phát hành

Không liên quan giấy phép mã nguồn nhưng không miễn phí:

- **Tài khoản Apple Developer:** 99 USD/năm, bắt buộc để đưa app lên App Store và thử trên iPhone thật. Owner chưa duyệt.
- **Tài khoản Google Play Console:** phí đăng ký một lần, cần đối chiếu mức phí hiện hành.
- **GitHub Actions:** repo đang công khai nên theo chính sách GitHub mà em biết là miễn phí với máy chạy tiêu chuẩn. Nếu chuyển repo sang riêng tư thì có hạn mức, và máy macOS tính hệ số cao hơn. Cần đối chiếu chính sách hiện hành.

## 9. Công cụ CI (không đóng gói vào ứng dụng)

| Thành phần | Giấy phép | Ghi chú |
|---|---|---|
| `actions/checkout`, `actions/setup-dotnet`, `actions/setup-java`, `actions/upload-artifact` | MIT (theo hiểu biết chung, chưa mở tệp LICENSE trong phiên này) | Hành động chính thức của GitHub |
| `reactivecircus/android-emulator-runner` | Apache-2.0 (theo hiểu biết chung, chưa xác minh) | Chỉ dùng trong workflow chụp ảnh chạy tay |

## 10. Quyết định của Owner

1. Dùng các thành phần miễn phí, không phải mua bản quyền, gồm lõi Skia (BSD) và AndroidX (Apache-2.0). Không cần thử phương án `Microsoft.Maui.Graphics`.
2. Không dùng và không thử các thành phần ở mục 6 thuộc loại GPL, thương mại, hoặc tài sản có bản quyền của bên khác (font SHX của Autodesk).
3. Dự án vẫn ưu tiên MIT cho thư viện tự chọn. Thư viện Apache-2.0, BSD, Boost ở mục 6 không bị cấm, nhưng chỉ thêm khi có nhu cầu cụ thể và sau khi ghi vào tệp này.
