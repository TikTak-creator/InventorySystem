namespace InventoryApp.Models;

public class Product
{
    public int Id { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal PurchasePrice { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public int MinQuantity { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool IsLow => Quantity <= MinQuantity;
    public decimal StockValue => Quantity * PurchasePrice;
    public string Status => Quantity == 0 ? "Нет в наличии" : IsLow ? "Мало" : "В наличии";
}

public class Sale
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal Total { get; set; }
    public int ItemsCount { get; set; }
}

public class SaleItem
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public int? ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal CostPrice { get; set; }
    public decimal Sum => Quantity * Price;
}

/// <summary>Позиция в корзине при оформлении продажи.</summary>
public class CartItem
{
    public Product Product { get; set; }
    public int Quantity { get; set; }
    public string Name => Product.Name;
    public decimal Price => Product.Price;
    public decimal Sum => Quantity * Product.Price;
}

public class PriceChange
{
    public DateTime ChangedAt { get; set; }
    public decimal OldPrice { get; set; }
    public decimal NewPrice { get; set; }
    public decimal Diff => NewPrice - OldPrice;
    public string DiffText => (Diff > 0 ? "+" : "") + Diff.ToString("N0");
}

public class DashboardStats
{
    public int ProductsCount { get; set; }
    public int LowStockCount { get; set; }
    public int UnitsInStock { get; set; }
    public decimal StockValue { get; set; }
    public int TodaySales { get; set; }
    public decimal TodayRevenue { get; set; }
    public decimal TodayProfit { get; set; }
}
