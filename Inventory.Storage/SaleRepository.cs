using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Inventory.Contracts;

namespace Inventory.Storage;

public class SaleRepository
{
    private readonly Store _store;
    public SaleRepository(Store store) => _store = store ?? throw new ArgumentNullException(nameof(store));

    // Reusable steps run inside one transaction. Any failure rolls back the whole recipe.
    private static readonly Flow<Checkout> SaleRecipe = Flow<Checkout>.Start()
        .Then(LockAndCheckStock)
        .Then(SaveReceipt)
        .Then(SaveItemsAndDeductStock);

    public int CreateSale(IList<CartItem> items) => CheckoutSale(items).Id;

    public SaleReceipt CheckoutSale(IList<CartItem> items, string requestKey = null, int userId = 0)
    {
        if (items == null || items.Count == 0) throw new ConflictException("Корзина пуста.");
        if (items.Any(i => i?.Product == null || i.Quantity <= 0))
            throw new ConflictException("Количество товара должно быть больше нуля.");
        return _store.Transaction(s =>
        {
            if (requestKey == null) return SaleRecipe.Run(new Checkout(s, items)).Receipt;
            if (userId <= 0 || !Guid.TryParseExact(requestKey, "D", out _)) throw new ArgumentException("Invalid idempotency key.");
            var canonical = string.Join(";", items.GroupBy(i => i.Product.Id).OrderBy(g => g.Key).Select(g => $"{g.Key}:{g.Sum(i => i.Quantity)}"));
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
            s.Execute(Sql.TextOnly("""
                INSERT INTO sale_requests (user_id, request_key, request_hash) VALUES (@user, @key, @hash)
                ON DUPLICATE KEY UPDATE request_key=request_key
                """).Bind("user", userId).Bind("key", requestKey).Bind("hash", hash));
            var prior = s.Query(Sql.TextOnly("SELECT request_hash, sale_id FROM sale_requests WHERE user_id=@user AND request_key=@key FOR UPDATE")
                .Bind("user", userId).Bind("key", requestKey), r => (Hash: r.GetString(0), Sale: r.IsDBNull(1) ? (int?)null : r.GetInt32(1))).Single();
            if (prior.Hash != hash) throw new ConflictException("Этот ключ запроса уже использован для другой корзины.");
            if (prior.Sale.HasValue) return s.Query(Sql.TextOnly("SELECT id, total FROM sales WHERE id=@id").Bind("id", prior.Sale.Value),
                r => new SaleReceipt(r.GetInt32(0), r.GetDecimal(1))).Single();
            var receipt = SaleRecipe.Run(new Checkout(s, items)).Receipt;
            s.Execute(Sql.TextOnly("UPDATE sale_requests SET sale_id=@sale WHERE user_id=@user AND request_key=@key")
                .Bind("sale", receipt.Id).Bind("user", userId).Bind("key", requestKey));
            return receipt;
        });
    }

    private static void LockAndCheckStock(Checkout sale)
    {
        // Aggregate repeated products and lock in the same order on every cash register.
        var groups = sale.Items.GroupBy(i => i.Product.Id).ToList();
        var locked = new Dictionary<int, SaleLine>();
        foreach (var group in groups.OrderBy(g => g.Key))
        {
            var quantity = checked(group.Sum(i => i.Quantity));
            var line = sale.Session.Query(Sql.TextOnly(
                "SELECT name, quantity, price, purchase_price FROM products WHERE id = @id FOR UPDATE")
                .Bind("id", group.Key), r => new SaleLine(group.Key, r.GetString(0), quantity, r.GetDecimal(2), r.GetDecimal(3), r.GetInt32(1)))
                .SingleOrDefault();
            if (line == null) throw new MissingEntityException($"Товар «{group.First().Name}» не найден (возможно, удалён).");
            if (line.Stock < line.Quantity)
                throw new ConflictException($"Недостаточно товара «{line.Name}»: в наличии {line.Stock}, требуется {line.Quantity}.");
            locked.Add(line.Id, line);
        }
        // The receipt still lists products in the order chosen by the cashier.
        sale.Lines.AddRange(groups.Select(group => locked[group.Key]));
    }

    private static void SaveReceipt(Checkout sale)
    {
        var total = sale.Lines.Sum(l => l.Quantity * l.Price);
        if (total > 9_999_999_999.99m) throw new ConflictException("Сумма чека превышает предел хранения.");
        sale.Session.Execute(Sql.TextOnly("INSERT INTO sales (created_at, total) VALUES (NOW(), @t)").Bind("t", total));
        sale.Receipt = new(sale.Session.Scalar<int>(Sql.TextOnly("SELECT LAST_INSERT_ID()")), total);
    }

