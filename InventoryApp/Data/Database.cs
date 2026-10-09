using System.Text.Json;
using MySqlConnector;

namespace InventoryApp.Data;

/// <summary>Подключение к MySQL, автосоздание схемы и демо-данных.</summary>
public static class Database
{
    private static string _connectionString;
    private static bool _seedDemo = true;

    public static string ConnectionString => _connectionString ??= LoadConnectionString();

    private static string LoadConnectionString()
    {
        const string fallback = "Server=localhost;Port=3306;Database=inventory_db;User ID=root;Password=;CharSet=utf8mb4;";
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path)) return fallback;

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        if (root.TryGetProperty("SeedDemoData", out var seed)) _seedDemo = seed.GetBoolean();
        return root.TryGetProperty("ConnectionStrings", out var cs) && cs.TryGetProperty("Default", out var def)
            ? def.GetString()
            : fallback;
    }

    public static MySqlConnection Open()
    {
        var conn = new MySqlConnection(ConnectionString);
        conn.Open();
        return conn;
    }

    public static readonly string[] Schema =
    {
        @"CREATE TABLE IF NOT EXISTS products (
            id             INT AUTO_INCREMENT PRIMARY KEY,
            sku            VARCHAR(50)   NOT NULL UNIQUE,
            name           VARCHAR(200)  NOT NULL,
            category       VARCHAR(100)  NULL,
            purchase_price DECIMAL(12,2) NOT NULL DEFAULT 0,
            price          DECIMAL(12,2) NOT NULL DEFAULT 0,
            quantity       INT           NOT NULL DEFAULT 0,
            min_quantity   INT           NOT NULL DEFAULT 0,
            created_at     DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
            updated_at     DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
            INDEX ix_products_name (name),
            INDEX ix_products_category (category)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",

        @"CREATE TABLE IF NOT EXISTS sales (
            id         INT AUTO_INCREMENT PRIMARY KEY,
            created_at DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
            total      DECIMAL(12,2) NOT NULL,
            INDEX ix_sales_created (created_at)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",

        @"CREATE TABLE IF NOT EXISTS sale_items (
            id           INT AUTO_INCREMENT PRIMARY KEY,
            sale_id      INT           NOT NULL,
            product_id   INT           NULL,
            product_name VARCHAR(200)  NOT NULL,
            quantity     INT           NOT NULL,
            price        DECIMAL(12,2) NOT NULL,
            cost_price   DECIMAL(12,2) NOT NULL DEFAULT 0,
            CONSTRAINT fk_items_sale    FOREIGN KEY (sale_id)    REFERENCES sales(id)    ON DELETE CASCADE,
            CONSTRAINT fk_items_product FOREIGN KEY (product_id) REFERENCES products(id) ON DELETE SET NULL
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",

        @"CREATE TABLE IF NOT EXISTS price_history (
            id         INT AUTO_INCREMENT PRIMARY KEY,
            product_id INT           NOT NULL,
            old_price  DECIMAL(12,2) NOT NULL,
            new_price  DECIMAL(12,2) NOT NULL,
            changed_at DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
            CONSTRAINT fk_price_product FOREIGN KEY (product_id) REFERENCES products(id) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;"
    };

    /// <summary>Создаёт базу данных и таблицы, если их нет.</summary>
    public static void EnsureCreated()
    {
        var builder = new MySqlConnectionStringBuilder(ConnectionString);
        var dbName = builder.Database;
        builder.Database = "";

        using (var server = new MySqlConnection(builder.ConnectionString))
        {
            server.Open();
            using var cmd = new MySqlCommand(
                $"CREATE DATABASE IF NOT EXISTS `{dbName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;", server);
            cmd.ExecuteNonQuery();
        }

        using var conn = Open();
        foreach (var sql in Schema)
        {
            using var cmd = new MySqlCommand(sql, conn);
            cmd.ExecuteNonQuery();
        }

        if (_seedDemo) SeedIfEmpty(conn);
    }

    private static void SeedIfEmpty(MySqlConnection conn)
    {
        using (var check = new MySqlCommand("SELECT COUNT(*) FROM products", conn))
            if (Convert.ToInt32(check.ExecuteScalar()) > 0) return;

        var products = new (string Sku, string Name, string Cat, decimal Buy, decimal Sell, int Qty, int Min)[]
        {
            ("PRD-001", "Молоко «Ак-Сүт» 3,2% 1 л",         "Молочные",        62,  79, 48, 10),
            ("PRD-002", "Кефир 2,5% 0,9 л",                 "Молочные",        55,  72,  6, 10),
            ("PRD-003", "Сыр «Российский» 1 кг",            "Молочные",       520, 690, 14,  5),
            ("PRD-004", "Хлеб белый «Нан»",                 "Хлеб",            22,  30, 35, 15),
            ("PRD-005", "Лепёшка тандырная",                "Хлеб",            25,  35,  4, 10),
            ("PRD-006", "Чай «Пиала» чёрный 100 пак.",      "Бакалея",        180, 245, 26,  8),
            ("PRD-007", "Сахар 1 кг",                       "Бакалея",         78,  95, 60, 20),
            ("PRD-008", "Рис «Девзира» 1 кг",               "Бакалея",        160, 210,  3, 10),
            ("PRD-009", "Масло подсолнечное 1 л",           "Бакалея",        135, 170, 22, 10),
            ("PRD-010", "Вода «Легенда» 1,5 л",             "Напитки",         30,  45, 80, 24),
            ("PRD-011", "Coca-Cola 1 л",                    "Напитки",         65,  90,  0, 12),
            ("PRD-012", "Сок «Gracio» 1 л",                 "Напитки",         95, 130, 18,  8),
            ("PRD-013", "Шоколад «Alpen Gold» 90 г",        "Сладости",        70,  99, 40, 15),
            ("PRD-014", "Печенье «Юбилейное»",              "Сладости",        48,  65,  9, 12),
            ("PRD-015", "Мыло «Dove» 100 г",                "Бытовая химия",   85, 120, 15,  6),
            ("PRD-016", "Стиральный порошок «Ariel» 3 кг",  "Бытовая химия",  690, 890,  7,  4),
        };

        using var tx = conn.BeginTransaction();
        var ids = new List<int>();
        foreach (var p in products)
        {
            using var cmd = new MySqlCommand(
                @"INSERT INTO products (sku, name, category, purchase_price, price, quantity, min_quantity)
                  VALUES (@sku, @name, @cat, @buy, @sell, @qty, @min)", conn, tx);
            cmd.Parameters.AddWithValue("@sku", p.Sku);
            cmd.Parameters.AddWithValue("@name", p.Name);
            cmd.Parameters.AddWithValue("@cat", p.Cat);
            cmd.Parameters.AddWithValue("@buy", p.Buy);
            cmd.Parameters.AddWithValue("@sell", p.Sell);
            cmd.Parameters.AddWithValue("@qty", p.Qty);
            cmd.Parameters.AddWithValue("@min", p.Min);
            cmd.ExecuteNonQuery();
            ids.Add((int)cmd.LastInsertedId);
        }

        // Демо-история продаж за последние 7 дней (для графика на главной)
        var rnd = new Random(42);
        for (int day = 6; day >= 0; day--)
        {
            int salesCount = rnd.Next(4, 9);
            for (int s = 0; s < salesCount; s++)
            {
                var date = DateTime.Today.AddDays(-day).AddHours(rnd.Next(9, 20)).AddMinutes(rnd.Next(0, 60));
                if (date > DateTime.Now) date = DateTime.Now.AddMinutes(-rnd.Next(5, 120));

                var lines = Enumerable.Range(0, rnd.Next(1, 4))
                    .Select(_ => rnd.Next(products.Length)).Distinct()
                    .Select(i => (Index: i, Qty: rnd.Next(1, 4))).ToList();
                decimal total = lines.Sum(l => products[l.Index].Sell * l.Qty);

                using var saleCmd = new MySqlCommand("INSERT INTO sales (created_at, total) VALUES (@d, @t)", conn, tx);
                saleCmd.Parameters.AddWithValue("@d", date);
                saleCmd.Parameters.AddWithValue("@t", total);
                saleCmd.ExecuteNonQuery();
                var saleId = (int)saleCmd.LastInsertedId;

                foreach (var l in lines)
                {
                    var p = products[l.Index];
                    using var itemCmd = new MySqlCommand(
                        @"INSERT INTO sale_items (sale_id, product_id, product_name, quantity, price, cost_price)
                          VALUES (@s, @p, @n, @q, @pr, @c)", conn, tx);
                    itemCmd.Parameters.AddWithValue("@s", saleId);
                    itemCmd.Parameters.AddWithValue("@p", ids[l.Index]);
                    itemCmd.Parameters.AddWithValue("@n", p.Name);
                    itemCmd.Parameters.AddWithValue("@q", l.Qty);
                    itemCmd.Parameters.AddWithValue("@pr", p.Sell);
                    itemCmd.Parameters.AddWithValue("@c", p.Buy);
                    itemCmd.ExecuteNonQuery();
                }
            }
        }

        // Демо-история изменения цен
        using (var ph = new MySqlCommand(
            @"INSERT INTO price_history (product_id, old_price, new_price, changed_at) VALUES
              (@a, 72, 79, NOW() - INTERVAL 5 DAY),
              (@b, 230, 245, NOW() - INTERVAL 3 DAY),
              (@c, 95, 90, NOW() - INTERVAL 1 DAY)", conn, tx))
        {
            ph.Parameters.AddWithValue("@a", ids[0]);
            ph.Parameters.AddWithValue("@b", ids[5]);
            ph.Parameters.AddWithValue("@c", ids[10]);
            ph.ExecuteNonQuery();
        }

        tx.Commit();
    }
}
