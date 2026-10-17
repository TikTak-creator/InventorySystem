using MySqlConnector;

namespace Inventory.Storage;

/// <summary>Only the server owns credentials and schema initialization.</summary>
public sealed class Database
{
    public Store Store { get; }
    public static readonly string[] Schema = StockSchema.Schema;
    private readonly string _connection;
    private readonly bool _seed;
    public Database(string connection, bool seedDemo = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connection);
        _connection = connection; _seed = seedDemo;
        Store = new Store(() => new MySqlConnection(_connection));
    }
    public void EnsureCreated(bool createDatabase = false)
    {
        if (createDatabase)
        {
            var builder = new MySqlConnectionStringBuilder(_connection);
            var name = builder.Database;
            if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Database name is required.");
            builder.Database = "";
            new Store(() => new MySqlConnection(builder.ConnectionString)).Run(s => s.Execute(Sql.TextOnly(
                $"CREATE DATABASE IF NOT EXISTS `{name.Replace("`", "``")}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;")));
        }
        Store.Run(s => { foreach (var ddl in Schema) s.Execute(Sql.TextOnly(ddl)); });
        if (_seed) DemoData.Seed(Store);
    }
}
