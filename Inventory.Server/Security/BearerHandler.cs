using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Inventory.Server.Security;

public sealed class BearerHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, UserStore users)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "StockFlowBearer";
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return AuthenticateResult.NoResult();
        var user = await users.Authenticate(authorization[7..], Context.RequestAborted);
        if (user == null) return AuthenticateResult.Fail("Invalid or expired session.");
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username), new Claim(ClaimTypes.Role, user.Role)], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401; Response.Headers.WWWAuthenticate = "Bearer";
        return Response.WriteAsJsonAsync(new Inventory.Contracts.ApiProblem("about:blank", "Unauthorized", 401, "Войдите в систему.", Context.TraceIdentifier));
    }
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 403;
        return Response.WriteAsJsonAsync(new Inventory.Contracts.ApiProblem("about:blank", "Forbidden", 403, "Недостаточно прав.", Context.TraceIdentifier));
    }
}
