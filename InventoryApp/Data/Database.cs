using System.Text.Json;
using MySqlConnector;

namespace InventoryApp.Data;

/// <summary>MySQL is a StockFlow adapter. EasyDo only requires a DbConnection factory.</summary>
public static class Database
{
    private sealed record Settings(string ConnectionString, bool SeedDemo);
    private static readonly Lazy<Settings> Config = new(LoadSettings);
    private static readonly Lazy<Store> Storage = new(() => new Store(() => new MySqlConnection(ConnectionString)));
    public static string ConnectionString => Config.Value.ConnectionString;
    public static Store Store => Storage.Value;
    public static readonly string[] Schema = StockSchema.Schema;

    private static Settings LoadSettings()
    {
        const string fallback = "Server=localhost;Port=3306;Database=inventory_db;User ID=root;Password=;CharSet=utf8mb4;";
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path)) return new(fallback, true);
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        var connection = root.TryGetProperty("ConnectionStrings", out var cs) && cs.TryGetProperty("Default", out var def)
            ? def.GetString() ?? fallback : fallback;
        return new(connection, !root.TryGetProperty("SeedDemoData", out var seed) || seed.GetBoolean());
    }

    public static void EnsureCreated()
    {
        var builder = new MySqlConnectionStringBuilder(ConnectionString);
        var database = builder.Database;
        if (string.IsNullOrWhiteSpace(database)) throw new InvalidOperationException("В строке подключения не указана база данных.");
        builder.Database = "";
        new Store(() => new MySqlConnection(builder.ConnectionString)).Run(s => s.Execute(Sql.TextOnly(
            $"CREATE DATABASE IF NOT EXISTS `{database.Replace("`", "``")}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;")));
        Store.Run(s => { foreach (var ddl in Schema) s.Execute(Sql.TextOnly(ddl)); });
        if (Config.Value.SeedDemo) DemoData.Seed(Store);
    }
}
