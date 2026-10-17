namespace Inventory.Contracts;

public static class Roles
{
    public const string Admin = "Admin", Operator = "Operator", Viewer = "Viewer";
    public static bool IsValid(string role) => role is Admin or Operator or Viewer;
}
public sealed record LoginRequest(string Username, string Password);
public sealed record UserInfo(int Id, string Username, string Role);
public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, UserInfo User, string TokenType = "Bearer");
public sealed record CreateUserRequest(string Username, string Password, string Role);
public sealed record SetPasswordRequest(string Password);
public sealed record SetActiveRequest(bool Active);
public sealed record StockRequest(int Quantity);
public sealed record SaleLineRequest(int ProductId, int Quantity);
public sealed record CreateSaleRequest(List<SaleLineRequest> Items);
public sealed record RevenueDay(DateTime Day, decimal Total);
public sealed record IdResponse(int Id);
public sealed record ExistsResponse(bool Exists);
public sealed record ApiProblem(string Type, string Title, int Status, string Detail, string TraceId);
