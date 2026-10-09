using InventoryApp.Models;
using MySqlConnector;

namespace InventoryApp.Data;

public class ProductRepository
{
    private const string SelectSql =
        "SELECT id, sku, name, category, purchase_price, price, quantity, min_quantity, updated_at FROM products";

    public List<Product> GetAll(string search = null, string category = null, bool lowOnly = false)
    {
        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        var sql = SelectSql + " WHERE 1=1";

        if (!string.IsNullOrWhiteSpace(search))
        {
            sql += " AND (name LIKE @s OR sku LIKE @s OR category LIKE @s)";
            cmd.Parameters.AddWithValue("@s", "%" + search.Trim() + "%");
        }
        if (!string.IsNullOrWhiteSpace(category))
        {
            sql += " AND category = @c";
            cmd.Parameters.AddWithValue("@c", category);
        }
        if (lowOnly) sql += " AND quantity <= min_quantity";

        cmd.CommandText = sql + " ORDER BY name";

        var list = new List<Product>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(Map(r));
        return list;
    }

    public List<string> GetCategories()
    {
        using var conn = Database.Open();
        using var cmd = new MySqlCommand(
            "SELECT DISTINCT category FROM products WHERE category IS NOT NULL AND category <> '' ORDER BY category", conn);
        var list = new List<string>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(r.GetString(0));
        return list;
    }

    public bool SkuExists(string sku, int excludeId)
    {
        using var conn = Database.Open();
        using var cmd = new MySqlCommand("SELECT COUNT(*) FROM products WHERE sku = @sku AND id <> @id", conn);
        cmd.Parameters.AddWithValue("@sku", sku);
        cmd.Parameters.AddWithValue("@id", excludeId);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    public int Insert(Product p)
    {
        using var conn = Database.Open();
        using var cmd = new MySqlCommand(
            @"INSERT INTO products (sku, name, category, purchase_price, price, quantity, min_quantity)
              VALUES (@sku, @name, @cat, @buy, @price, @qty, @min)", conn);
        Fill(cmd, p);
        cmd.ExecuteNonQuery();
        return (int)cmd.LastInsertedId;
    }

    /// <summary>Обновляет товар. При изменении цены пишет запись в историю цен.</summary>
    public void Update(Product p)
    {
        using var conn = Database.Open();
        using var tx = conn.BeginTransaction();

        decimal oldPrice;
        using (var get = new MySqlCommand("SELECT price FROM products WHERE id = @id FOR UPDATE", conn, tx))
        {
            get.Parameters.AddWithValue("@id", p.Id);
            oldPrice = Convert.ToDecimal(get.ExecuteScalar());
        }

        using (var cmd = new MySqlCommand(
            @"UPDATE products SET sku=@sku, name=@name, category=@cat, purchase_price=@buy,
                     price=@price, quantity=@qty, min_quantity=@min
              WHERE id=@id", conn, tx))
        {
            Fill(cmd, p);
            cmd.Parameters.AddWithValue("@id", p.Id);
            cmd.ExecuteNonQuery();
        }

        if (oldPrice != p.Price)
        {
            using var hist = new MySqlCommand(
                "INSERT INTO price_history (product_id, old_price, new_price) VALUES (@id, @old, @new)", conn, tx);
            hist.Parameters.AddWithValue("@id", p.Id);
            hist.Parameters.AddWithValue("@old", oldPrice);
            hist.Parameters.AddWithValue("@new", p.Price);
            hist.ExecuteNonQuery();
        }

        tx.Commit();
    }

    public void Delete(int id)
    {
        using var conn = Database.Open();
        using var cmd = new MySqlCommand("DELETE FROM products WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Приход товара на склад.</summary>
    public void AddStock(int id, int quantity)
    {
        using var conn = Database.Open();
        using var cmd = new MySqlCommand("UPDATE products SET quantity = quantity + @q WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("@q", quantity);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public List<PriceChange> GetPriceHistory(int productId)
    {
        using var conn = Database.Open();
        using var cmd = new MySqlCommand(
            "SELECT changed_at, old_price, new_price FROM price_history WHERE product_id = @id ORDER BY changed_at DESC", conn);
        cmd.Parameters.AddWithValue("@id", productId);
        var list = new List<PriceChange>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new PriceChange { ChangedAt = r.GetDateTime(0), OldPrice = r.GetDecimal(1), NewPrice = r.GetDecimal(2) });
        return list;
    }

    private static void Fill(MySqlCommand cmd, Product p)
    {
        cmd.Parameters.AddWithValue("@sku", p.Sku.Trim());
        cmd.Parameters.AddWithValue("@name", p.Name.Trim());
        cmd.Parameters.AddWithValue("@cat", string.IsNullOrWhiteSpace(p.Category) ? null : p.Category.Trim());
        cmd.Parameters.AddWithValue("@buy", p.PurchasePrice);
        cmd.Parameters.AddWithValue("@price", p.Price);
        cmd.Parameters.AddWithValue("@qty", p.Quantity);
        cmd.Parameters.AddWithValue("@min", p.MinQuantity);
    }

    private static Product Map(MySqlDataReader r) => new()
    {
        Id = r.GetInt32(0),
        Sku = r.GetString(1),
        Name = r.GetString(2),
        Category = r.IsDBNull(3) ? "" : r.GetString(3),
        PurchasePrice = r.GetDecimal(4),
        Price = r.GetDecimal(5),
        Quantity = r.GetInt32(6),
        MinQuantity = r.GetInt32(7),
        UpdatedAt = r.GetDateTime(8)
    };
}
