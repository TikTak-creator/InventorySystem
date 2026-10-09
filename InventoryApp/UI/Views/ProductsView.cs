using InventoryApp.Data;
using InventoryApp.Models;
using InventoryApp.UI.Dialogs;

namespace InventoryApp.UI.Views;

public class ProductsView : UserControl, IRefreshable
{
    private const string AllCategories = "Все категории";

    private readonly ProductRepository _repo = new();
    private readonly TextBox _search = Theme.TextBox("Поиск: название, артикул, категория…", 320);
    private readonly ComboBox _category = Theme.Combo(200);
    private readonly CheckBox _lowOnly = new()
    {
        Text = "Только заканчивающиеся", ForeColor = Theme.Text, AutoSize = true,
        Padding = new Padding(4, 6, 0, 0), Cursor = Cursors.Hand
    };
    private readonly DataGridView _grid = Theme.Grid();
    private readonly Label _footer = Ui.Footer();
    private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 300 };
    private bool _loading;

    public ProductsView()
    {
        BackColor = Theme.Bg;

        var add = Theme.Btn("+ Добавить", Theme.Accent, 130);
        var edit = Theme.SecondaryBtn("Изменить");
        var stockIn = Theme.Btn("Приход", Theme.Success, 110);
        var delete = Theme.Btn("Удалить", Theme.Danger, 110);

        add.Click += (_, _) => AddProduct();
        edit.Click += (_, _) => EditSelected();
        stockIn.Click += (_, _) => StockInSelected();
        delete.Click += (_, _) => DeleteSelected();

        var toolbar = Ui.Toolbar(new Control[] { _search, _category, _lowOnly },
                                     new Control[] { delete, stockIn, edit, add });

        _grid.Columns.AddRange(
            Theme.Col("Sku", "Артикул", 70),
            Theme.Col("Name", "Название", 220),
            Theme.Col("Category", "Категория", 100),
            Theme.Col("PurchasePrice", "Закупка, сом", 75, "N0", true),
            Theme.Col("Price", "Цена, сом", 75, "N0", true),
            Theme.Col("Quantity", "Остаток", 60, right: true),
            Theme.Col("MinQuantity", "Мин.", 45, right: true),
            Theme.Col("Status", "Статус", 85));
        _grid.CellFormatting += (_, e) => Ui.ColorStatus(_grid, e);
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditSelected(); };
        _grid.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Delete) DeleteSelected();
            if (e.KeyCode == Keys.Enter) { EditSelected(); e.Handled = true; }
        };

        var body = new Panel { BackColor = Theme.Card };
        body.Controls.Add(_grid);
        body.Controls.Add(_footer);
        _grid.BringToFront();
        var card = Theme.TitledCard("Каталог товаров", body);

        _search.TextChanged += (_, _) => { _debounce.Stop(); _debounce.Start(); };
        _debounce.Tick += (_, _) => { _debounce.Stop(); LoadProducts(); };
        _category.SelectedIndexChanged += (_, _) => { if (!_loading) LoadProducts(); };
        _lowOnly.CheckedChanged += (_, _) => LoadProducts();

        Controls.Add(card);
        Controls.Add(Ui.Spacer(12));
        Controls.Add(toolbar);
        Controls.Add(Theme.PageHeader("Товары", "Добавление товаров, изменение цен, остатки и поиск"));
        card.BringToFront();
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
        Theme.Safe(() =>
        {
            var selectedId = Selected?.Id;
            var cat = _category.SelectedItem as string;
            var list = _repo.GetAll(_search.Text, cat == AllCategories ? null : cat, _lowOnly.Checked);
            _grid.DataSource = list;

            if (selectedId != null)
                foreach (DataGridViewRow row in _grid.Rows)
                    if (((Product)row.DataBoundItem).Id == selectedId) { row.Selected = true; _grid.CurrentCell = row.Cells[1]; break; }

            _footer.Text = $"Позиций: {list.Count}   •   Единиц: {list.Sum(p => p.Quantity):N0}   •   " +
                           $"Стоимость (закуп): {Theme.Money(list.Sum(p => p.StockValue))}   •   " +
                           $"Стоимость (продажа): {Theme.Money(list.Sum(p => p.Quantity * p.Price))}";
        });
    }

    private Product Selected => _grid.CurrentRow?.DataBoundItem as Product;

    private void AddProduct()
    {
        using var f = new ProductEditForm(null, _repo.GetCategories());
        if (f.ShowDialog(this) != DialogResult.OK) return;
        Theme.Safe(() => { _repo.Insert(f.Result); RefreshData(); });
    }

    private void EditSelected()
    {
        var p = Selected;
        if (p == null) { Theme.Warn(this, "Выберите товар в списке."); return; }
        using var f = new ProductEditForm(p, _repo.GetCategories());
        if (f.ShowDialog(this) != DialogResult.OK) return;
        Theme.Safe(() => { _repo.Update(f.Result); RefreshData(); });
    }

    private void StockInSelected()
    {
        var p = Selected;
        if (p == null) { Theme.Warn(this, "Выберите товар в списке."); return; }
        var qty = QuantityDialog.Ask(this, "Приход товара", p.Name, $"Сейчас на складе: {p.Quantity} шт.");
        if (qty == null) return;
        Theme.Safe(() => { _repo.AddStock(p.Id, qty.Value); LoadProducts(); });
    }

    private void DeleteSelected()
    {
        var p = Selected;
        if (p == null) { Theme.Warn(this, "Выберите товар в списке."); return; }
        if (!Theme.Confirm(this, $"Удалить товар «{p.Name}»?\nИстория продаж сохранится.")) return;
        Theme.Safe(() => { _repo.Delete(p.Id); RefreshData(); });
    }
}
