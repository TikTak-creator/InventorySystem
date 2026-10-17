using InventoryApp.Data;
using Inventory.Contracts;

namespace InventoryApp.UI.Views;

public class DashboardView : UserControl, IRefreshable
{
    private readonly ProductRepository _products = new();
    private readonly SaleRepository _sales = new();

    private readonly StatCard _cProducts = new(AppStyle.Ui, "Товаров", AppStyle.Ui.Accent);
    private readonly StatCard _cRevenue = new(AppStyle.Ui, "Выручка сегодня", AppStyle.Ui.Success);
    private readonly StatCard _cLow = new(AppStyle.Ui, "Мало на складе", AppStyle.Ui.Danger);
    private readonly StatCard _cSales = new(AppStyle.Ui, "Продаж сегодня", AppStyle.Ui.Info);

    private readonly BarChart _chart = new(AppStyle.Ui);
    private readonly DataGridView _recent = AppStyle.Ui.Grid();
    private readonly DataGridView _low = AppStyle.Ui.Grid();
    private readonly Label _subtitle;

    public DashboardView()
    {
        BackColor = AppStyle.Ui.Bg;
        DoubleBuffered = true;

        var header = AppStyle.Ui.PageHeader("Обзор", "");
        _subtitle = (Label)header.Controls[1];

        var stats = AppStyle.Ui.Columns(new[] { 25f, 25f, 25f, 25f },
            new Control[] { _cProducts, _cRevenue, _cLow, _cSales });
        stats.Dock = DockStyle.Top;
        stats.Height = 112;

        _recent.Define<Sale>()
            .Column(row => row.Id, "№ чека", 60)
            .Column(row => row.CreatedAt, "Дата и время", 120, "dd.MM.yyyy HH:mm")
            .Column(row => row.ItemsCount, "Товаров", 60, right: true)
            .Column(row => row.Total, "Сумма, сом", 90, "N0", right: true).Apply();

        _low.Define<Product>()
            .Column(row => row.Name, "Товар", 170)
            .Column(row => row.Quantity, "Ост.", 45, right: true)
            .Column(row => row.Status, "Статус", 85).Apply();
        _low.CellFormatting += (_, e) => Ui.ColorStatus(_low, e);

        var chartCard = AppStyle.Ui.TitledCard("Выручка за 7 дней", _chart);
        var recentCard = AppStyle.Ui.TitledCard("Последние продажи", _recent);
        var lowCard = AppStyle.Ui.TitledCard("Заканчиваются на складе", _low);

        var bottom = AppStyle.Ui.Matrix(new[] { 62f, 38f }, new[] { 52f, 48f })
            .Cell(chartCard, 0, 0, margin: new Padding(0, 0, 16, 16))
            .Cell(recentCard, 0, 1, margin: new Padding(0, 0, 16, 0))
            .Cell(lowCard, 1, 0, rowSpan: 2, margin: Padding.Empty).Build();

        AppStyle.Ui.Page(bottom)
            .Above(AppStyle.Ui.Spacer(18))
            .Above(stats)
            .Above(header).Into(this);
    }

    public void RefreshData()
    {
        var date = DateTime.Now.ToString("dddd, d MMMM yyyy");
        _subtitle.Text = char.ToUpper(date[0]) + date[1..] + " • сводка по магазину";

        var s = _sales.GetStats();
        _cProducts.Value = s.ProductsCount.ToString("N0");
        _cProducts.Hint = $"{s.UnitsInStock:N0} ед. на {AppStyle.Ui.Money(s.StockValue)}";
        _cRevenue.Value = AppStyle.Ui.Money(s.TodayRevenue);
        _cRevenue.Hint = "прибыль: " + AppStyle.Ui.Money(s.TodayProfit);
        _cLow.Value = s.LowStockCount.ToString();
        _cLow.Hint = "требуют пополнения";
        _cSales.Value = s.TodaySales.ToString();
        _cSales.Hint = s.TodaySales > 0 ? "средний чек: " + AppStyle.Ui.Money(s.TodayRevenue / s.TodaySales) : "чеков пока нет";

        _chart.SetData(_sales.GetRevenueByDay(7)
            .Select(d => (d.Day.Date == DateTime.Today ? "Сегодня" : d.Day.ToString("ddd, dd"), d.Total)));

        _recent.DataSource = _sales.GetRecent(12);
        _low.DataSource = _products.GetAll(lowOnly: true).OrderBy(p => p.Quantity).ToList();
        _recent.ClearSelection();
        _low.ClearSelection();
    }
}
