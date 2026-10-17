using Inventory.Contracts;

namespace InventoryApp.Data;

public class SaleRepository
{
    public SaleReceipt CheckoutSale(IList<CartItem> items, string requestKey = null) => ApiSession.Require().Post<SaleReceipt>("api/v1/sales",
        new CreateSaleRequest(items.Select(item => new SaleLineRequest(item.Product.Id, item.Quantity)).ToList()), requestKey ?? Guid.NewGuid().ToString("D"));
    public int CreateSale(IList<CartItem> items) => CheckoutSale(items).Id;
    public List<Sale> GetSales(DateTime from, DateTime to)
    {
        var result = new List<Sale>();
        for (var offset = 0; ; offset += 200)
        {
            var page = ApiSession.Require().Get<List<Sale>>($"api/v1/sales?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&offset={offset}&limit=200");
            result.AddRange(page); if (page.Count < 200) return result;
        }
    }
    public List<Sale> GetRecent(int count) => ApiSession.Require().Get<List<Sale>>($"api/v1/sales/recent?count={count}");
    public List<SaleItem> GetItems(int id) => ApiSession.Require().Get<List<SaleItem>>($"api/v1/sales/{id}/items");
    public DashboardStats GetStats() => ApiSession.Require().Get<DashboardStats>("api/v1/dashboard/stats");
    public List<(DateTime Day, decimal Total)> GetRevenueByDay(int days) => ApiSession.Require().Get<List<RevenueDay>>($"api/v1/dashboard/revenue?days={days}")
        .Select(day => (day.Day, day.Total)).ToList();
}
