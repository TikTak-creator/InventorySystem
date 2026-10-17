using Inventory.Contracts;
using Inventory.Server;
using Inventory.Server.Security;
using Inventory.Storage;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory });
if (args.Length > 0 && args[0] is "init-db" or "create-user")
{
    var database = new Database(ServerHost.ValidateConnection(builder), builder.Configuration.GetValue("Database:SeedDemoData", false));
    database.EnsureCreated(builder.Configuration.GetValue("Database:CreateDatabase", false));
    var users = new UserStore(database.Store, new PasswordService(), TimeProvider.System, builder.Configuration);
    users.Initialize();
    if (args[0] == "create-user")
    {
        Console.Write("Login: "); var username = Console.ReadLine();
        Console.Write("Role (Admin/Operator/Viewer): "); var role = Console.ReadLine();
        Console.Write("Password (12–128 characters): ");
        string password;
        if (Console.IsInputRedirected) password = Console.ReadLine();
        else
        {
            var chars = new List<char>();
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter) break;
                if (key.Key == ConsoleKey.Backspace) { if (chars.Count > 0) chars.RemoveAt(chars.Count - 1); }
                else if (!char.IsControl(key.KeyChar)) chars.Add(key.KeyChar);
            }
            Console.WriteLine(); password = new string(chars.ToArray()); chars.Clear();
        }
        var user = users.Create(new CreateUserRequest(username, password, role));
        Console.WriteLine($"Created {user.Username} ({user.Role}), ID={user.Id}.");
    }
    else Console.WriteLine("Database schema initialized.");
    return;
}
await using var app = ServerHost.Build(builder);
await app.RunAsync();
