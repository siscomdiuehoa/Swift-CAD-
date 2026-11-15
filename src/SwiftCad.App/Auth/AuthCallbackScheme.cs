namespace SwiftCad.App.Auth;

/// <summary>
/// Scheme của liên kết Google dùng để trả kết quả đăng nhập về ứng dụng (REVERSED_CLIENT_ID trong GoogleService-Info.plist).
/// Android cần giá trị này lúc biên dịch để đăng ký AuthCallbackActivity. CI ghi đè tệp này từ Secret
/// (tools/ci/firebase_config.py); giá trị dưới đây chỉ là mặc định cho bản dựng không có cấu hình Firebase.
/// Đây không phải bí mật: nó nằm công khai trong mọi bản cài.
/// </summary>
public static class AuthCallbackScheme
{
    public const string Value = "vn.siscom.swiftcad.auth";
}
