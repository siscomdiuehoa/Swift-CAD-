using System.Net;
using System.Text;
using System.Text.Json;
using SwiftCad.Core.Auth;

namespace SwiftCad.App.Auth;

/// <summary>
/// Đăng nhập tùy chọn bằng Google và Apple qua Firebase (gói Spark). Không đăng nhập vẫn dùng đầy đủ tính năng xem bản vẽ.
/// - Google trên iOS: trình duyệt hệ thống (ASWebAuthenticationSession) với OAuth client iOS, PKCE.
/// - Google trên Android: trình duyệt hệ thống + máy chủ cục bộ 127.0.0.1 (OAuth client loại Desktop), PKCE.
///   Không dùng SDK Google Play Services (điều khoản không phải MIT).
/// - Apple trên iOS: AuthenticationServices gốc qua AppleSignInAuthenticator của MAUI.
/// Phiên (refresh token) chỉ lưu trong kho bảo mật của hệ điều hành (Keychain/Keystore).
/// </summary>
public static class AuthService
{
    private const string SessionKey = "swiftcad.auth.session";
    private static AuthConfig? _config;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public static AuthSession? Current { get; private set; }
    public static event Action? Changed;

    public static bool IsConfigured => _config is not null;
    public static bool CanUseGoogle => _config is { } c && (DeviceInfo.Platform == DevicePlatform.iOS ? c.IosClientId.Length > 0 : c.DesktopClientId.Length > 0);
    public static bool CanUseApple => _config is not null && DeviceInfo.Platform == DevicePlatform.iOS;

    /// <summary>Đọc cấu hình (tệp firebase.json do CI tạo từ Secret) và phiên đã lưu. Không có cấu hình thì đăng nhập bị ẩn.</summary>
    public static async Task InitAsync()
    {
        try
        {
            await using var s = await FileSystem.OpenAppPackageFileAsync("firebase.json");
            _config = await JsonSerializer.DeserializeAsync(s, AuthConfigJson.Default.AuthConfig);
            if (_config is { ApiKey.Length: 0 }) _config = null;
        }
        catch (Exception) { _config = null; }
        try { Current = AuthSessionCodec.Deserialize(await SecureStorage.Default.GetAsync(SessionKey)); }
        catch (Exception) { Current = null; }
    }

    private static FirebaseAuthClient Firebase() => new(Http, new FirebaseConfig(_config!.ApiKey, _config.ProjectId));

    public static async Task SignInWithGoogleAsync()
    {
        if (!CanUseGoogle) throw new AuthException("not_configured", "Google sign-in is not configured.");
        var verifier = Pkce.RandomUrlSafe(); var state = Pkce.RandomUrlSafe(16); var nonce = Pkce.RandomUrlSafe(16);
        string idToken;
        if (DeviceInfo.Platform == DevicePlatform.iOS)
        {
            var redirect = _config!.IosReversedClientId + ":/oauth2redirect";
            var url = Pkce.GoogleAuthorizeUrl(_config.IosClientId, redirect, Pkce.Challenge(verifier), state, nonce);
            var r = await WebAuthenticator.Default.AuthenticateAsync(new WebAuthenticatorOptions { Url = new Uri(url), CallbackUrl = new Uri(redirect) });
            var code = CheckCallback(r.Properties, state);
            idToken = await new GoogleTokenClient(Http).ExchangeCodeAsync(code, _config.IosClientId, null, redirect, verifier);
        }
        else
        {
            idToken = await LoopbackGoogleAsync(verifier, state, nonce);
        }
        await Save(await Firebase().SignInWithIdTokenAsync(AuthProvider.Google, idToken));
    }

    public static async Task SignInWithAppleAsync()
    {
        if (!CanUseApple) throw new AuthException("not_supported", "Apple sign-in is only available on iPhone/iPad.");
        var r = await AppleSignInAuthenticator.Default.AuthenticateAsync(new AppleSignInAuthenticator.Options { IncludeEmailScope = true, IncludeFullNameScope = true });
        if (string.IsNullOrEmpty(r.IdToken)) throw new AuthException("apple_no_token", "Apple did not return an identity token.");
        var session = await Firebase().SignInWithIdTokenAsync(AuthProvider.Apple, r.IdToken);
        // Apple chỉ gửi tên ở lần đăng nhập đầu tiên; giữ lại nếu Firebase chưa có.
        if (string.IsNullOrEmpty(session.DisplayName) && r.Properties.TryGetValue("name", out var name) && !string.IsNullOrWhiteSpace(name))
            session = session with { DisplayName = name };
        await Save(session);
    }

