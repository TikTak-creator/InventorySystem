using InventoryApp.Data;
using InventoryApp.Models;
using InventoryApp.UI.Dialogs;

namespace InventoryApp.UI.Views;

public class StockView : UserControl, IRefreshable
{
    private readonly ProductRepository _repo = new();
    private readonly SaleRepository _sales = new();

    private readonly StatCard _cPositions = new("Позиций", Theme.Accent);
    private readonly StatCard _cUnits = new("Единиц на складе", Theme.Info);
    private readonly StatCard _cValue = new("Стоимость склада", Theme.Success);
    private readonly StatCard _cLow = new("Нужно пополнить", Theme.Warning);

    private readonly DataGridView _grid = Theme.Grid();
    private readonly DataGridView _history = Theme.Grid();
    private readonly Label _historyTitle = Ui.Footer();
    private readonly CheckBox _lowOnly = new()
    {
        Text = "Только требующие пополнения", ForeColor = Theme.Text, AutoSize = true,
        Padding = new Padding(4, 6, 0, 0), Cursor = Cursors.Hand
    };

    public StockView()
    {
        BackColor = Theme.Bg;

        var stats = Ui.Columns(new[] { 25f, 25f, 25f, 25f }, new Control[] { _cPositions, _cUnits, _cValue, _cLow });
        stats.Dock = DockStyle.Top;
        stats.Height = 112;

        var stockIn = Theme.Btn("+ Приход", Theme.Success, 120);
        stockIn.Click += (_, _) => StockIn();
        _lowOnly.CheckedChanged += (_, _) => RefreshData();
        var toolbar = Ui.Toolbar(new Control[] { _lowOnly }, new Control[] { stockIn }, 44);

        _grid.Columns.AddRange(
            Theme.Col("Name", "Товар", 210),
            Theme.Col("Quantity", "Остаток", 60, right: true),
            Theme.Col("MinQuantity", "Мин.", 50, right: true),
            Theme.Col("Status", "Статус", 90),
            Theme.Col("StockValue", "Стоимость, сом", 90, "N0", true));
        _grid.CellFormatting += (_, e) => Ui.ColorStatus(_grid, e);
        _grid.SelectionChanged += (_, _) => LoadHistory();
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) StockIn(); };

        _history.Columns.AddRange(
            Theme.Col("ChangedAt", "Дата", 110, "dd.MM.yyyy HH:mm"),
            Theme.Col("OldPrice", "Было", 65, "N0", true),
            Theme.Col("NewPrice", "Стало", 65, "N0", true),
            Theme.Col("DiffText", "Изм.", 60, right: true));
        _history.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || _history.Columns[e.ColumnIndex].Name != "DiffText") return;
            var change = (PriceChange)_history.Rows[e.RowIndex].DataBoundItem;
            var c = change.Diff > 0 ? Theme.Danger : Theme.Success;
            e.CellStyle.ForeColor = c;
            e.CellStyle.SelectionForeColor = c;
        };

        var left = new Panel { BackColor = Theme.Card };
        left.Controls.Add(_grid);
        left.Controls.Add(toolbar);
        _grid.BringToFront();
        toolbar.BackColor = Theme.Card;
        foreach (Control c in toolbar.Controls) c.BackColor = Theme.Card;

        var right = new Panel { BackColor = Theme.Card };
        right.Controls.Add(_history);
        right.Controls.Add(_historyTitle);
        _history.BringToFront();

        var body = Ui.Columns(new[] { 62f, 38f }, new Control[]
        {
            Theme.TitledCard("Остатки на складе", left),
            Theme.TitledCard("История изменения цен", right)
        });
        body.Dock = DockStyle.Fill;

        Controls.Add(body);
        Controls.Add(Ui.Spacer(18));
        Controls.Add(stats);
        Controls.Add(Theme.PageHeader("Склад", "Остатки, приход товара и история цен"));
        body.BringToFront();
    }

    public void RefreshData()
    {
        var s = _sales.GetStats();
        _cPositions.Value = s.ProductsCount.ToString("N0");
        _cPositions.Hint = "наименований в каталоге";
        _cUnits.Value = s.UnitsInStock.ToString("N0");
        _cUnits.Hint = "штук всего";
        _cValue.Value = Theme.Money(s.StockValue);
        _cValue.Hint = "по закупочным ценам";
        _cLow.Value = s.LowStockCount.ToString();
        _cLow.Hint = "остаток ≤ минимального";

        var selectedId = (_grid.CurrentRow?.DataBoundItem as Product)?.Id;
        _grid.DataSource = _repo.GetAll(lowOnly: _lowOnly.Checked).OrderBy(p => p.Quantity - p.MinQuantity).ToList();
        if (selectedId != null)
            foreach (DataGridViewRow row in _grid.Rows)
                if (((Product)row.DataBoundItem).Id == selectedId) { _grid.CurrentCell = row.Cells[0]; break; }
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
        Theme.Safe(() =>
        {
            var list = _repo.GetPriceHistory(p.Id);
            _history.DataSource = list;
            _historyTitle.Text = list.Count == 0
                ? $"{p.Name}: цена не менялась ({Theme.Money(p.Price)})"
                : $"{p.Name}: текущая цена {Theme.Money(p.Price)}";
        });
    }

    private void StockIn()
    {
        if (_grid.CurrentRow?.DataBoundItem is not Product p) { Theme.Warn(this, "Выберите товар в списке."); return; }
        var qty = QuantityDialog.Ask(this, "Приход товара", p.Name, $"Сейчас на складе: {p.Quantity} шт.");
        if (qty == null) return;
        Theme.Safe(() => { _repo.AddStock(p.Id, qty.Value); RefreshData(); });
    }
}
