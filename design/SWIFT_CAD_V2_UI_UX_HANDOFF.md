# Swift CAD — Approved V2 UI/UX handoff
Owner-approved visual direction, 12 November 2026. **Design reference, not executable app code.**

## Roles
- ChatGPT: UI/UX design, visual asset preparation, UX QA, review.
- Claude: app engineering, real device builds, rendering and integration.
- Owner: approval to merge/release.

## Design principles
- Soft pale-blue neumorphic design inspired by the owner-provided reference, **without copying proprietary brand assets**.
- Surface #E8F1F8, restrained white highlights and blue-gray shadows, sharp readable dark navy text #0F2342, blue primary actions #0868EE.
- Soft raised controls, but high contrast text, obvious hit regions (>=44 pt iOS / >=48 dp Android), avoid excessive shadows in dense content.
- CAD canvas dark near-black and must remain the dominant area. Toolbar should not cover dimensions/details.
- Responsive at 360 and 390 logical px, landscape CAD, large accessibility text. English and Vietnamese translations including screen reader labels.
- **No ads in V1**.

## Approved screens
### 1. Home / Bản vẽ
Top brand Swift CAD blue folded-sheet mark and settings. Search. Primary action **Chọn tệp / Choose file**. Google Drive and Dropbox visible as **source choices**, with status 'Kết nối / Connect' unless selected through OS file picker; do not claim OAuth connected. Recent DWG/DXF list with thumbnails/placeholder, filenames, size/date, overflow actions. Saved and Settings navigation may be included only when useful.

### 2. File picker / Chọn tệp
Browse folders/files, search, DWG/DXF badges and filename, selected state; button **Mở bản vẽ**. Actual Android/iOS implementation should rely on supported native system file pickers; the design visual is illustrative, not a requirement to rebuild the operating system picker.

### 3. CAD viewer / Xem CAD
Top back, filename and overflow. CAD drawing dark full-area viewport with fit-to-view/fullscreen. Bottom quick actions **Tìm / Find**, **Đo / Measure**, **Layer**, **Ghi chú / Markup**. Secondary tools as a collapsible sheet. Preserve viewport when toggling layer. Unit and snap-state should be explicit when measuring; do not present unverified precision.

### 4. Welcome & account
First launch optionally offers **Dùng không cần tài khoản / Continue as guest** (prominent) and **Đăng nhập / Sign in**. Guest has full local viewing features. Do not nag guest after skipping. Login in prototype is Demo only.

### 5. Settings
Account/guest status, language (System / English / Tiếng Việt), CAD canvas theme and measurement unit; clear differentiation of demonstration versus active features.

## Brand asset note
`design/assets/swift-cad-icon-reference.svg` is a **hand-authored vector approximation** of the Owner-approved folded blue mark. It is **NOT** the original approved PNG source and must not be substituted as final master before visual comparison. The original rendered image must be uploaded from the design conversation, then exported to iOS/Android platform icon sizes and reviewed at 48/60/120 px.

## Review/acceptance
1. Side-by-side screenshot comparisons: approved neumorphic screens vs Android/iOS actual screenshots.
2. At widths 360/390 and increased font scale: no clipping or overflow.
3. Native picker cancel, reopen and permissions; Drive/Dropbox via OS document provider only if installed and accessible. No invented connected status.
4. Demo CAD clearly marked; do not claim real DWG rendering until verified.
5. On real DWG, check xref, layers, blocks, Vietnamese fonts, text, hatch; measure with known distances and units.
6. All screen variants EN/VI and accessible labels.
7. PR evidence: actual simulator/device screenshots, build status, notes; no merge/release without Owner approval.
