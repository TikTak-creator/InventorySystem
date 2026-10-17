namespace Inventory.Contracts;

/// <summary>Rules belong to the shop, not to the reusable EasyDo library.</summary>
public static class ProductRules
{
    public static Rules<Product> Valid { get; } = Rules<Product>.Create()
        .Require(nameof(Product.Name), p => !string.IsNullOrWhiteSpace(p?.Name), "Введите название товара.")
        .Require(nameof(Product.Sku), p => !string.IsNullOrWhiteSpace(p?.Sku), "Введите артикул.")
        .Require(nameof(Product.Price), p => p != null && p.Price > 0, "Цена продажи должна быть больше нуля.")
        .Require(nameof(Product.PurchasePrice), p => p != null && p.PurchasePrice >= 0, "Закупочная цена не может быть отрицательной.")
        .Require(nameof(Product.Quantity), p => p != null && p.Quantity >= 0, "Остаток не может быть отрицательным.")
        .Require(nameof(Product.MinQuantity), p => p != null && p.MinQuantity >= 0, "Минимальный остаток не может быть отрицательным.")
        .Require(nameof(Product.Price), p => p != null && p.Price <= 10_000_000 && decimal.Round(p.Price, 2) == p.Price, "Цена: до 10000000 и не более двух знаков после запятой.")
        .Require(nameof(Product.PurchasePrice), p => p != null && p.PurchasePrice <= 10_000_000 && decimal.Round(p.PurchasePrice, 2) == p.PurchasePrice, "Закупка: до 10000000 и не более двух знаков после запятой.")
        .Require(nameof(Product.Quantity), p => p != null && p.Quantity <= 1_000_000, "Остаток: до 1000000.")
        .Require(nameof(Product.MinQuantity), p => p != null && p.MinQuantity <= 1_000_000, "Минимальный остаток: до 1000000.")
        .Require(nameof(Product.Name), p => p?.Name?.Trim().Length <= 200, "Название: максимум 200 символов.")
        .Require(nameof(Product.Sku), p => p?.Sku?.Trim().Length <= 50, "Артикул: максимум 50 символов.")
        .Require(nameof(Product.Category), p => p != null && (p.Category?.Trim().Length ?? 0) <= 100, "Категория: максимум 100 символов.");
}
