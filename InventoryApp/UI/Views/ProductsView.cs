using InventoryApp.Data;
using InventoryApp.Models;
using InventoryApp.UI.Dialogs;

namespace InventoryApp.UI.Views;

public class ProductsView : UserControl, IRefreshable
{
    private const string AllCategories = "Все категории";

    private readonly ProductRepository _repo = new();
    private readonly TextBox _search = AppStyle.Ui.TextBox("Поиск: название, артикул, категория…", 320);
    private readonly ComboBox _category = AppStyle.Ui.Combo(200);
    private readonly CheckBox _lowOnly = new()
    {
        Text = "Только заканчивающиеся", ForeColor = AppStyle.Ui.Text, AutoSize = true,
        Padding = new Padding(4, 6, 0, 0), Cursor = Cursors.Hand
    };
    private readonly DataGridView _grid = AppStyle.Ui.Grid();
    private readonly Label _footer = AppStyle.Ui.Footer();
    private readonly DebouncedAction _searchReload;
    private bool _loading;

    public ProductsView()
    {
        BackColor = AppStyle.Ui.Bg;
        _searchReload = new DebouncedAction(this, LoadProducts);

        var add = AppStyle.Ui.Btn("+ Добавить", AppStyle.Ui.Accent, 130);
        var edit = AppStyle.Ui.SecondaryBtn("Изменить");
        var stockIn = AppStyle.Ui.Btn("Приход", AppStyle.Ui.Success, 110);
        var delete = AppStyle.Ui.Btn("Удалить", AppStyle.Ui.Danger, 110);

        add.OnClick(AddProduct);
        edit.OnClick(EditSelected);
        stockIn.OnClick(StockInSelected);
        delete.OnClick(DeleteSelected);

        var toolbar = AppStyle.Ui.Toolbar(new Control[] { _search, _category, _lowOnly },
                                     new Control[] { delete, stockIn, edit, add });

        _grid.Define<Product>()
            .Column(row => row.Sku, "Артикул", 70)
            .Column(row => row.Name, "Название", 220)
            .Column(row => row.Category, "Категория", 100)
            .Column(row => row.PurchasePrice, "Закупка, сом", 75, "N0", true)
            .Column(row => row.Price, "Цена, сом", 75, "N0", true)
            .Column(row => row.Quantity, "Остаток", 60, right: true)
            .Column(row => row.MinQuantity, "Мин.", 45, right: true)
            .Column(row => row.Status, "Статус", 85).Apply();
        _grid.CellFormatting += (_, e) => Ui.ColorStatus(_grid, e);
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditSelected(); };
        _grid.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Delete) DeleteSelected();
            if (e.KeyCode == Keys.Enter) { EditSelected(); e.Handled = true; }
        };

        var body = AppStyle.Ui.Surface(_grid)
            .Bottom(_footer).Build();
        var card = AppStyle.Ui.TitledCard("Каталог товаров", body);

        _search.TextChanged += (_, _) => _searchReload.Trigger();
        _category.SelectedIndexChanged += (_, _) => { if (!_loading) LoadProducts(); };
        _lowOnly.CheckedChanged += (_, _) => LoadProducts();

        AppStyle.Ui.Page(card)
            .Above(AppStyle.Ui.Spacer(12))
            .Above(toolbar)
            .Above(AppStyle.Ui.PageHeader("Товары", "Добавление товаров, изменение цен, остатки и поиск")).Into(this);
    }

    public void RefreshData()
    {
        _loading = true;
        var current = _category.SelectedItem as string;
        _category.Items.Clear();
        _category.Items.Add(AllCategories);
        foreach (var c in _repo.GetCategories()) _category.Items.Add(c);
        _category.SelectedItem = current != null && _category.Items.Contains(current) ? current : AllCategories;
        _loading = false;
        LoadProducts();
    }

    private void LoadProducts()
    {
        AppStyle.Ui.Safe(() =>
        {
            var cat = _category.SelectedItem as string;
            var list = _repo.GetAll(_search.Text, cat == AllCategories ? null : cat, _lowOnly.Checked);
            _grid.BindRows(list, p => p.Id, focusColumn: 1);

            _footer.Text = $"Позиций: {list.Count}   •   Единиц: {list.Sum(p => p.Quantity):N0}   •   " +
                           $"Стоимость (закуп): {AppStyle.Ui.Money(list.Sum(p => p.StockValue))}   •   " +
                           $"Стоимость (продажа): {AppStyle.Ui.Money(list.Sum(p => p.Quantity * p.Price))}";
        });
    }

    private Product Selected => _grid.Selected<Product>();

    private void AddProduct()
    {
        using var f = new ProductEditForm(null, _repo.GetCategories());
        if (f.ShowDialog(this) != DialogResult.OK) return;
        AppStyle.Ui.Safe(() => { _repo.Insert(f.Result); RefreshData(); });
    }

    private void EditSelected()
    {
        var p = Selected;
        if (p == null) { AppStyle.Ui.Warn(this, "Выберите товар в списке."); return; }
        using var f = new ProductEditForm(p, _repo.GetCategories());
        if (f.ShowDialog(this) != DialogResult.OK) return;
        AppStyle.Ui.Safe(() => { _repo.Update(f.Result); RefreshData(); });
    }

    private void StockInSelected()
    {
        var p = Selected;
        if (p == null) { AppStyle.Ui.Warn(this, "Выберите товар в списке."); return; }
        var qty = QuantityDialog.Ask(this, "Приход товара", p.Name, $"Сейчас на складе: {p.Quantity} шт.");
        if (qty == null) return;
        AppStyle.Ui.Safe(() => { _repo.AddStock(p.Id, qty.Value); LoadProducts(); });
    }

    private void DeleteSelected()
    {
        var p = Selected;
        if (p == null) { AppStyle.Ui.Warn(this, "Выберите товар в списке."); return; }
        if (!AppStyle.Ui.Confirm(this, $"Удалить товар «{p.Name}»?\nИстория продаж сохранится.")) return;
        AppStyle.Ui.Safe(() => { _repo.Delete(p.Id); RefreshData(); });
    }
}
