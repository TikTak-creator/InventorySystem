using System.Security.Claims;
using System.Threading.RateLimiting;
using System.Text.Json;
using System.Security.Authentication;
using EasyDo;
using EasyDo.Data;
using Inventory.Contracts;
using Inventory.Server.Security;
using Inventory.Storage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using MySqlConnector;

namespace Inventory.Server;

public static class ServerHost
{
    public static WebApplication Build(WebApplicationBuilder builder)
    {
        var connection = ValidateConnection(builder);
        var minutes = builder.Configuration.GetValue("Security:SessionMinutes", 60);
        if (minutes is < 5 or > 480) throw new InvalidOperationException("SessionMinutes must be between 5 and 480.");
        var urls = builder.Configuration["urls"] ?? "https://localhost:7071";
        if (urls.Split(';').Any(url => !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("The API must listen over HTTPS only.");
        if (builder.Configuration.GetSection("Kestrel:Endpoints").GetChildren().Any(endpoint =>
            endpoint["Url"] is { } url && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("All configured Kestrel endpoints must use HTTPS.");
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = 65_536;
            options.ConfigureHttpsDefaults(https => https.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13);
        });
        builder.Services.AddSingleton(new Database(connection, builder.Configuration.GetValue("Database:SeedDemoData", false)));
        builder.Services.AddSingleton(sp => sp.GetRequiredService<Database>().Store);
        builder.Services.AddSingleton<ProductRepository>(); builder.Services.AddSingleton<SaleRepository>();
        builder.Services.AddSingleton<PasswordService>(); builder.Services.AddSingleton<UserStore>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddAuthentication(BearerHandler.SchemeName).AddScheme<AuthenticationSchemeOptions, BearerHandler>(BearerHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            options.AddPolicy("write", policy => policy.RequireRole(Roles.Admin, Roles.Operator));
            options.AddPolicy("admin", policy => policy.RequireRole(Roles.Admin));
        });
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow);
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ =>
                    new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ =>
                new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.OnRejected = async (context, cancellation) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";
                await context.HttpContext.Response.WriteAsJsonAsync(new ApiProblem("about:blank", "Too Many Requests", 429,
                    "Слишком много запросов. Повторите позже.", context.HttpContext.TraceIdentifier), cancellation);
            };
        });
        var app = builder.Build();
        if (builder.Configuration.GetValue("Database:InitializeSchema", false))
        {
            app.Services.GetRequiredService<Database>().EnsureCreated(builder.Configuration.GetValue("Database:CreateDatabase", false));
            app.Services.GetRequiredService<UserStore>().Initialize();
        }
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            if (!context.Request.IsHttps)
            { await Problem(context, 400, "HTTPS обязателен."); return; }
            try { await next(); }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
            catch (Exception error) when (!context.Response.HasStarted)
            {
                var (status, message) = error switch
                {
                    MissingEntityException => (404, error.Message),
                    ConflictException => (409, error.Message),
                    RuleException => (400, error.Message),
                    ArgumentException => (400, error.Message),
                    BadHttpRequestException bad => (bad.StatusCode, "Некорректный запрос."),
                    JsonException => (400, "Некорректный JSON."),
                    MySqlException { Number: 1062 } => (409, "Запись с таким идентификатором уже существует."),
                    _ => (500, "Ошибка сервера. Сообщите идентификатор запроса администратору.")
                };
                if (status == 500) app.Logger.LogError("Request {TraceId} failed: {ErrorType}", context.TraceIdentifier, error.GetType().Name);
                await Problem(context, status, message);
            }
        });
        if (!app.Environment.IsDevelopment()) app.UseHsts();
        app.UseRouting(); app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization();
        app.Use(async (context, next) =>
        {
            await next();
            app.Logger.LogInformation("API {Method} {Path} => {Status}; user={UserId}; trace={TraceId}", context.Request.Method,
                context.Request.Path.Value, context.Response.StatusCode, context.User.FindFirstValue(ClaimTypes.NameIdentifier), context.TraceIdentifier);
        });
        MapEndpoints(app);
        return app;
    }
    public static string ValidateConnection(WebApplicationBuilder builder)
    {
        var connection = builder.Configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Configure ConnectionStrings:Default on the server.");
        var mysql = new MySqlConnectionStringBuilder(connection);
        if (string.IsNullOrWhiteSpace(mysql.Database)) throw new InvalidOperationException("Configure the MySQL database name.");
        if (!builder.Environment.IsDevelopment() && mysql.SslMode != MySqlSslMode.VerifyFull)
            throw new InvalidOperationException("Production MySQL connection requires SslMode=VerifyFull and a trusted CA.");
        return connection;
    }
    private static Task Problem(HttpContext context, int status, string detail)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new ApiProblem("about:blank", "Request failed", status, detail, context.TraceIdentifier));
    }
    private static void Page(int offset, int limit)
    { if (offset < 0 || limit is < 1 or > 200) throw new ArgumentException("offset >= 0; limit: 1–200."); }
    private static void Positive(int id) { if (id <= 0) throw new ArgumentException("ID должен быть положительным."); }
    private static void MapEndpoints(WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous().WithName("Health");
        var api = app.MapGroup("/api/v1").RequireAuthorization();
        api.MapPost("/auth/login", async (LoginRequest request, UserStore users, HttpContext context) =>
        {
            var login = await users.Login(request, context.RequestAborted);
            return login == null ? Results.Problem(statusCode: 401, title: "Неверный логин или пароль.") : Results.Ok(login);
        }).AllowAnonymous().RequireRateLimiting("login").WithName("Login");
        api.MapGet("/auth/me", (ClaimsPrincipal user) => new UserInfo(int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)), user.Identity.Name,
            user.FindFirstValue(ClaimTypes.Role))).WithName("Me");
        api.MapPost("/auth/logout", async (UserStore users, HttpContext context) =>
        { await users.Logout(context.Request.Headers.Authorization.ToString()[7..], context.RequestAborted); return Results.NoContent(); }).WithName("Logout");

        api.MapGet("/products", (ProductRepository products, string search = null, string category = null, bool lowOnly = false, int offset = 0, int limit = 100) =>
        { Page(offset, limit); if (search?.Length > 200 || category?.Length > 100) throw new ArgumentException("Слишком длинный фильтр."); return products.GetAll(search, category, lowOnly, limit, offset); }).WithName("Products");
        api.MapGet("/products/categories", (ProductRepository products) => products.GetCategories()).WithName("Categories");
        api.MapGet("/products/sku-exists", (string sku, ProductRepository products, int excludeId = 0) =>
        { if (sku == null || sku.Length > 50 || excludeId < 0) throw new ArgumentException("Некорректный артикул."); return new ExistsResponse(products.SkuExists(sku, excludeId)); }).WithName("SkuExists");
        api.MapGet("/products/{id:int}", (int id, ProductRepository products) => { Positive(id); return products.Get(id); }).WithName("Product");
        api.MapPost("/products", (Product product, ProductRepository products) =>
        { product.Id = 0; var id = products.Insert(product); return Results.Created($"/api/v1/products/{id}", new IdResponse(id)); }).RequireAuthorization("write").WithName("CreateProduct");
        api.MapPut("/products/{id:int}", (int id, Product product, ProductRepository products) =>
        { Positive(id); if (product.Id != id || product.UpdatedAt == default) throw new ArgumentException("Нужны ID и UpdatedAt полученного товара."); products.Update(product, checkVersion: true); return Results.NoContent(); }).RequireAuthorization("write").WithName("UpdateProduct");
        api.MapDelete("/products/{id:int}", (int id, ProductRepository products) =>
        { Positive(id); products.Delete(id); return Results.NoContent(); }).RequireAuthorization("admin").WithName("DeleteProduct");
        api.MapPost("/products/{id:int}/stock", (int id, StockRequest request, ProductRepository products) =>
        { Positive(id); if (request.Quantity is < 1 or > 1_000_000) throw new ArgumentException("Количество: 1–1000000."); products.AddStock(id, request.Quantity); return Results.NoContent(); }).RequireAuthorization("write").WithName("AddStock");
        api.MapGet("/products/{id:int}/price-history", (int id, ProductRepository products) =>
        { Positive(id); products.Get(id); return products.GetPriceHistory(id); }).WithName("PriceHistory");

        api.MapGet("/sales", (DateTime from, DateTime to, SaleRepository sales, int offset = 0, int limit = 100) =>
        { Page(offset, limit); if (from.Date > to.Date || (to.Date - from.Date).TotalDays > 366 || to.Year >= 9999) throw new ArgumentException("Период: до 366 дней."); return sales.GetSales(from, to, limit, offset); }).WithName("Sales");
        api.MapGet("/sales/recent", (SaleRepository sales, int count = 12) =>
        { if (count is < 1 or > 100) throw new ArgumentException("count: 1–100."); return sales.GetRecent(count); }).WithName("RecentSales");
        api.MapGet("/sales/{id:int}/items", (int id, SaleRepository sales) =>
        { Positive(id); var items = sales.GetItems(id); if (items.Count == 0) throw new MissingEntityException("Чек не найден."); return items; }).WithName("SaleItems");
        api.MapPost("/sales", (CreateSaleRequest request, SaleRepository sales, HttpContext context) =>
        {
            if (request.Items == null || request.Items.Count is < 1 or > 100 || request.Items.Any(i => i == null || i.ProductId <= 0 || i.Quantity is < 1 or > 1_000_000))
                throw new ArgumentException("Корзина: 1–100 позиций, положительные ID и количество до 1000000.");
            var key = context.Request.Headers["Idempotency-Key"].ToString();
            if (!Guid.TryParseExact(key, "D", out _)) throw new ArgumentException("Idempotency-Key должен быть UUID в формате D.");
            var items = request.Items.Select(i => new CartItem { Product = new Product { Id = i.ProductId }, Quantity = i.Quantity }).ToList();
            var receipt = sales.CheckoutSale(items, key, int.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)));
            return Results.Ok(receipt);
        }).RequireAuthorization("write").WithName("CreateSale");
        api.MapGet("/dashboard/stats", (SaleRepository sales) => sales.GetStats()).WithName("Stats");
        api.MapGet("/dashboard/revenue", (SaleRepository sales, int days = 7) =>
        { if (days is < 1 or > 366) throw new ArgumentException("days: 1–366."); return sales.GetRevenueByDay(days).Select(d => new RevenueDay(d.Day, d.Total)); }).WithName("Revenue");

        api.MapGet("/users", (UserStore users) => users.List()).RequireAuthorization("admin").WithName("Users");
        api.MapPost("/users", (CreateUserRequest request, UserStore users) =>
        { var user = users.Create(request); return Results.Created($"/api/v1/users/{user.Id}", user); }).RequireAuthorization("admin").WithName("CreateUser");
        api.MapPut("/users/{id:int}/password", (int id, SetPasswordRequest request, UserStore users) =>
        { Positive(id); users.SetPassword(id, request.Password); return Results.NoContent(); }).RequireAuthorization("admin").WithName("SetPassword");
        api.MapPut("/users/{id:int}/active", (int id, SetActiveRequest request, UserStore users, ClaimsPrincipal principal) =>
        { Positive(id); if (id.ToString() == principal.FindFirstValue(ClaimTypes.NameIdentifier) && !request.Active) throw new ArgumentException("Нельзя отключить собственную учётную запись."); users.SetActive(id, request.Active); return Results.NoContent(); }).RequireAuthorization("admin").WithName("SetActive");
        api.MapGet("/openapi.json", () => Results.File(Path.Combine(AppContext.BaseDirectory, "openapi.json"), "application/json")).WithName("OpenApi");
    }
}
