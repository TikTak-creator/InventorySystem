using InventoryApp.Data;

namespace InventoryApp.UI.Views;

public class DashboardView : UserControl, IRefreshable
{
    private readonly ProductRepository _products = new();
    private readonly SaleRepository _sales = new();

    private readonly StatCard _cProducts = new("Товаров", Theme.Accent);
    private readonly StatCard _cRevenue = new("Выручка сегодня", Theme.Success);
    private readonly StatCard _cLow = new("Мало на складе", Theme.Danger);
    private readonly StatCard _cSales = new("Продаж сегодня", Theme.Info);

    private readonly BarChart _chart = new();
    private readonly DataGridView _recent = Theme.Grid();
    private readonly DataGridView _low = Theme.Grid();
    private readonly Label _subtitle;

    public DashboardView()
    {
        BackColor = Theme.Bg;
        DoubleBuffered = true;

        var header = Theme.PageHeader("Обзор", "");
        _subtitle = (Label)header.Controls[1];

        var stats = Ui.Columns(new[] { 25f, 25f, 25f, 25f },
            new Control[] { _cProducts, _cRevenue, _cLow, _cSales });
        stats.Dock = DockStyle.Top;
        stats.Height = 112;

        _recent.Columns.AddRange(
            Theme.Col("Id", "№ чека", 60),
            Theme.Col("CreatedAt", "Дата и время", 120, "dd.MM.yyyy HH:mm"),
            Theme.Col("ItemsCount", "Товаров", 60, right: true),
            Theme.Col("Total", "Сумма, сом", 90, "N0", right: true));

        _low.Columns.AddRange(
            Theme.Col("Name", "Товар", 170),
            Theme.Col("Quantity", "Ост.", 45, right: true),
            Theme.Col("Status", "Статус", 85));
        _low.CellFormatting += (_, e) => Ui.ColorStatus(_low, e);

        var chartCard = Theme.TitledCard("Выручка за 7 дней", _chart);
        var recentCard = Theme.TitledCard("Последние продажи", _recent);
        var lowCard = Theme.TitledCard("Заканчиваются на складе", _low);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = Theme.Bg };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
        bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
        chartCard.Margin = new Padding(0, 0, 16, 16);
        recentCard.Margin = new Padding(0, 0, 16, 0);
        lowCard.Margin = Padding.Empty;
        bottom.Controls.Add(chartCard, 0, 0);
        bottom.Controls.Add(recentCard, 0, 1);
        bottom.Controls.Add(lowCard, 1, 0);
        bottom.SetRowSpan(lowCard, 2);

        Controls.Add(bottom);
        Controls.Add(Ui.Spacer(18));
        Controls.Add(stats);
        Controls.Add(header);
        bottom.BringToFront();
    }

    public void RefreshData()
    {
        var date = DateTime.Now.ToString("dddd, d MMMM yyyy");
        _subtitle.Text = char.ToUpper(date[0]) + date[1..] + " • сводка по магазину";

        var s = _sales.GetStats();
        _cProducts.Value = s.ProductsCount.ToString("N0");
        _cProducts.Hint = $"{s.UnitsInStock:N0} ед. на {Theme.Money(s.StockValue)}";
        _cRevenue.Value = Theme.Money(s.TodayRevenue);
        _cRevenue.Hint = "прибыль: " + Theme.Money(s.TodayProfit);
        _cLow.Value = s.LowStockCount.ToString();
        _cLow.Hint = "требуют пополнения";
        _cSales.Value = s.TodaySales.ToString();
        _cSales.Hint = s.TodaySales > 0 ? "средний чек: " + Theme.Money(s.TodayRevenue / s.TodaySales) : "чеков пока нет";

        _chart.SetData(_sales.GetRevenueByDay(7)
            .Select(d => (d.Day.Date == DateTime.Today ? "Сегодня" : d.Day.ToString("ddd, dd"), d.Total)));

        _recent.DataSource = _sales.GetRecent(12);
        _low.DataSource = _products.GetAll(lowOnly: true).OrderBy(p => p.Quantity).ToList();
        _recent.ClearSelection();
        _low.ClearSelection();
    }
}