    private static void SaveItemsAndDeductStock(Checkout sale)
    {
        foreach (var line in sale.Lines)
        {
            sale.Session.Execute(Sql.TextOnly("""
                INSERT INTO sale_items (sale_id, product_id, product_name, quantity, price, cost_price)
                VALUES (@s, @p, @n, @q, @pr, @c)
                """).Bind("s", sale.Receipt.Id).Bind("p", line.Id).Bind("n", line.Name)
                .Bind("q", line.Quantity).Bind("pr", line.Price).Bind("c", line.Cost));
            sale.Session.Execute(Sql.TextOnly("UPDATE products SET quantity = quantity - @q, updated_at=GREATEST(CURRENT_TIMESTAMP, updated_at + INTERVAL 1 SECOND) WHERE id = @id")
                .Bind("q", line.Quantity).Bind("id", line.Id));
        }
    }

    private const string SelectSales = """
        SELECT s.id, s.created_at, s.total, COALESCE(SUM(i.quantity), 0)
        FROM sales s LEFT JOIN sale_items i ON i.sale_id = s.id
        """;
    private const string GroupSales = " GROUP BY s.id, s.created_at, s.total ORDER BY s.created_at DESC";
    public List<Sale> GetSales(DateTime from, DateTime to, int? limit = null, int offset = 0) => _store.Read(s =>
    {
        var sql = Sql.TextOnly(SelectSales + " WHERE s.created_at >= @f AND s.created_at < @t" + GroupSales + (limit.HasValue ? " LIMIT @limit OFFSET @offset" : ""))
            .Bind("f", from.Date).Bind("t", to.Date.AddDays(1));
        if (limit.HasValue) sql = sql.Bind("limit", limit.Value).Bind("offset", offset);
        return s.Query(sql, MapSale);
    });

    public List<Sale> GetRecent(int count) => _store.Read(s => s.Query(Sql.TextOnly(SelectSales + GroupSales + " LIMIT @n").Bind("n", count), MapSale));

    public List<SaleItem> GetItems(int saleId) => _store.Read(s => s.Query(Sql.TextOnly(
        "SELECT id, sale_id, product_id, product_name, quantity, price, cost_price FROM sale_items WHERE sale_id = @id ORDER BY id")
        .Bind("id", saleId), r => new SaleItem
        {
            Id = r.GetInt32(0), SaleId = r.GetInt32(1), ProductId = r.IsDBNull(2) ? null : r.GetInt32(2),
            ProductName = r.GetString(3), Quantity = r.GetInt32(4), Price = r.GetDecimal(5), CostPrice = r.GetDecimal(6)
        }));

    public DashboardStats GetStats() => _store.Read(session =>
    {
        var stats = session.Query(Sql.TextOnly("""
            SELECT COUNT(*), COALESCE(SUM(quantity <= min_quantity), 0),
                COALESCE(SUM(quantity), 0), COALESCE(SUM(quantity * purchase_price), 0) FROM products
            """), r => new DashboardStats
        {
            ProductsCount = Convert.ToInt32(r.GetValue(0)), LowStockCount = Convert.ToInt32(r.GetValue(1)),
            UnitsInStock = Convert.ToInt32(r.GetValue(2)), StockValue = Convert.ToDecimal(r.GetValue(3))
        }).Single();
        var today = session.Query(Sql.TextOnly(
            "SELECT COUNT(*), COALESCE(SUM(total), 0) FROM sales WHERE created_at >= CURDATE()"),
            r => (Count: Convert.ToInt32(r.GetValue(0)), Revenue: Convert.ToDecimal(r.GetValue(1)))).Single();
        stats.TodaySales = today.Count;
        stats.TodayRevenue = today.Revenue;
        stats.TodayProfit = session.Scalar<decimal>(Sql.TextOnly("""
            SELECT COALESCE(SUM(i.quantity * (i.price - i.cost_price)), 0)
            FROM sale_items i JOIN sales s ON s.id = i.sale_id WHERE s.created_at >= CURDATE()
            """));
        return stats;
    });

    public List<(DateTime Day, decimal Total)> GetRevenueByDay(int days)
    {
        if (days <= 0) throw new ArgumentOutOfRangeException(nameof(days));
        var map = _store.Read(s => s.Query(Sql.TextOnly("""
            SELECT DATE(created_at) d, SUM(total) FROM sales
            WHERE created_at >= CURDATE() - INTERVAL @n DAY GROUP BY DATE(created_at)
            """).Bind("n", days - 1), r => (Day: r.GetDateTime(0).Date, Total: r.GetDecimal(1))))
            .ToDictionary(d => d.Day, d => d.Total);
        return Enumerable.Range(0, days).Select(i => DateTime.Today.AddDays(i - days + 1))
            .Select(day => (day, map.TryGetValue(day, out var value) ? value : 0m)).ToList();
    }

    private static Sale MapSale(DbDataReader r) => new()
    {
        Id = r.GetInt32(0), CreatedAt = r.GetDateTime(1), Total = r.GetDecimal(2), ItemsCount = Convert.ToInt32(r.GetValue(3))
    };
    private sealed record SaleLine(int Id, string Name, int Quantity, decimal Price, decimal Cost, int Stock);
    private sealed class Checkout(Session session, IList<CartItem> items)
    {
        public Session Session { get; } = session;
        public IList<CartItem> Items { get; } = items;
        public List<SaleLine> Lines { get; } = new();
        public SaleReceipt Receipt { get; set; }
    }
}
