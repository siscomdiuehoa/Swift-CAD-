using Android.App;
using Android.Content;
using Android.Content.PM;
using SwiftCad.App.Auth;

namespace SwiftCad.App;

/// <summary>
/// Nhận liên kết Google trả về sau khi đăng nhập trong trình duyệt (scheme:/oauth2redirect) và chuyển cho WebAuthenticator.
/// Android mở lại ứng dụng qua liên kết này nên ứng dụng luôn ở phía trước khi đổi mã lấy phiên đăng nhập.
/// </summary>
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter(new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = AuthCallbackScheme.Value)]
public class AuthCallbackActivity : WebAuthenticatorCallbackActivity
{
}
