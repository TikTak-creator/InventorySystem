using InventoryApp.Data;
using Inventory.Contracts;
using InventoryApp.UI.Dialogs;

namespace InventoryApp.UI.Views;

public class StockView : UserControl, IRefreshable
{
    private readonly ProductRepository _repo = new();
    private readonly SaleRepository _sales = new();

    private readonly StatCard _cPositions = new(AppStyle.Ui, "Позиций", AppStyle.Ui.Accent);
    private readonly StatCard _cUnits = new(AppStyle.Ui, "Единиц на складе", AppStyle.Ui.Info);
    private readonly StatCard _cValue = new(AppStyle.Ui, "Стоимость склада", AppStyle.Ui.Success);
    private readonly StatCard _cLow = new(AppStyle.Ui, "Нужно пополнить", AppStyle.Ui.Warning);

    private readonly DataGridView _grid = AppStyle.Ui.Grid();
    private readonly DataGridView _history = AppStyle.Ui.Grid();
    private readonly Label _historyTitle = AppStyle.Ui.Footer();
    private readonly CheckBox _lowOnly = new()
    {
        Text = "Только требующие пополнения", ForeColor = AppStyle.Ui.Text, AutoSize = true,
        Padding = new Padding(4, 6, 0, 0), Cursor = Cursors.Hand
    };

    public StockView()
    {
        BackColor = AppStyle.Ui.Bg;

        var stats = AppStyle.Ui.Columns(new[] { 25f, 25f, 25f, 25f }, new Control[] { _cPositions, _cUnits, _cValue, _cLow });
        stats.Dock = DockStyle.Top;
        stats.Height = 112;

        var stockIn = AppStyle.Ui.Btn("+ Приход", AppStyle.Ui.Success, 120);
        stockIn.Enabled = ApiSession.Current?.CanWrite ?? true;
        stockIn.OnClick(StockIn);
        _lowOnly.CheckedChanged += (_, _) => RefreshData();
        var toolbar = AppStyle.Ui.Toolbar(new Control[] { _lowOnly }, new Control[] { stockIn }, 44);

        _grid.Define<Product>()
            .Column(row => row.Name, "Товар", 210)
            .Column(row => row.Quantity, "Остаток", 60, right: true)
            .Column(row => row.MinQuantity, "Мин.", 50, right: true)
            .Column(row => row.Status, "Статус", 90)
            .Column(row => row.StockValue, "Стоимость, сом", 90, "N0", true).Apply();
        _grid.CellFormatting += (_, e) => Ui.ColorStatus(_grid, e);
        _grid.SelectionChanged += (_, _) => LoadHistory();
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) StockIn(); };

        _history.Define<PriceChange>()
            .Column(row => row.ChangedAt, "Дата", 110, "dd.MM.yyyy HH:mm")
            .Column(row => row.OldPrice, "Было", 65, "N0", true)
            .Column(row => row.NewPrice, "Стало", 65, "N0", true)
            .Column(row => row.DiffText, "Изм.", 60, right: true).Apply();
        _history.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || _history.Columns[e.ColumnIndex].Name != "DiffText") return;
            var change = (PriceChange)_history.Rows[e.RowIndex].DataBoundItem;
            var c = change.Diff > 0 ? AppStyle.Ui.Danger : AppStyle.Ui.Success;
            e.CellStyle.ForeColor = c;
            e.CellStyle.SelectionForeColor = c;
        };

        var left = AppStyle.Ui.Surface(_grid)
            .Above(toolbar).Build();
        toolbar.BackColor = AppStyle.Ui.Card;
        foreach (Control c in toolbar.Controls) c.BackColor = AppStyle.Ui.Card;

        var right = AppStyle.Ui.Surface(_history)
            .Bottom(_historyTitle).Build();

        var body = AppStyle.Ui.Columns(new[] { 62f, 38f }, new Control[]
        {
            AppStyle.Ui.TitledCard("Остатки на складе", left),
            AppStyle.Ui.TitledCard("История изменения цен", right)
        });
        body.Dock = DockStyle.Fill;

        AppStyle.Ui.Page(body)
            .Above(AppStyle.Ui.Spacer(18))
            .Above(stats)
            .Above(AppStyle.Ui.PageHeader("Склад", "Остатки, приход товара и история цен")).Into(this);
    }

    public void RefreshData()
    {
        var s = _sales.GetStats();
        _cPositions.Value = s.ProductsCount.ToString("N0");
        _cPositions.Hint = "наименований в каталоге";
        _cUnits.Value = s.UnitsInStock.ToString("N0");
        _cUnits.Hint = "штук всего";
        _cValue.Value = AppStyle.Ui.Money(s.StockValue);
        _cValue.Hint = "по закупочным ценам";
        _cLow.Value = s.LowStockCount.ToString();
        _cLow.Hint = "остаток ≤ минимального";

        _grid.BindRows(_repo.GetAll(lowOnly: _lowOnly.Checked)
            .OrderBy(p => p.Quantity - p.MinQuantity), p => p.Id);
        LoadHistory();
    }

    private void LoadHistory()
    {
        if (_grid.CurrentRow?.DataBoundItem is not Product p)
        {
            _history.DataSource = null;
            _historyTitle.Text = "Выберите товар слева";
            return;
        }
        AppStyle.Ui.Safe(() =>
        {
            var list = _repo.GetPriceHistory(p.Id);
            _history.DataSource = list;
            _historyTitle.Text = list.Count == 0
                ? $"{p.Name}: цена не менялась ({AppStyle.Ui.Money(p.Price)})"
                : $"{p.Name}: текущая цена {AppStyle.Ui.Money(p.Price)}";
        });
    }

    private void StockIn()
    {
        if (_grid.CurrentRow?.DataBoundItem is not Product p) { AppStyle.Ui.Warn(this, "Выберите товар в списке."); return; }
        var qty = QuantityDialog.Ask(this, "Приход товара", p.Name, $"Сейчас на складе: {p.Quantity} шт.");
        if (qty == null) return;
        AppStyle.Ui.Safe(() => { _repo.AddStock(p.Id, qty.Value); RefreshData(); });
    }
}
