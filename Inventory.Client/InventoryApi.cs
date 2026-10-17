using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Security.Authentication;
using Inventory.Contracts;

namespace Inventory.Client;

public sealed class ApiException(HttpStatusCode status, string message) : InvalidOperationException(message)
{ public HttpStatusCode Status { get; } = status; }

/// <summary>Portable HTTPS client. Credentials and session tokens are never written to disk.</summary>
public sealed class InventoryApi : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly Uri _base;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private string _token;
    public UserInfo CurrentUser { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public bool CanWrite => CurrentUser?.Role is Roles.Admin or Roles.Operator;
    public bool CanDelete => CurrentUser?.Role == Roles.Admin;
    public InventoryApi(string serverUrl, HttpClient http = null)
    {
        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("Укажите HTTPS-адрес сервера без логина, пароля и параметров.", nameof(serverUrl));
        _base = new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
        _http = http ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13 }) { Timeout = TimeSpan.FromSeconds(20) };
        _ownsHttp = http == null;
    }
    public async Task<LoginResponse> LoginAsync(string username, string password, CancellationToken cancellation = default)
    {
        ForgetSession();
        var response = await Send<LoginResponse>(HttpMethod.Post, "api/v1/auth/login", new LoginRequest(username, password), false, cancellation).ConfigureAwait(false);
        _token = response.AccessToken; CurrentUser = response.User; ExpiresAt = response.ExpiresAt;
        return response;
    }
    public async Task LogoutAsync(CancellationToken cancellation = default)
    {
        try { if (_token != null) await Send<object>(HttpMethod.Post, "api/v1/auth/logout", null, true, cancellation).ConfigureAwait(false); }
        finally { ForgetSession(); }
    }
    public void ForgetSession() { _token = null; CurrentUser = null; ExpiresAt = default; }
    public Task<T> GetAsync<T>(string relativePath, CancellationToken cancellation = default) => Send<T>(HttpMethod.Get, relativePath, null, true, cancellation);
    public Task<T> PostAsync<T>(string relativePath, object body, CancellationToken cancellation = default, string idempotencyKey = null)
        => Send<T>(HttpMethod.Post, relativePath, body, true, cancellation, idempotencyKey);
    public Task PutAsync(string relativePath, object body, CancellationToken cancellation = default) => Send<object>(HttpMethod.Put, relativePath, body, true, cancellation);
    public Task DeleteAsync(string relativePath, CancellationToken cancellation = default) => Send<object>(HttpMethod.Delete, relativePath, null, true, cancellation);
    public Task<SaleReceipt> CreateSaleAsync(CreateSaleRequest sale, string idempotencyKey, CancellationToken cancellation = default)
        => PostAsync<SaleReceipt>("api/v1/sales", sale, cancellation, idempotencyKey);
    private async Task<T> Send<T>(HttpMethod method, string path, object body, bool authenticated, CancellationToken cancellation, string idempotencyKey = null)
    {
        // Restrict calls to this server even if a caller passes an absolute URL or traversal.
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith('/') || path.Contains('\\') || Uri.TryCreate(path, UriKind.Absolute, out _))
            throw new ArgumentException("Нужен относительный путь API.", nameof(path));
        var address = new Uri(_base, path);
        if (address.Scheme != _base.Scheme || address.Host != _base.Host || address.Port != _base.Port
            || !address.AbsolutePath.StartsWith(_base.AbsolutePath, StringComparison.Ordinal)) throw new ArgumentException("Путь вне сервера.");
        using var request = new HttpRequestMessage(method, address);
        if (authenticated)
        {
            var token = _token;
            if (token == null || ExpiresAt <= DateTimeOffset.UtcNow)
            { ForgetSession(); throw new ApiException(HttpStatusCode.Unauthorized, "Сессия закончилась. Войдите заново."); }
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        if (body != null) request.Content = JsonContent.Create(body, options: Json);
        if (idempotencyKey != null) request.Headers.Add("Idempotency-Key", idempotencyKey);
        using var response = await _http.SendAsync(request, cancellation).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized && authenticated) ForgetSession();
            var text = "Сервер вернул ошибку " + (int)response.StatusCode + ".";
            try
            {
                var problem = await response.Content.ReadFromJsonAsync<ApiProblem>(Json, cancellation).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(problem?.Detail)) text = problem.Detail;
                else if (!string.IsNullOrWhiteSpace(problem?.Title)) text = problem.Title;
            }
            catch (JsonException) { }
            throw new ApiException(response.StatusCode, text);
        }
        if (response.StatusCode == HttpStatusCode.NoContent) return default;
        return await response.Content.ReadFromJsonAsync<T>(Json, cancellation).ConfigureAwait(false);
    }
    // Compatibility for the existing synchronous UI. New integrations should use the async API.
    public T Get<T>(string path) => GetAsync<T>(path).GetAwaiter().GetResult();
    public T Post<T>(string path, object body, string key = null) => PostAsync<T>(path, body, idempotencyKey: key).GetAwaiter().GetResult();
    public void Put(string path, object body) => PutAsync(path, body).GetAwaiter().GetResult();
    public void Delete(string path) => DeleteAsync(path).GetAwaiter().GetResult();
    public void Dispose() { ForgetSession(); if (_ownsHttp) _http.Dispose(); }
}
