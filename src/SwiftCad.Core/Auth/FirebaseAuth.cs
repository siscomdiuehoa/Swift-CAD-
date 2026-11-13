using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwiftCad.Core.Auth;

/// <summary>Cấu hình công khai của dự án Firebase (không phải bí mật, nhưng không đưa vào repo công khai).</summary>
public sealed record FirebaseConfig(string ApiKey, string ProjectId);

/// <summary>Phiên đăng nhập. RefreshToken là bí mật, chỉ lưu trong kho bảo mật của hệ điều hành.</summary>
public sealed record AuthSession(string UserId, string? Email, string? DisplayName, string Provider,
    string IdToken, string RefreshToken, DateTimeOffset ExpiresUtc)
{
    public bool NeedsRefresh(DateTimeOffset now) => now >= ExpiresUtc - TimeSpan.FromMinutes(5);
}

public sealed class AuthException(string code, string message, Exception? inner = null) : Exception(message, inner)
{
    /// <summary>Mã lỗi Firebase (ví dụ INVALID_IDP_RESPONSE, TOKEN_EXPIRED) hoặc mã nội bộ (network, cancelled).</summary>
    public string Code { get; } = code;
}

public enum AuthProvider { Google, Apple }

/// <summary>
/// Đăng nhập Firebase qua REST (identitytoolkit, securetoken), không dùng SDK Firebase.
/// Ứng dụng tự lấy id_token của Google/Apple rồi đổi lấy phiên Firebase ở đây.
/// </summary>
public sealed class FirebaseAuthClient(HttpClient http, FirebaseConfig config, Func<DateTimeOffset>? clock = null)
{
    private readonly Func<DateTimeOffset> _now = clock ?? (() => DateTimeOffset.UtcNow);
    private const string Identity = "https://identitytoolkit.googleapis.com/v1/";
    private const string SecureToken = "https://securetoken.googleapis.com/v1/token";

    public static string ProviderId(AuthProvider p) => p == AuthProvider.Apple ? "apple.com" : "google.com";

    /// <summary>Đổi id_token của nhà cung cấp (Google/Apple) lấy phiên Firebase. rawNonce: chuỗi nonce gốc nếu id_token có nonce.</summary>
    public async Task<AuthSession> SignInWithIdTokenAsync(AuthProvider provider, string idToken, string? rawNonce = null, CancellationToken ct = default)
    {
        var post = $"id_token={Uri.EscapeDataString(idToken)}&providerId={ProviderId(provider)}";
        if (!string.IsNullOrEmpty(rawNonce)) post += "&nonce=" + Uri.EscapeDataString(rawNonce);
        var body = new SignInWithIdpRequest(post, "http://localhost", true, true);
        var r = await PostAsync(Identity + "accounts:signInWithIdp?key=" + config.ApiKey, body, AuthJson.Default.SignInWithIdpRequest, AuthJson.Default.SignInWithIdpResponse, ct);
        return new AuthSession(r.LocalId, r.Email, r.DisplayName ?? r.FullName, ProviderId(provider), r.IdToken, r.RefreshToken,
            _now() + TimeSpan.FromSeconds(ParseSeconds(r.ExpiresIn)));
    }

    /// <summary>Làm mới phiên bằng refresh token. Giữ nguyên email/tên đã có.</summary>
    public async Task<AuthSession> RefreshAsync(AuthSession s, CancellationToken ct = default)
    {
        using var content = new FormUrlEncodedContent([new("grant_type", "refresh_token"), new("refresh_token", s.RefreshToken)]);
        HttpResponseMessage resp;
        try { resp = await http.PostAsync(SecureToken + "?key=" + config.ApiKey, content, ct); }
        catch (HttpRequestException ex) { throw new AuthException("network", "Không kết nối được máy chủ đăng nhập.", ex); }
        using (resp)
        {
            var text = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode) throw ErrorFrom(text);
            var r = JsonSerializer.Deserialize(text, AuthJson.Default.RefreshResponse) ?? throw new AuthException("bad_response", "Phản hồi không hợp lệ.");
            return s with { IdToken = r.IdToken, RefreshToken = r.RefreshToken, ExpiresUtc = _now() + TimeSpan.FromSeconds(ParseSeconds(r.ExpiresIn)) };
        }
    }

    /// <summary>Xóa vĩnh viễn tài khoản Firebase của phiên này (yêu cầu của Apple: xóa tài khoản ngay trong ứng dụng).</summary>
    public async Task DeleteAccountAsync(AuthSession s, CancellationToken ct = default)
    {
        if (s.NeedsRefresh(_now())) s = await RefreshAsync(s, ct);
        await PostAsync(Identity + "accounts:delete?key=" + config.ApiKey, new DeleteRequest(s.IdToken), AuthJson.Default.DeleteRequest, AuthJson.Default.EmptyResponse, ct);
    }

    private async Task<TOut> PostAsync<TIn, TOut>(string url, TIn body,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TIn> inInfo, System.Text.Json.Serialization.Metadata.JsonTypeInfo<TOut> outInfo, CancellationToken ct)
    {
        HttpResponseMessage resp;
        try { resp = await http.PostAsJsonAsync(url, body, inInfo, ct); }
        catch (HttpRequestException ex) { throw new AuthException("network", "Không kết nối được máy chủ đăng nhập.", ex); }
        using (resp)
        {
            var text = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode) throw ErrorFrom(text);
            return JsonSerializer.Deserialize(string.IsNullOrWhiteSpace(text) ? "{}" : text, outInfo) ?? throw new AuthException("bad_response", "Phản hồi không hợp lệ.");
        }
    }

    private static AuthException ErrorFrom(string text)
    {
        try
        {
            var e = JsonSerializer.Deserialize(text, AuthJson.Default.ErrorEnvelope);
            var msg = e?.Error?.Message ?? "unknown";
            // Firebase trả "CODE : mô tả"; giữ phần mã.
            var code = msg.Split(':', 2)[0].Trim();
            return new AuthException(code, msg);
        }
        catch (JsonException) { return new AuthException("bad_response", "Phản hồi lỗi không đọc được."); }
    }

    private static double ParseSeconds(string? s) =>
        double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 3600;
}

