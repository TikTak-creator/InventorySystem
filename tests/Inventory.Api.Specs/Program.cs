using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Diagnostics;
using EasyDo.Data;
using Inventory.Client;
using Inventory.Contracts;
using Inventory.Server;
using Inventory.Server.Security;
using Inventory.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlConnector;

var checks = 0;
void Check(bool accepted, string message = "Assertion failed") { if (!accepted) throw new Exception(message); }
async Task Test(string name, Func<Task> run) { await run(); checks++; Console.WriteLine("PASS " + name); }
async Task Status(HttpResponseMessage response, HttpStatusCode expected)
{
    using (response)
    { if (response.StatusCode != expected) throw new Exception($"Expected {(int)expected}, received {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}"); }
}
async Task<T> Json<T>(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
{
    using (response)
    {
        if (response.StatusCode != expected) throw new Exception($"Expected {(int)expected}, received {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<T>();
    }
}

// Requires an explicit test server connection, never silently uses the application's database.
var settingsPath = args.Length == 2 && args[0] == "--settings" ? args[1] : throw new ArgumentException("Use --settings PATH to an isolated MySQL settings file.");
using var settings = JsonDocument.Parse(File.ReadAllText(settingsPath));
var mysql = new MySqlConnectionStringBuilder(settings.RootElement.GetProperty("ConnectionStrings").GetProperty("Default").GetString());
var databaseName = "easydo_test_" + Guid.NewGuid().ToString("N"); mysql.Database = "";
var serverConnection = mysql.ConnectionString;
var server = new Store(() => new MySqlConnection(serverConnection));
server.Run(s => s.Execute(Sql.TextOnly($"CREATE DATABASE `{databaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci")));
try
{
    mysql.Database = databaseName; mysql.SslMode = MySqlSslMode.Required;
    var connection = mysql.ConnectionString;
    using var key = RSA.Create(2048);
    var certRequest = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    var san = new SubjectAlternativeNameBuilder(); san.AddDnsName("localhost"); san.AddIpAddress(IPAddress.Loopback);
    certRequest.CertificateExtensions.Add(san.Build());
    certRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
    certRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
    certRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, true));
    using var generatedCertificate = certRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
    // Schannel on Windows needs an imported private key for an HTTPS server credential.
    using var certificate = new X509Certificate2(generatedCertificate.Export(X509ContentType.Pfx));
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development", ApplicationName = typeof(ServerHost).Assembly.FullName });
    builder.Logging.ClearProviders(); builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Warning);
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string>
    {
        ["ConnectionStrings:Default"] = connection, ["Database:InitializeSchema"] = "true", ["Database:SeedDemoData"] = "false",
        ["urls"] = "https://127.0.0.1", ["Security:SessionMinutes"] = "60"
    });
    builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(certificate)));
    await using var app = ServerHost.Build(builder);
    await app.StartAsync();
    var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.Single();
    var users = app.Services.GetRequiredService<UserStore>();
    var passwords = app.Services.GetRequiredService<PasswordService>();
    var store = app.Services.GetRequiredService<Store>();
    var userAdmin = users.Create(new("admin", "ApiSpec-LongPassword!1", Roles.Admin));
    var userOperator = users.Create(new("operator", "ApiSpec-LongPassword!2", Roles.Operator));
    var userViewer = users.Create(new("viewer", "ApiSpec-LongPassword!3", Roles.Viewer));
    // Only tests trust this exact ephemeral certificate. Production InventoryApi uses normal TLS validation.
    HttpClient Client(string token = null)
    {
        var client = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            ServerCertificateCustomValidationCallback = (_, actual, _, errors) => actual != null
                && errors is SslPolicyErrors.None or SslPolicyErrors.RemoteCertificateChainErrors
                && CryptographicOperations.FixedTimeEquals(actual.GetCertHash(HashAlgorithmName.SHA256), certificate.GetCertHash(HashAlgorithmName.SHA256))
        }) { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(30) };
        if (token != null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
    using var anonymous = Client();
    await Test("HTTPS liveness is public; data requires authentication", async () =>
    { await Status(await anonymous.GetAsync("/health"), HttpStatusCode.OK); await Status(await anonymous.GetAsync("/api/v1/products"), HttpStatusCode.Unauthorized); });
    await Test("production startup rejects unverified database TLS and HTTP listeners", () =>
    {
        var production = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production", ApplicationName = typeof(ServerHost).Assembly.FullName });
        production.Configuration["ConnectionStrings:Default"] = connection;
        var rejected = false;
        try { ServerHost.ValidateConnection(production); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected);
        var verified = new MySqlConnectionStringBuilder(connection) { SslMode = MySqlSslMode.VerifyFull };
        production.Configuration["ConnectionStrings:Default"] = verified.ConnectionString;
        Check(ServerHost.ValidateConnection(production) == verified.ConnectionString);
        production.Configuration["urls"] = "http://localhost:8080";
        rejected = false;
        try { ServerHost.Build(production); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected);
        production.Configuration["urls"] = "https://localhost:7071";
        production.Configuration["Kestrel:Endpoints:PlainHttp:Url"] = "http://localhost:8080";
        rejected = false;
        try { ServerHost.Build(production); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected); return Task.CompletedTask;
    });
    await Test("bad password returns generic 401", async () =>
    { await Status(await anonymous.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("admin", "wrong")), HttpStatusCode.Unauthorized); });
    var adminSession = await Json<LoginResponse>(await anonymous.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("admin", "ApiSpec-LongPassword!1")));
    var operatorSession = await Json<LoginResponse>(await anonymous.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("operator", "ApiSpec-LongPassword!2")));
    var viewerSession = await Json<LoginResponse>(await anonymous.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("viewer", "ApiSpec-LongPassword!3")));
    using var admin = Client(adminSession.AccessToken); using var op = Client(operatorSession.AccessToken); using var viewer = Client(viewerSession.AccessToken);
    await Test("login supplies expiring bearer session and identity", async () =>
    { Check(adminSession.AccessToken.Length == 43 && adminSession.ExpiresAt > DateTimeOffset.UtcNow); var me = await Json<UserInfo>(await admin.GetAsync("/api/v1/auth/me")); Check(me.Role == Roles.Admin && me.Id == userAdmin.Id); });
    await Test("password salts differ and session storage contains only hashes", () =>
    {
        var first = passwords.Hash("same-long-test-password"); var second = passwords.Hash("same-long-test-password"); Check(first != second);
        Check(passwords.Verify(first, "same-long-test-password") != Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed);
        var hashes = store.Read(s => s.Query(Sql.TextOnly("SELECT token_hash FROM app_sessions"), r => r.GetString(0)));
        Check(hashes.Count == 3 && hashes.All(hash => hash.Length == 64 && hash != adminSession.AccessToken));
        return Task.CompletedTask;
    });
    await Test("viewer cannot mutate products or create users", async () =>
    {
        await Status(await viewer.PostAsJsonAsync("/api/v1/products", new Product { Sku = "NO", Name = "No", Price = 1 }), HttpStatusCode.Forbidden);
        await Status(await op.PostAsJsonAsync("/api/v1/users", new CreateUserRequest("forbidden", "Long-Password!123", Roles.Admin)), HttpStatusCode.Forbidden);
    });
    Product NewProduct(string sku, int quantity = 10) => new() { Sku = sku, Name = "Товар " + sku, Category = "Тест", Price = 25.50m, PurchasePrice = 15, Quantity = quantity, MinQuantity = 2 };
    var product = NewProduct("API-001");
    var inserted = await Json<IdResponse>(await op.PostAsJsonAsync("/api/v1/products", product), HttpStatusCode.Created);
    var productId = inserted.Id;
    await Test("operator creates product and viewer reads it", async () =>
    { product = await Json<Product>(await viewer.GetAsync($"/api/v1/products/{productId}")); Check(product.Price == 25.50m && product.Quantity == 10); });
    await Test("filters, categories, SKU and pagination are available", async () =>
    {
        Check((await Json<List<Product>>(await viewer.GetAsync("/api/v1/products?search=API-001&category=" + Uri.EscapeDataString("Тест") + "&limit=1"))).Single().Id == productId);
        Check((await Json<List<string>>(await viewer.GetAsync("/api/v1/products/categories"))).Contains("Тест"));
        Check((await Json<ExistsResponse>(await viewer.GetAsync("/api/v1/products/sku-exists?sku=API-001"))).Exists);
        await Status(await viewer.GetAsync("/api/v1/products?limit=201"), HttpStatusCode.BadRequest);
    });
    await Test("invalid product is rejected; duplicate SKU is conflict", async () =>
    {
        var invalid = NewProduct("INVALID"); invalid.Quantity = -1;
        await Status(await op.PostAsJsonAsync("/api/v1/products", invalid), HttpStatusCode.BadRequest);
        await Status(await op.PostAsJsonAsync("/api/v1/products", NewProduct("API-001")), HttpStatusCode.Conflict);
        invalid.Quantity = 1; invalid.Price = 1.123m;
        await Status(await op.PostAsJsonAsync("/api/v1/products", invalid), HttpStatusCode.BadRequest);
    });
    await Test("update checks version and price history; stale card cannot restore stock", async () =>
    {
        var oldVersion = product.UpdatedAt; product.Price = 30;
        await Status(await op.PutAsJsonAsync($"/api/v1/products/{productId}", product), HttpStatusCode.NoContent);
        await Status(await op.PutAsJsonAsync($"/api/v1/products/{productId}", product), HttpStatusCode.Conflict);
        var history = await Json<List<PriceChange>>(await viewer.GetAsync($"/api/v1/products/{productId}/price-history"));
        Check(history.Single().OldPrice == 25.50m && history.Single().NewPrice == 30);
        product = await Json<Product>(await op.GetAsync($"/api/v1/products/{productId}")); Check(product.UpdatedAt > oldVersion);
    });
    await Test("stock arrival updates quantity; unknown products return 404", async () =>
    {
        await Status(await op.PostAsJsonAsync($"/api/v1/products/{productId}/stock", new StockRequest(2)), HttpStatusCode.NoContent);
        await Status(await viewer.GetAsync("/api/v1/products/999999"), HttpStatusCode.NotFound);
        await Status(await op.PostAsJsonAsync("/api/v1/products/999999/stock", new StockRequest(1)), HttpStatusCode.NotFound);
        await Status(await viewer.GetAsync("/api/v1/sales/999999/items"), HttpStatusCode.NotFound);
    });
    var basket = new CreateSaleRequest([new SaleLineRequest(productId, 2)]); var saleKey = Guid.NewGuid().ToString("D");
    async Task<HttpResponseMessage> Buy(HttpClient client, CreateSaleRequest cart, string keyValue)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/sales") { Content = JsonContent.Create(cart) };
        if (keyValue != null) request.Headers.Add("Idempotency-Key", keyValue);
        return await client.SendAsync(request);
    }
    SaleReceipt receipt = null;
    await Test("sale snapshots price/cost and retries do not create a second receipt", async () =>
    {
        receipt = await Json<SaleReceipt>(await Buy(op, basket, saleKey));
        var repeated = await Json<SaleReceipt>(await Buy(op, basket, saleKey));
        Check(receipt == repeated && receipt.Total == 60);
        Check((await Json<Product>(await viewer.GetAsync($"/api/v1/products/{productId}"))).Quantity == 10);
        var lines = await Json<List<SaleItem>>(await viewer.GetAsync($"/api/v1/sales/{receipt.Id}/items")); Check(lines.Single().CostPrice == 15);
    });
    await Test("missing key and price injection are rejected; different body with same key conflicts", async () =>
    {
        await Status(await Buy(op, basket, null), HttpStatusCode.BadRequest);
        await Status(await Buy(op, new([new(productId, 1)]), saleKey), HttpStatusCode.Conflict);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/sales") { Content = JsonContent.Create(new { items = new[] { new { productId, quantity = 1, price = 0.01 } } }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        await Status(await op.SendAsync(request), HttpStatusCode.BadRequest);
    });
    await Test("concurrent retries of one request return exactly one sale", async () =>
    {
        var keyValue = Guid.NewGuid().ToString("D"); var cart = new CreateSaleRequest([new(productId, 1)]);
        var responses = await Task.WhenAll(Buy(op, cart, keyValue), Buy(op, cart, keyValue));
        var first = await Json<SaleReceipt>(responses[0]); var second = await Json<SaleReceipt>(responses[1]); Check(first == second);
        Check((await Json<Product>(await viewer.GetAsync($"/api/v1/products/{productId}"))).Quantity == 9);
    });
    await Test("insufficient stock rolls back receipt and idempotency claim", async () =>
    {
        var keyValue = Guid.NewGuid().ToString("D"); var before = store.Read(s => s.Scalar<int>(Sql.TextOnly("SELECT COUNT(*) FROM sales")));
        await Status(await Buy(op, new([new(productId, 100)]), keyValue), HttpStatusCode.Conflict);
        Check(before == store.Read(s => s.Scalar<int>(Sql.TextOnly("SELECT COUNT(*) FROM sales"))));
        Check(store.Read(s => s.Scalar<int>(Sql.TextOnly("SELECT COUNT(*) FROM sale_requests WHERE request_key=@key").Bind("key", keyValue))) == 0);
    });
    await Test("statistics, revenue and receipt queries are exposed", async () =>
    {
        var stats = await Json<DashboardStats>(await viewer.GetAsync("/api/v1/dashboard/stats")); Check(stats.TodayRevenue == 90 && stats.TodayProfit == 45);
        Check((await Json<List<RevenueDay>>(await viewer.GetAsync("/api/v1/dashboard/revenue?days=7"))).Count == 7);
        Check((await Json<List<Sale>>(await viewer.GetAsync($"/api/v1/sales?from={DateTime.Today:yyyy-MM-dd}&to={DateTime.Today:yyyy-MM-dd}"))).Count == 2);
        Check((await Json<List<Sale>>(await viewer.GetAsync("/api/v1/sales/recent?count=1"))).Count == 1);
    });
    await Test("SQL-looking search remains a literal value", async () =>
    { Check((await Json<List<Product>>(await viewer.GetAsync("/api/v1/products?search=" + Uri.EscapeDataString("'; DROP TABLE products; --")))).Count == 0); });
    await Test("only admin can delete; receipt snapshots survive deletion", async () =>
    {
        await Status(await op.DeleteAsync($"/api/v1/products/{productId}"), HttpStatusCode.Forbidden);
        await Status(await admin.DeleteAsync($"/api/v1/products/{productId}"), HttpStatusCode.NoContent);
        var line = (await Json<List<SaleItem>>(await viewer.GetAsync($"/api/v1/sales/{receipt.Id}/items"))).Single(); Check(line.ProductId == null && line.Price == 30);
    });
    await Test("unknown token and expired token cannot access data", async () =>
    {
        using var fake = Client(new string('A', 43)); await Status(await fake.GetAsync("/api/v1/products"), HttpStatusCode.Unauthorized);
        var expired = await users.Login(new("viewer", "ApiSpec-LongPassword!3"), default);
        var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(expired.AccessToken)));
        store.Run(s => s.Execute(Sql.TextOnly("UPDATE app_sessions SET expires_at=@past WHERE token_hash=@hash").Bind("past", DateTime.UtcNow.AddMinutes(-1)).Bind("hash", hash)));
        using var old = Client(expired.AccessToken); await Status(await old.GetAsync("/api/v1/products"), HttpStatusCode.Unauthorized);
    });
    await Test("logout revokes token on the server", async () =>
    {
        var session = await users.Login(new("viewer", "ApiSpec-LongPassword!3"), default); using var client = Client(session.AccessToken);
        await Status(await client.PostAsync("/api/v1/auth/logout", null), HttpStatusCode.NoContent);
        await Status(await client.GetAsync("/api/v1/products"), HttpStatusCode.Unauthorized);
    });
    await Test("changing password revokes all existing user sessions", async () =>
    {
        await Status(await admin.PutAsJsonAsync($"/api/v1/users/{userViewer.Id}/password", new SetPasswordRequest("Changed-Password!123")), HttpStatusCode.NoContent);
        await Status(await viewer.GetAsync("/api/v1/products"), HttpStatusCode.Unauthorized);
        Check(await users.Login(new("viewer", "ApiSpec-LongPassword!3"), default) == null);
    });
    await Test("disabled account cannot login and existing sessions are revoked", async () =>
    {
        await Status(await admin.PutAsJsonAsync($"/api/v1/users/{userOperator.Id}/active", new SetActiveRequest(false)), HttpStatusCode.NoContent);
        await Status(await op.GetAsync("/api/v1/products"), HttpStatusCode.Unauthorized);
        Check(await users.Login(new("operator", "ApiSpec-LongPassword!2"), default) == null);
    });
    await Test("persistent lockout records five failures without rolling them back", async () =>
    {
        var locked = users.Create(new("locktest", "Locked-Password!123", Roles.Viewer));
        for (var attempt = 0; attempt < 5; attempt++) Check(await users.Login(new("locktest", "wrong"), default) == null);
        Check(await users.Login(new("locktest", "Locked-Password!123"), default) == null);
        var failures = store.Read(s => s.Scalar<int>(Sql.TextOnly("SELECT failed_attempts FROM app_users WHERE id=@id").Bind("id", locked.Id))); Check(failures == 5);
        users.SetPassword(locked.Id, "Unlocked-Password!123"); Check(await users.Login(new("locktest", "Unlocked-Password!123"), default) != null);
    });
    await Test("administrator validates user roles, passwords and self-disable", async () =>
    {
        await Status(await admin.PostAsJsonAsync("/api/v1/users", new CreateUserRequest("newuser", "short", Roles.Viewer)), HttpStatusCode.BadRequest);
        await Status(await admin.PostAsJsonAsync("/api/v1/users", new CreateUserRequest("newuser", "Long-Password!123", "Superuser")), HttpStatusCode.BadRequest);
        await Status(await admin.PutAsJsonAsync($"/api/v1/users/{userAdmin.Id}/active", new SetActiveRequest(false)), HttpStatusCode.BadRequest);
        var created = await Json<UserInfo>(await admin.PostAsJsonAsync("/api/v1/users", new CreateUserRequest("newuser", "Long-Password!123", Roles.Viewer)), HttpStatusCode.Created); Check(created.Role == Roles.Viewer);
        var list = await Json<List<UserInfo>>(await admin.GetAsync("/api/v1/users")); Check(list.Any(u => u.Id == created.Id));
    });
    await Test("standalone server CLI initializes schema and creates user without echoing password", async () =>
    {
        var start = new ProcessStartInfo(Environment.ProcessPath)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(typeof(ServerHost).Assembly.Location); start.ArgumentList.Add("create-user");
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        start.Environment["ConnectionStrings__Default"] = connection;
        start.Environment["Database__CreateDatabase"] = "false";
        using var process = Process.Start(start);
        await process.StandardInput.WriteAsync("cliuser\nViewer\nCLI-Test-Password!123\n"); process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        var text = await output; var errorText = await errors;
        Check(process.ExitCode == 0, errorText); Check(text.Contains("Created cliuser") && !text.Contains("CLI-Test-Password!123"));
        Check((await users.Login(new("cliuser", "CLI-Test-Password!123"), default))?.User.Role == Roles.Viewer);
    });
    await Test("portable client authenticates, calls API, restricts origin and forgets revoked sessions", async () =>
    {
        using var http = Client(); using var api = new InventoryApi(address, http);
        await api.LoginAsync("newuser", "Long-Password!123"); Check(!api.CanWrite);
        Check((await api.GetAsync<DashboardStats>("api/v1/dashboard/stats")).TodaySales == 2);
        var rejected = false; try { await api.GetAsync<object>("https://example.com/"); } catch (ArgumentException) { rejected = true; } Check(rejected);
        await api.LogoutAsync(); Check(api.CurrentUser == null);
        rejected = false; try { await api.GetAsync<object>("api/v1/products"); } catch (ApiException e) { rejected = e.Status == HttpStatusCode.Unauthorized; } Check(rejected);
    });
    await Test("production client rejects HTTP and does not trust the test self-signed certificate", async () =>
    {
        var rejected = false; try { using var api = new InventoryApi("http://localhost"); } catch (ArgumentException) { rejected = true; } Check(rejected);
        using var strict = new InventoryApi(address);
        rejected = false; try { await strict.LoginAsync("admin", "ApiSpec-LongPassword!1"); } catch (HttpRequestException) { rejected = true; } Check(rejected);
    });
    await Test("OpenAPI operations correspond to registered endpoints", async () =>
    {
        using var schema = await Json<JsonDocument>(await admin.GetAsync("/api/v1/openapi.json"));
        var documented = schema.RootElement.GetProperty("paths").EnumerateObject().SelectMany(path => path.Value.EnumerateObject())
            .Select(operation => operation.Value.GetProperty("operationId").GetString()).Order().ToArray();
        var actual = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .Select(endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName).Where(name => name != null).Order().ToArray();
        Check(documented.SequenceEqual(actual), "OpenAPI operations do not match the API.");
    });
    await Test("responses prevent caching and do not expose credential hashes", async () =>
    {
        using var response = await admin.GetAsync("/api/v1/users"); var text = await response.Content.ReadAsStringAsync();
        Check(response.Headers.CacheControl.NoStore && !text.Contains("password", StringComparison.OrdinalIgnoreCase) && !text.Contains("token", StringComparison.OrdinalIgnoreCase));
    });
    await Test("login endpoint applies per-IP rate limiting", async () =>
    {
        var limited = false;
        for (var attempt = 0; attempt < 12; attempt++)
        {
            using var response = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("missing", "wrong"));
            if (response.StatusCode == HttpStatusCode.TooManyRequests) { limited = true; Check(response.Headers.RetryAfter != null); break; }
        }
        Check(limited);
    });
    await app.StopAsync();
    Console.WriteLine($"{checks} HTTPS/API checks passed against isolated MySQL.");
}
finally
{
    if (!databaseName.StartsWith("easydo_test_", StringComparison.Ordinal) || databaseName.Length != 44) throw new InvalidOperationException("Unsafe cleanup target.");
    server.Run(s => s.Execute(Sql.TextOnly($"DROP DATABASE `{databaseName}`")));
}
