using InventoryApp.Data;
using InventoryApp.Models;
using InventoryApp.UI.Dialogs;

namespace InventoryApp.UI.Views;

public class SalesView : UserControl, IRefreshable
{
    private readonly SaleRepository _repo = new();
    private readonly DateTimePicker _from = AppStyle.Ui.DatePicker(DateTime.Today.AddDays(-6));
    private readonly DateTimePicker _to = AppStyle.Ui.DatePicker(DateTime.Today);
    private readonly DataGridView _salesGrid = AppStyle.Ui.Grid();
    private readonly DataGridView _itemsGrid = AppStyle.Ui.Grid();
    private readonly Label _summary = AppStyle.Ui.Footer();
    private readonly Label _itemsTotal = AppStyle.Ui.Footer();

    public SalesView()
    {
        BackColor = AppStyle.Ui.Bg;

        var show = AppStyle.Ui.SecondaryBtn("Показать", 110);
        var today = AppStyle.Ui.SecondaryBtn("Сегодня", 100);
        var newSale = AppStyle.Ui.Btn("+ Новая продажа", AppStyle.Ui.Accent, 170);
        show.OnClick(RefreshData);
        today.Click += (_, _) => { _from.Value = DateTime.Today; _to.Value = DateTime.Today; RefreshData(); };
        newSale.OnClick(NewSale);

        var toolbar = AppStyle.Ui.Toolbar(
            new Control[] { AppStyle.Ui.ToolLabel("Период с"), _from, AppStyle.Ui.ToolLabel("по"), _to, show, today },
            new Control[] { newSale });

        _salesGrid.Define<Sale>()
            .Column(row => row.Id, "№ чека", 60)
            .Column(row => row.CreatedAt, "Дата и время", 130, "dd.MM.yyyy HH:mm")
            .Column(row => row.ItemsCount, "Товаров, шт.", 80, right: true)
            .Column(row => row.Total, "Сумма, сом", 90, "N0", true).Apply();
        _salesGrid.SelectionChanged += (_, _) => LoadItems();

        _itemsGrid.Define<SaleItem>()
            .Column(row => row.ProductName, "Товар", 190)
            .Column(row => row.Quantity, "Кол-во", 55, right: true)
            .Column(row => row.Price, "Цена", 70, "N0", true)
            .Column(row => row.Sum, "Сумма", 80, "N0", true).Apply();

        var left = AppStyle.Ui.Surface(_salesGrid)
            .Bottom(_summary).Build();

        var right = AppStyle.Ui.Surface(_itemsGrid)
            .Bottom(_itemsTotal).Build();

        var body = AppStyle.Ui.Columns(new[] { 55f, 45f }, new Control[]
        {
            AppStyle.Ui.TitledCard("Чеки за период", left),
            AppStyle.Ui.TitledCard("Состав чека", right)
        });
        body.Dock = DockStyle.Fill;

        AppStyle.Ui.Page(body)
            .Above(AppStyle.Ui.Spacer(12))
            .Above(toolbar)
            .Above(AppStyle.Ui.PageHeader("Продажи", "Оформление продаж и история чеков")).Into(this);
    }

    public void RefreshData()
    {
        if (_from.Value.Date > _to.Value.Date) { AppStyle.Ui.Warn(this, "Дата начала больше даты окончания."); return; }

        var sales = _repo.GetSales(_from.Value, _to.Value);
        _salesGrid.DataSource = sales;
        _summary.Text = $"Чеков: {sales.Count}   •   Продано: {sales.Sum(s => s.ItemsCount):N0} шт.   •   " +
                        $"Выручка: {AppStyle.Ui.Money(sales.Sum(s => s.Total))}" +
                        (sales.Count > 0 ? $"   •   Средний чек: {AppStyle.Ui.Money(sales.Average(s => s.Total))}" : "");
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
        AppStyle.Ui.Safe(() =>
        {
            var items = _repo.GetItems(sale.Id);
            _itemsGrid.DataSource = items;
            var profit = items.Sum(i => i.Quantity * (i.Price - i.CostPrice));
            _itemsTotal.Text = $"Чек №{sale.Id} от {sale.CreatedAt:dd.MM.yyyy HH:mm}   •   " +
                               $"Итого: {AppStyle.Ui.Money(sale.Total)}   •   Прибыль: {AppStyle.Ui.Money(profit)}";
        });
    }

    private void NewSale()
    {
        using var f = new SaleForm();
        if (f.ShowDialog(this) != DialogResult.OK) return;
        if (_to.Value.Date < DateTime.Today) _to.Value = DateTime.Today;
        RefreshData();
        AppStyle.Ui.Notify(this, $"Продажа №{f.SaleId} успешно оформлена.\nСумма: {AppStyle.Ui.Money(f.SaleTotal)}");
    }
}
