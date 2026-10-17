using System.Data.Common;
using Inventory.Contracts;

namespace Inventory.Storage;

public class ProductRepository
{
    private readonly Store _store;
    public ProductRepository(Store store) => _store = store ?? throw new ArgumentNullException(nameof(store));
    private const string SelectSql =
        "SELECT id, sku, name, category, purchase_price, price, quantity, min_quantity, updated_at FROM products";

    public List<Product> GetAll(string search = null, string category = null, bool lowOnly = false, int? limit = null, int offset = 0)
    {
        var text = SelectSql + " WHERE 1=1";
        if (!string.IsNullOrWhiteSpace(search)) text += " AND (name LIKE @s OR sku LIKE @s OR category LIKE @s)";
        if (!string.IsNullOrWhiteSpace(category)) text += " AND category = @c";
        if (lowOnly) text += " AND quantity <= min_quantity";
        var sql = Sql.TextOnly(text + " ORDER BY name, id" + (limit.HasValue ? " LIMIT @limit OFFSET @offset" : ""));
        if (limit.HasValue) sql = sql.Bind("limit", limit.Value).Bind("offset", offset);
        if (!string.IsNullOrWhiteSpace(search)) sql = sql.Bind("s", "%" + search.Trim() + "%");
        if (!string.IsNullOrWhiteSpace(category)) sql = sql.Bind("c", category);
        return _store.Read(s => s.Query(sql, Map));
    }

    public List<string> GetCategories() => _store.Read(s => s.Query(Sql.TextOnly(
        "SELECT DISTINCT category FROM products WHERE category IS NOT NULL AND category <> '' ORDER BY category"), r => r.GetString(0)));

    public bool SkuExists(string sku, int excludeId) => _store.Read(s => s.Scalar<int>(Sql.TextOnly(
        "SELECT COUNT(*) FROM products WHERE sku = @sku AND id <> @id").Bind("sku", sku).Bind("id", excludeId))) > 0;

    public int Insert(Product product)
    {
        ProductRules.Valid.Ensure(product);
        return _store.Read(s =>
        {
            s.Execute(Fields(Sql.TextOnly("""
                INSERT INTO products (sku, name, category, purchase_price, price, quantity, min_quantity)
                VALUES (@sku, @name, @cat, @buy, @price, @qty, @min)
                """), product));
            return s.Scalar<int>(Sql.TextOnly("SELECT LAST_INSERT_ID()"));
        });
    }

    public void Update(Product product, bool checkVersion = false)
    {
        ProductRules.Valid.Ensure(product);
        _store.Transaction(s =>
        {
            var current = s.Query(Sql.TextOnly(SelectSql + " WHERE id = @id FOR UPDATE").Bind("id", product.Id), Map).SingleOrDefault()
                ?? throw new MissingEntityException("Товар не найден.");
            if (checkVersion && (current.UpdatedAt != product.UpdatedAt))
                throw new ConflictException("Товар изменился. Обновите список перед сохранением.");
            var oldPrice = current.Price;
            s.Execute(Fields(Sql.TextOnly("""
                UPDATE products SET sku=@sku, name=@name, category=@cat, purchase_price=@buy,
                    price=@price, quantity=@qty, min_quantity=@min,
                    updated_at=GREATEST(CURRENT_TIMESTAMP, updated_at + INTERVAL 1 SECOND) WHERE id=@id
                """), product).Bind("id", product.Id));
            if (oldPrice != product.Price)
                s.Execute(Sql.TextOnly("INSERT INTO price_history (product_id, old_price, new_price) VALUES (@id, @old, @new)")
                    .Bind("id", product.Id).Bind("old", oldPrice).Bind("new", product.Price));
        });
    }

    public Product Get(int id) => _store.Read(s => s.Query(Sql.TextOnly(SelectSql + " WHERE id=@id").Bind("id", id), Map).SingleOrDefault())
        ?? throw new MissingEntityException("Товар не найден.");

    public void Delete(int id) => _store.Run(s =>
    {
        if (s.Execute(Sql.TextOnly("DELETE FROM products WHERE id = @id").Bind("id", id)) == 0)
            throw new MissingEntityException("Товар не найден.");
    });

    public void AddStock(int id, int quantity)
    {
        if (quantity is < 1 or > 1_000_000) throw new ArgumentException("Количество: 1–1000000.");
        _store.Transaction(s =>
        {
            var stock = s.Scalar<int?>(Sql.TextOnly("SELECT quantity FROM products WHERE id=@id FOR UPDATE").Bind("id", id))
                ?? throw new MissingEntityException("Товар не найден.");
            if (stock > 1_000_000 - quantity) throw new ConflictException("Остаток не может превышать 1000000.");
            s.Execute(Sql.TextOnly("UPDATE products SET quantity = quantity + @q, updated_at=GREATEST(CURRENT_TIMESTAMP, updated_at + INTERVAL 1 SECOND) WHERE id = @id")
                .Bind("q", quantity).Bind("id", id));
        });
    }

    public List<PriceChange> GetPriceHistory(int productId) => _store.Read(s => s.Query(Sql.TextOnly(
        "SELECT changed_at, old_price, new_price FROM price_history WHERE product_id = @id ORDER BY changed_at DESC")
        .Bind("id", productId), r => new PriceChange { ChangedAt = r.GetDateTime(0), OldPrice = r.GetDecimal(1), NewPrice = r.GetDecimal(2) }));

    private static Sql Fields(Sql sql, Product p) => sql
        .Bind("sku", p.Sku.Trim()).Bind("name", p.Name.Trim())
        .Bind("cat", string.IsNullOrWhiteSpace(p.Category) ? null : p.Category.Trim())
        .Bind("buy", p.PurchasePrice).Bind("price", p.Price).Bind("qty", p.Quantity).Bind("min", p.MinQuantity);

    private static Product Map(DbDataReader r) => new()
    {
        Id = r.GetInt32(0), Sku = r.GetString(1), Name = r.GetString(2), Category = r.IsDBNull(3) ? "" : r.GetString(3),
        PurchasePrice = r.GetDecimal(4), Price = r.GetDecimal(5), Quantity = r.GetInt32(6),
        MinQuantity = r.GetInt32(7), UpdatedAt = r.GetDateTime(8)
    };
}
