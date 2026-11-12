# [Draft] Đọc DWG/DXF thật bằng ACadSharp (bước 1)

Liên kết #1. Nhánh `feat/cad-reader-acadsharp` → `feat/v1-foundation` (PR xếp chồng, tách khỏi PR nền tảng #2). Chờ #2 được duyệt trước; không tự merge.

## Phạm vi

- Dự án mới `SwiftCad.Cad`: adapter ACadSharp 3.8.35 → `DrawingModel`. Chỉ lớp này biết ACadSharp.
- Hỗ trợ: LINE, LWPOLYLINE (bulge xấp xỉ ≤10°/đoạn), CIRCLE, ARC, ELLIPSE (gồm đường tròn trong block, do ACadSharp đổi thành ellipse khi tách block), TEXT (góc xoay, căn trái/giữa/phải × chân chữ/dưới/giữa/trên, hệ số rộng), MTEXT (chữ thuần, tách dòng, góc xoay, 9 điểm gắn, giãn dòng), INSERT (mở rộng block lồng nhau tối đa 16 cấp, chữ trong block giữ góc và điểm căn), giá trị thuộc tính block (ATTRIB; khuôn ATTDEF không hiển thị).
- Hiển thị gần đúng và được đếm để cảnh báo cụ thể: TEXT Aligned/Fit (vẽ trái-chân chữ theo hướng hai điểm, không co giãn), MTEXT có dòng dài hơn khung (AutoCAD tự xuống dòng, app chưa). Giải mã `\U+XXXX` của DXF cũ. Đơn vị từ `$INSUNITS`. Layer tắt/đóng băng trong tệp thì ẩn khi mở.
- Đối tượng chưa hỗ trợ (Hatch, Dimension, Spline, Polyline 2D/3D…) được đếm và báo trên màn hình xem: "Hiển thị đơn giản hóa: N đối tượng chưa hiển thị". Xref thiếu và đơn vị chưa rõ cũng được báo.
- App: chọn tệp hoặc mở từ Gần đây → trang "Đang đọc…" chạy nền (Back trong lúc đọc thì bỏ kết quả, không điều hướng) → màn hình xem với Tìm/Đo/Layer/Ghi chú trên dữ liệu thật, không nhãn Demo. Đọc lỗi: báo lỗi, không vẽ hình thay thế, tệp gốc không đổi.

## Kiểm thử

- `tests/SwiftCad.Cad.Tests`: bản vẽ kiểm thử dựng bằng ACadSharp, ghi rồi đọc lại ở DXF ASCII, DXF binary, DWG AC1032. Kiểm: độ dài 12000 mm, tròn, cung, polyline bulge thành nửa đường tròn, chữ tiếng Việt, MTEXT, block tỷ lệ 2 xoay 90°, layer tắt, đối tượng chưa hỗ trợ được đếm, `\U+XXXX`, tệp hỏng, đuôi lạ.
- Tại chỗ (ACadSharp build từ mã nguồn v3.8.35): 36/36 đạt. Đọc 24 tệp mẫu AutoCAD của repo ACadSharp (R12 → 2018) không lỗi, 30–250 ms/tệp.
- Kiểm tra luồng Android: chọn `TEST-Flow-Plan.dxf` → đọc thật → tìm "phong" ra 1 kết quả.

## Chưa làm (bước sau)

- Hatch (đường bao), Dimension (qua block ẩn), Spline, Polyline 2D/3D, linetype, lineweight, layout/viewport; định dạng trong MTEXT (font, màu từng đoạn, cột).
- Tối ưu bộ nhớ cho tệp lớn (buffer phẳng), chỉ mục không gian cho bắt điểm, hủy đọc giữa chừng.
- Kiểm chứng AOT/trimming trên iOS thật.
- Bảng mã tiếng Việt cũ (VNI/TCVN3) và font SHX.
