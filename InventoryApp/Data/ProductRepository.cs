using Inventory.Contracts;

namespace InventoryApp.Data;

/// <summary>UI adapter: no database credentials or MySQL dependency on the client.</summary>
public class ProductRepository
{
    private static string Escape(string text) => Uri.EscapeDataString(text ?? "");
    public List<Product> GetAll(string search = null, string category = null, bool lowOnly = false)
    {
        var result = new List<Product>();
        for (var offset = 0; ; offset += 200)
        {
            var page = ApiSession.Require().Get<List<Product>>($"api/v1/products?search={Escape(search)}&category={Escape(category)}&lowOnly={lowOnly}&offset={offset}&limit=200");
            result.AddRange(page); if (page.Count < 200) return result;
        }
    }
    public List<string> GetCategories() => ApiSession.Require().Get<List<string>>("api/v1/products/categories");
    public bool SkuExists(string sku, int excludeId) => ApiSession.Require().Get<ExistsResponse>($"api/v1/products/sku-exists?sku={Escape(sku)}&excludeId={excludeId}").Exists;
    public int Insert(Product product) => ApiSession.Require().Post<IdResponse>("api/v1/products", product).Id;
    public void Update(Product product) => ApiSession.Require().Put($"api/v1/products/{product.Id}", product);
    public void Delete(int id) => ApiSession.Require().Delete($"api/v1/products/{id}");
    public void AddStock(int id, int quantity) => ApiSession.Require().Post<object>($"api/v1/products/{id}/stock", new StockRequest(quantity));
    public List<PriceChange> GetPriceHistory(int id) => ApiSession.Require().Get<List<PriceChange>>($"api/v1/products/{id}/price-history");
}
