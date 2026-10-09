using InventoryApp.Data;
using InventoryApp.Models;
using InventoryApp.UI.Dialogs;

namespace InventoryApp.UI.Views;

public class SalesView : UserControl, IRefreshable
{
    private readonly SaleRepository _repo = new();
    private readonly DateTimePicker _from = Theme.DatePicker(DateTime.Today.AddDays(-6));
    private readonly DateTimePicker _to = Theme.DatePicker(DateTime.Today);
    private readonly DataGridView _salesGrid = Theme.Grid();
    private readonly DataGridView _itemsGrid = Theme.Grid();
    private readonly Label _summary = Ui.Footer();
    private readonly Label _itemsTotal = Ui.Footer();

    public SalesView()
    {
        BackColor = Theme.Bg;

        var show = Theme.SecondaryBtn("Показать", 110);
        var today = Theme.SecondaryBtn("Сегодня", 100);
        var newSale = Theme.Btn("+ Новая продажа", Theme.Accent, 170);
        show.Click += (_, _) => RefreshData();
        today.Click += (_, _) => { _from.Value = DateTime.Today; _to.Value = DateTime.Today; RefreshData(); };
        newSale.Click += (_, _) => NewSale();

        var toolbar = Ui.Toolbar(
            new Control[] { Ui.ToolLabel("Период с"), _from, Ui.ToolLabel("по"), _to, show, today },
            new Control[] { newSale });

        _salesGrid.Columns.AddRange(
            Theme.Col("Id", "№ чека", 60),
            Theme.Col("CreatedAt", "Дата и время", 130, "dd.MM.yyyy HH:mm"),
            Theme.Col("ItemsCount", "Товаров, шт.", 80, right: true),
            Theme.Col("Total", "Сумма, сом", 90, "N0", true));
        _salesGrid.SelectionChanged += (_, _) => LoadItems();

        _itemsGrid.Columns.AddRange(
            Theme.Col("ProductName", "Товар", 190),
            Theme.Col("Quantity", "Кол-во", 55, right: true),
            Theme.Col("Price", "Цена", 70, "N0", true),
            Theme.Col("Sum", "Сумма", 80, "N0", true));

        var left = new Panel { BackColor = Theme.Card };
        left.Controls.Add(_salesGrid);
        left.Controls.Add(_summary);
        _salesGrid.BringToFront();

        var right = new Panel { BackColor = Theme.Card };
        right.Controls.Add(_itemsGrid);
        right.Controls.Add(_itemsTotal);
        _itemsGrid.BringToFront();

        var body = Ui.Columns(new[] { 55f, 45f }, new Control[]
        {
            Theme.TitledCard("Чеки за период", left),
            Theme.TitledCard("Состав чека", right)
        });
        body.Dock = DockStyle.Fill;

        Controls.Add(body);
        Controls.Add(Ui.Spacer(12));
        Controls.Add(toolbar);
        Controls.Add(Theme.PageHeader("Продажи", "Оформление продаж и история чеков"));
        body.BringToFront();
    }

    public void RefreshData()
    {
        if (_from.Value.Date > _to.Value.Date) { Theme.Warn(this, "Дата начала больше даты окончания."); return; }

        var sales = _repo.GetSales(_from.Value, _to.Value);
        _salesGrid.DataSource = sales;
        _summary.Text = $"Чеков: {sales.Count}   •   Продано: {sales.Sum(s => s.ItemsCount):N0} шт.   •   " +
                        $"Выручка: {Theme.Money(sales.Sum(s => s.Total))}" +
                        (sales.Count > 0 ? $"   •   Средний чек: {Theme.Money(sales.Average(s => s.Total))}" : "");
        LoadItems();
    }

    private void LoadItems()
    {
        if (_salesGrid.CurrentRow?.DataBoundItem is not Sale sale)
        {
            _itemsGrid.DataSource = null;
            _itemsTotal.Text = "Выберите чек слева";
            return;
        }
        Theme.Safe(() =>
        {
            var items = _repo.GetItems(sale.Id);
            _itemsGrid.DataSource = items;
            var profit = items.Sum(i => i.Quantity * (i.Price - i.CostPrice));
            _itemsTotal.Text = $"Чек №{sale.Id} от {sale.CreatedAt:dd.MM.yyyy HH:mm}   •   " +
                               $"Итого: {Theme.Money(sale.Total)}   •   Прибыль: {Theme.Money(profit)}";
        });
    }

    private void NewSale()
    {
        using var f = new SaleForm();
        if (f.ShowDialog(this) != DialogResult.OK) return;
        if (_to.Value.Date < DateTime.Today) _to.Value = DateTime.Today;
        RefreshData();
        Theme.Notify(this, $"Продажа №{f.SaleId} успешно оформлена.\nСумма: {Theme.Money(f.SaleTotal)}");
    }
}
