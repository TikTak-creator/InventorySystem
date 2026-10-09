using System.ComponentModel;
using InventoryApp.Data;
using InventoryApp.Models;
using InventoryApp.UI.Views;

namespace InventoryApp.UI.Dialogs;

/// <summary>Окно кассы: выбор товаров, корзина и оформление продажи.</summary>
public class SaleForm : Form
{
    private readonly ProductRepository _products = new();
    private readonly SaleRepository _sales = new();
    private readonly BindingList<CartItem> _cart = new();

    private readonly TextBox _search = Theme.TextBox("Поиск товара…", 300);
    private readonly DataGridView _productsGrid = Theme.Grid();
    private readonly DataGridView _cartGrid = Theme.Grid();
    private readonly NumericUpDown _qty = Theme.Number(100_000);
    private readonly Label _total = Theme.Label("0 сом", Theme.Total);
    private readonly Label _count = Theme.Label("Корзина пуста", Theme.Small, Theme.Muted);

    public int SaleId { get; private set; }
    public decimal SaleTotal { get; private set; }

    public SaleForm()
    {
        Theme.StyleDialog(this, "Новая продажа", new Size(1120, 660));
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(1000, 600);
        Padding = new Padding(20);

        // ----- Левая часть: товары -----
        _productsGrid.Columns.AddRange(
            Theme.Col("Name", "Товар", 220),
            Theme.Col("Price", "Цена", 70, "N0", true),
            Theme.Col("Quantity", "В наличии", 70, right: true));
        _productsGrid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) AddToCart(1); };
        _productsGrid.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { AddToCart((int)_qty.Value); e.Handled = true; } };

        _qty.Minimum = 1;
        _qty.Value = 1;
        _qty.Width = 90;
        var addBtn = Theme.Btn("В корзину →", Theme.Accent, 150);
        addBtn.Click += (_, _) => AddToCart((int)_qty.Value);

        var searchBar = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme.Card };
        _search.Dock = DockStyle.Fill;
        searchBar.Controls.Add(_search);
        _search.TextChanged += (_, _) => LoadProducts();
        _search.KeyDown += (_, e) => { if (e.KeyCode == Keys.Down) { _productsGrid.Focus(); e.Handled = true; } };

        var addBar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, BackColor = Theme.Card, Padding = new Padding(0, 12, 0, 0) };
        var qtyLabel = Ui.ToolLabel("Кол-во:");
        qtyLabel.Margin = new Padding(0, 0, 8, 0);
        _qty.Margin = new Padding(0, 2, 8, 0);
        addBtn.Margin = new Padding(0);
        addBar.Controls.AddRange(new Control[] { qtyLabel, _qty, addBtn });

        var left = new Panel { BackColor = Theme.Card };
        left.Controls.Add(_productsGrid);
        left.Controls.Add(addBar);
        left.Controls.Add(searchBar);
        _productsGrid.BringToFront();

        // ----- Правая часть: корзина -----
        _cartGrid.Columns.AddRange(
            Theme.Col("Name", "Товар", 200),
            Theme.Col("Quantity", "Кол-во", 55, right: true),
            Theme.Col("Price", "Цена", 70, "N0", true),
            Theme.Col("Sum", "Сумма", 80, "N0", true));
        _cartGrid.DataSource = _cart;
        _cartGrid.KeyDown += (_, e) => { if (e.KeyCode == Keys.Delete) RemoveSelected(); };

        var remove = Theme.SecondaryBtn("Убрать", 100);
        var clear = Theme.SecondaryBtn("Очистить", 110);
        var checkout = Theme.Btn("Оформить продажу", Theme.Success, 190);
        remove.Click += (_, _) => RemoveSelected();
        clear.Click += (_, _) => { if (_cart.Count > 0 && Theme.Confirm(this, "Очистить корзину?")) { _cart.Clear(); UpdateTotal(); } };
        checkout.Click += (_, _) => Checkout();

        var totalPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 110,
            BackColor = Theme.Card,
            ColumnCount = 2,
            RowCount = 2
        };
        totalPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        totalPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        totalPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        totalPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

        var infoPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card };
        var totalCaption = Theme.Label("ИТОГО К ОПЛАТЕ", new Font("Segoe UI Semibold", 8.5f), Theme.Muted);
        totalCaption.Location = new Point(0, 4);
        _total.Location = new Point(0, 24);
        infoPanel.Controls.AddRange(new Control[] { totalCaption, _total });

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Theme.Card,
            Padding = new Padding(0, 18, 0, 0),
            WrapContents = false
        };
        buttons.Controls.AddRange(new Control[] { checkout, clear, remove });

        _count.Dock = DockStyle.Fill;
        _count.TextAlign = ContentAlignment.MiddleLeft;

        totalPanel.Controls.Add(infoPanel, 0, 0);
        totalPanel.Controls.Add(buttons, 1, 0);
        totalPanel.Controls.Add(_count, 0, 1);
        totalPanel.SetColumnSpan(_count, 2);

        var right = new Panel { BackColor = Theme.Card };
        right.Controls.Add(_cartGrid);
        right.Controls.Add(totalPanel);
        _cartGrid.BringToFront();

        var body = Ui.Columns(new[] { 50f, 50f }, new Control[]
        {
            Theme.TitledCard("Товары", left),
            Theme.TitledCard("Корзина", right)
        });
        body.Dock = DockStyle.Fill;
        Controls.Add(body);

        CancelButton = null;
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.F2) { Checkout(); e.Handled = true; }
            if (e.KeyCode == Keys.Escape) Close();
        };

        Load += (_, _) => Theme.Safe(LoadProducts);
        Shown += (_, _) => _search.Focus();
        UpdateTotal();
    }

    private void LoadProducts()
    {
        Theme.Safe(() =>
            _productsGrid.DataSource = _products.GetAll(_search.Text).Where(p => p.Quantity > 0).ToList());
    }

    private void AddToCart(int qty)
    {
        if (_productsGrid.CurrentRow?.DataBoundItem is not Product p) { Theme.Warn(this, "Выберите товар."); return; }

        var existing = _cart.FirstOrDefault(c => c.Product.Id == p.Id);
        int already = existing?.Quantity ?? 0;
        if (already + qty > p.Quantity)
        {
            Theme.Warn(this, $"На складе только {p.Quantity} шт. товара «{p.Name}»" +
                             (already > 0 ? $" (в корзине уже {already})." : "."));
            return;
        }

        if (existing != null)
        {
            existing.Quantity += qty;
            _cart.ResetBindings();
        }
        else
        {
            _cart.Add(new CartItem { Product = p, Quantity = qty });
        }
        _qty.Value = 1;
        UpdateTotal();
    }

    private void RemoveSelected()
    {
        if (_cartGrid.CurrentRow?.DataBoundItem is CartItem item)
        {
            _cart.Remove(item);
            UpdateTotal();
        }
    }

    private void UpdateTotal()
    {
        var total = _cart.Sum(c => c.Sum);
        _total.Text = Theme.Money(total);
        _count.Text = _cart.Count == 0
            ? "Корзина пуста • двойной клик по товару — добавить"
            : $"Позиций: {_cart.Count} • Товаров: {_cart.Sum(c => c.Quantity)} шт. • F2 — оформить";
    }

    private void Checkout()
    {
        if (_cart.Count == 0) { Theme.Warn(this, "Добавьте товары в корзину."); return; }
        try
        {
            SaleTotal = _cart.Sum(c => c.Sum);
            SaleId = _sales.CreateSale(_cart.ToList());
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            Theme.Warn(this, ex.Message);
            LoadProducts();
        }
    }
}