    public static async Task SignOutAsync()
    {
        SecureStorage.Default.Remove(SessionKey);
        Current = null;
        Changed?.Invoke();
        await Task.CompletedTask;
    }

    /// <summary>Xóa vĩnh viễn tài khoản trên Firebase rồi đăng xuất. Bản vẽ và ghi chú trên máy giữ nguyên.</summary>
    public static async Task DeleteAccountAsync()
    {
        if (Current is null || _config is null) return;
        await Firebase().DeleteAccountAsync(Current);
        await SignOutAsync();
    }

    private static async Task Save(AuthSession s)
    {
        await SecureStorage.Default.SetAsync(SessionKey, AuthSessionCodec.Serialize(s));
        Current = s;
        Changed?.Invoke();
    }

    private static string CheckCallback(IDictionary<string, string> props, string expectedState)
    {
        if (!props.TryGetValue("state", out var st) || st != expectedState) throw new AuthException("state_mismatch", "Sign-in response did not match the request.");
        if (props.TryGetValue("error", out var err)) throw new AuthException("google_" + err, "Google sign-in was not completed.");
        return props.TryGetValue("code", out var code) && code.Length > 0 ? code : throw new AuthException("google_no_code", "Google did not return a code.");
    }

    /// <summary>
    /// Android: mở trình duyệt hệ thống tới Google, nhận mã qua máy chủ cục bộ 127.0.0.1 (cổng ngẫu nhiên) — cách Google cho phép
    /// với OAuth client loại Desktop. Trang trả về báo người dùng quay lại ứng dụng.
    /// </summary>
    private static async Task<string> LoopbackGoogleAsync(string verifier, string state, string nonce)
    {
        var port = FreePort();
        var redirect = $"http://127.0.0.1:{port}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(redirect);
        listener.Start();
        var url = Pkce.GoogleAuthorizeUrl(_config!.DesktopClientId, redirect, Pkce.Challenge(verifier), state, nonce);
        await Browser.Default.OpenAsync(new Uri(url), BrowserLaunchMode.SystemPreferred);

        var getCtx = listener.GetContextAsync();
        if (await Task.WhenAny(getCtx, Task.Delay(TimeSpan.FromMinutes(5))) != getCtx) throw new AuthException("timeout", "Sign-in timed out.");
        var ctx = await getCtx;
        var q = System.Web.HttpUtility.ParseQueryString(ctx.Request.Url?.Query ?? "");
        var props = q.AllKeys.Where(k => k is not null).ToDictionary(k => k!, k => q[k] ?? "");
        var html = Encoding.UTF8.GetBytes("<!doctype html><meta charset=utf-8><meta name=viewport content='width=device-width'><body style='font-family:sans-serif;padding:24px'><h2>Swift CAD</h2><p>"
            + WebUtility.HtmlEncode(L.T("auth_return_to_app")) + "</p></body>");
        ctx.Response.ContentType = "text/html; charset=utf-8";
        ctx.Response.ContentLength64 = html.Length;
        await ctx.Response.OutputStream.WriteAsync(html);
        ctx.Response.Close();
        listener.Stop();
        var code = CheckCallback(props, state);
        return await new GoogleTokenClient(Http).ExchangeCodeAsync(code, _config.DesktopClientId, _config.DesktopClientSecret, redirect, verifier);
    }

    private static int FreePort()
    {
        var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }
}

/// <summary>firebase.json: tạo lúc build từ GitHub Secrets, không có trong repo.</summary>
public sealed record AuthConfig(string ApiKey, string ProjectId, string IosClientId, string IosReversedClientId,
    string DesktopClientId, string DesktopClientSecret);

[System.Text.Json.Serialization.JsonSourceGenerationOptions(PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase)]
[System.Text.Json.Serialization.JsonSerializable(typeof(AuthConfig))]
internal sealed partial class AuthConfigJson : System.Text.Json.Serialization.JsonSerializerContext;
