using InventoryApp.Models;
using MySqlConnector;

namespace InventoryApp.Data;

public class SaleRepository
{
    /// <summary>
    /// Оформляет продажу в одной транзакции: проверяет остатки (с блокировкой строк),
    /// списывает товар со склада и сохраняет чек. Возвращает номер чека.
    /// </summary>
    public int CreateSale(IList<CartItem> items)
    {
        if (items == null || items.Count == 0) throw new InvalidOperationException("Корзина пуста.");

        using var conn = Database.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            var lines = new List<(int Id, string Name, int Qty, decimal Price, decimal Cost)>();
            foreach (var item in items)
            {
                using var cmd = new MySqlCommand(
                    "SELECT name, quantity, price, purchase_price FROM products WHERE id = @id FOR UPDATE", conn, tx);
                cmd.Parameters.AddWithValue("@id", item.Product.Id);
                using var r = cmd.ExecuteReader();
                if (!r.Read()) throw new InvalidOperationException($"Товар «{item.Name}» не найден (возможно, удалён).");

                var name = r.GetString(0);
                var stock = r.GetInt32(1);
                if (stock < item.Quantity)
                    throw new InvalidOperationException($"Недостаточно товара «{name}»: в наличии {stock}, требуется {item.Quantity}.");

                lines.Add((item.Product.Id, name, item.Quantity, r.GetDecimal(2), r.GetDecimal(3)));
            }

            decimal total = lines.Sum(l => l.Qty * l.Price);
            int saleId;
            using (var cmd = new MySqlCommand("INSERT INTO sales (created_at, total) VALUES (NOW(), @t)", conn, tx))
            {
                cmd.Parameters.AddWithValue("@t", total);
                cmd.ExecuteNonQuery();
                saleId = (int)cmd.LastInsertedId;
            }

            foreach (var l in lines)
            {
                using (var cmd = new MySqlCommand(
                    @"INSERT INTO sale_items (sale_id, product_id, product_name, quantity, price, cost_price)
                      VALUES (@s, @p, @n, @q, @pr, @c)", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@s", saleId);
                    cmd.Parameters.AddWithValue("@p", l.Id);
                    cmd.Parameters.AddWithValue("@n", l.Name);
                    cmd.Parameters.AddWithValue("@q", l.Qty);
                    cmd.Parameters.AddWithValue("@pr", l.Price);
                    cmd.Parameters.AddWithValue("@c", l.Cost);
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = new MySqlCommand("UPDATE products SET quantity = quantity - @q WHERE id = @id", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@q", l.Qty);
                    cmd.Parameters.AddWithValue("@id", l.Id);
                    cmd.ExecuteNonQuery();
                }
            }

            tx.Commit();
            return saleId;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public List<Sale> GetSales(DateTime from, DateTime to)
    {
        using var conn = Database.Open();
        using var cmd = new MySqlCommand(
            @"SELECT s.id, s.created_at, s.total, COALESCE(SUM(i.quantity), 0)
              FROM sales s LEFT JOIN sale_items i ON i.sale_id = s.id
              WHERE s.created_at >= @f AND s.created_at < @t
              GROUP BY s.id, s.created_at, s.total
              ORDER BY s.created_at DESC", conn);
        cmd.Parameters.AddWithValue("@f", from.Date);
        cmd.Parameters.AddWithValue("@t", to.Date.AddDays(1));
        return ReadSales(cmd);
    }

    public List<Sale> GetRecent(int count)
    {
        using var conn = Database.Open();
        using var cmd = new MySqlCommand(
            @"SELECT s.id, s.created_at, s.total, COALESCE(SUM(i.quantity), 0)
              FROM sales s LEFT JOIN sale_items i ON i.sale_id = s.id
              GROUP BY s.id, s.created_at, s.total
              ORDER BY s.created_at DESC LIMIT @n", conn);
        cmd.Parameters.AddWithValue("@n", count);
        return ReadSales(cmd);
    }

    public List<SaleItem> GetItems(int saleId)
    {
        using var conn = Database.Open();
        using var cmd = new MySqlCommand(
            "SELECT id, sale_id, product_id, product_name, quantity, price, cost_price FROM sale_items WHERE sale_id = @id", conn);
        cmd.Parameters.AddWithValue("@id", saleId);
        var list = new List<SaleItem>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new SaleItem
            {
                Id = r.GetInt32(0),
                SaleId = r.GetInt32(1),
                ProductId = r.IsDBNull(2) ? null : r.GetInt32(2),
                ProductName = r.GetString(3),
                Quantity = r.GetInt32(4),
                Price = r.GetDecimal(5),
                CostPrice = r.GetDecimal(6)
            });
        return list;
    }

    public DashboardStats GetStats()
    {
        using var conn = Database.Open();
        var s = new DashboardStats();

        using (var cmd = new MySqlCommand(
            @"SELECT COUNT(*), COALESCE(SUM(quantity <= min_quantity), 0),
                     COALESCE(SUM(quantity), 0), COALESCE(SUM(quantity * purchase_price), 0)
              FROM products", conn))
        using (var r = cmd.ExecuteReader())
        {
            r.Read();
            s.ProductsCount = Convert.ToInt32(r.GetValue(0));
            s.LowStockCount = Convert.ToInt32(r.GetValue(1));
            s.UnitsInStock = Convert.ToInt32(r.GetValue(2));
            s.StockValue = Convert.ToDecimal(r.GetValue(3));
        }

        using (var cmd = new MySqlCommand(
            "SELECT COUNT(*), COALESCE(SUM(total), 0) FROM sales WHERE created_at >= CURDATE()", conn))
        using (var r = cmd.ExecuteReader())
        {
            r.Read();
            s.TodaySales = Convert.ToInt32(r.GetValue(0));
            s.TodayRevenue = Convert.ToDecimal(r.GetValue(1));
        }

        using (var cmd = new MySqlCommand(
            @"SELECT COALESCE(SUM(i.quantity * (i.price - i.cost_price)), 0)
              FROM sale_items i JOIN sales s ON s.id = i.sale_id
              WHERE s.created_at >= CURDATE()", conn))
        {
            s.TodayProfit = Convert.ToDecimal(cmd.ExecuteScalar());
        }

        return s;
    }

    /// <summary>Выручка по дням за последние N дней (дни без продаж = 0).</summary>
    public List<(DateTime Day, decimal Total)> GetRevenueByDay(int days)
    {
        using var conn = Database.Open();
        using var cmd = new MySqlCommand(
            @"SELECT DATE(created_at) d, SUM(total) FROM sales
              WHERE created_at >= CURDATE() - INTERVAL @n DAY
              GROUP BY DATE(created_at)", conn);
        cmd.Parameters.AddWithValue("@n", days - 1);

        var map = new Dictionary<DateTime, decimal>();
        using (var r = cmd.ExecuteReader())
            while (r.Read()) map[r.GetDateTime(0).Date] = r.GetDecimal(1);

        return Enumerable.Range(0, days)
            .Select(i => DateTime.Today.AddDays(i - days + 1))
            .Select(d => (d, map.TryGetValue(d, out var v) ? v : 0m))
            .ToList();
    }

    private static List<Sale> ReadSales(MySqlCommand cmd)
    {
        var list = new List<Sale>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new Sale
            {
                Id = r.GetInt32(0),
                CreatedAt = r.GetDateTime(1),
                Total = r.GetDecimal(2),
                ItemsCount = Convert.ToInt32(r.GetValue(3))
            });
        return list;
    }
}
