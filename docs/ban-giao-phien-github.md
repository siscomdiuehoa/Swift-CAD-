# Bàn giao cho phiên có quyền GitHub

Dành cho phiên Claude Code/Cowork (hoặc người) có quyền ghi lên `siscomdiuehoa/Swift-CAD-`. Tiếp tục từ mã hiện có, không dựng lại từ đầu.

## Đầu vào

- Tệp `swift-cad-v1-foundation.bundle` chứa hai nhánh: `main` (bootstrap) và `feat/v1-foundation`.
- Mô tả PR: `docs/pr/feat-v1-foundation.md`.

## Các bước

1. Khôi phục và đẩy:
   ```
   git clone -b main swift-cad-v1-foundation.bundle Swift-CAD && cd Swift-CAD
   git fetch origin feat/v1-foundation:feat/v1-foundation
   git remote set-url origin https://github.com/siscomdiuehoa/Swift-CAD-.git
   git push origin main feat/v1-foundation
   ```
2. Mở Draft PR `feat/v1-foundation` → `main`, tiêu đề và nội dung lấy từ `docs/pr/feat-v1-foundation.md`, có dòng liên kết Issue #1.
3. Theo dõi workflow `CI`. Sửa lỗi cho tới khi ba job đạt: `core`, `android`, `ios-simulator`. Điểm dễ lỗi nhất:
   - Số phiên bản gói `Microsoft.Maui.Controls` 10.0.130 và `SkiaSharp.Views.Maui.Controls` 4.155.0 chưa được đối chiếu trên NuGet.
   - Phiên bản workload MAUI và ảnh máy macOS/Xcode.
   - Mã trong `src/SwiftCad.App` chưa từng qua trình biên dịch MAUI.
   - Tên tệp APK đầu ra mà bước upload-artifact tìm.
4. Chỉ ghi "build đạt" trong PR khi job CI tương ứng xanh. Kết quả `tools/typecheck` không được dùng làm bằng chứng.
5. Chạy tay workflow `Anh chay that tren Android emulator`. Script `tools/ci/shots.py` chưa chạy thử lần nào, cần sửa nếu lỗi. Kết quả mong đợi: ảnh ở bề rộng 360 và 390, tiếng Anh và tiếng Việt, và cỡ chữ 1,3.
6. Đính ảnh vào PR, cập nhật bảng trạng thái theo từng nền tảng: build Android, build iOS simulator, chạy emulator/simulator, chạy máy thật.
7. Ảnh iOS simulator: CI hiện chỉ build, chưa chụp. Thêm bước chạy simulator và chụp khi build iOS đã xanh.

## Ràng buộc

- Giữ nhãn Demo cho bản vẽ mẫu. Không ghi là đã đọc DWG/DXF thật.
- Icon ứng dụng hiện là bản tạm, chưa nghiệm thu. Khi có ảnh đã duyệt: lưu `docs/design/swift-cad-approved.png`, thay `Resources/AppIcon` và `Resources/Splash`, dẫn ảnh trong PR.
- Không merge, không phát hành. Codex review, Owner duyệt merge.
- Chỉ thêm thư viện miễn phí dùng thương mại được, và ghi vào `docs/thu-vien-ben-thu-ba.md`.
- Hỏi Owner trước khi bật bất kỳ khoản trả phí nào.