/// <summary>PKCE (RFC 7636) và nonce cho luồng đăng nhập qua trình duyệt.</summary>
public static class Pkce
{
    public static string RandomUrlSafe(int bytes = 32) => Base64Url(RandomNumberGenerator.GetBytes(bytes));
    public static string Challenge(string verifier) => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
    public static string Sha256Hex(string s) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
    public static string Base64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>URL đăng nhập Google (authorization code + PKCE, kèm nonce).</summary>
    public static string GoogleAuthorizeUrl(string clientId, string redirectUri, string challenge, string state, string nonce) =>
        "https://accounts.google.com/o/oauth2/v2/auth?response_type=code&scope=" + Uri.EscapeDataString("openid email profile")
        + "&client_id=" + Uri.EscapeDataString(clientId) + "&redirect_uri=" + Uri.EscapeDataString(redirectUri)
        + "&code_challenge=" + challenge + "&code_challenge_method=S256&state=" + Uri.EscapeDataString(state)
        + "&nonce=" + Uri.EscapeDataString(nonce) + "&prompt=select_account";
}

/// <summary>Đổi mã ủy quyền Google lấy id_token (endpoint token của Google).</summary>
public sealed class GoogleTokenClient(HttpClient http)
{
    public async Task<string> ExchangeCodeAsync(string code, string clientId, string? clientSecret, string redirectUri, string verifier, CancellationToken ct = default)
    {
        var form = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "authorization_code"), new("code", code), new("client_id", clientId),
            new("redirect_uri", redirectUri), new("code_verifier", verifier),
        };
        if (!string.IsNullOrEmpty(clientSecret)) form.Add(new("client_secret", clientSecret));
        HttpResponseMessage resp;
        try { resp = await http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(form), ct); }
        catch (HttpRequestException ex) { throw new AuthException("network", "Không kết nối được Google.", ex); }
        using (resp)
        {
            var text = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode) throw new AuthException("google_token", "Google từ chối mã đăng nhập.");
            var r = JsonSerializer.Deserialize(text, AuthJson.Default.GoogleTokenResponse);
            return r?.IdToken ?? throw new AuthException("google_token", "Google không trả id_token.");
        }
    }
}

public sealed record SignInWithIdpRequest(
    [property: JsonPropertyName("postBody")] string PostBody,
    [property: JsonPropertyName("requestUri")] string RequestUri,
    [property: JsonPropertyName("returnSecureToken")] bool ReturnSecureToken,
    [property: JsonPropertyName("returnIdpCredential")] bool ReturnIdpCredential);

public sealed record SignInWithIdpResponse(
    [property: JsonPropertyName("localId")] string LocalId,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("displayName")] string? DisplayName,
    [property: JsonPropertyName("fullName")] string? FullName,
    [property: JsonPropertyName("idToken")] string IdToken,
    [property: JsonPropertyName("refreshToken")] string RefreshToken,
    [property: JsonPropertyName("expiresIn")] string? ExpiresIn);

public sealed record RefreshResponse(
    [property: JsonPropertyName("id_token")] string IdToken,
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("expires_in")] string? ExpiresIn);

public sealed record DeleteRequest([property: JsonPropertyName("idToken")] string IdToken);
public sealed record EmptyResponse;
public sealed record ErrorBody([property: JsonPropertyName("message")] string? Message);
public sealed record ErrorEnvelope([property: JsonPropertyName("error")] ErrorBody? Error);
public sealed record GoogleTokenResponse([property: JsonPropertyName("id_token")] string? IdToken);

[JsonSerializable(typeof(SignInWithIdpRequest))]
[JsonSerializable(typeof(SignInWithIdpResponse))]
[JsonSerializable(typeof(RefreshResponse))]
[JsonSerializable(typeof(DeleteRequest))]
[JsonSerializable(typeof(EmptyResponse))]
[JsonSerializable(typeof(ErrorEnvelope))]
[JsonSerializable(typeof(GoogleTokenResponse))]
[JsonSerializable(typeof(AuthSession))]
internal sealed partial class AuthJson : JsonSerializerContext;

/// <summary>Chuyển phiên sang/từ chuỗi để lưu trong kho bảo mật của hệ điều hành.</summary>
public static class AuthSessionCodec
{
    public static string Serialize(AuthSession s) => JsonSerializer.Serialize(s, AuthJson.Default.AuthSession);
    public static AuthSession? Deserialize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        try { return JsonSerializer.Deserialize(s, AuthJson.Default.AuthSession); }
        catch (JsonException) { return null; }
    }
}
