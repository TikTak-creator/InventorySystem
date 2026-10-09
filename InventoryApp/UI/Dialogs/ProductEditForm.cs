using InventoryApp.Data;
using InventoryApp.Models;

namespace InventoryApp.UI.Dialogs;

/// <summary>Добавление / редактирование товара.</summary>
public class ProductEditForm : Form
{
    private readonly Product _original;
    private readonly TextBox _name = Theme.TextBox("", 432);
    private readonly TextBox _sku = Theme.TextBox("", 208);
    private readonly ComboBox _category = Theme.Combo(208, editable: true);
    private readonly NumericUpDown _purchase = Theme.Number(10_000_000, 2);
    private readonly NumericUpDown _price = Theme.Number(10_000_000, 2);
    private readonly NumericUpDown _qty = Theme.Number(1_000_000);
    private readonly NumericUpDown _min = Theme.Number(1_000_000);
    private readonly Label _markup = Theme.Label("", Theme.Small, Theme.Muted);

    public Product Result { get; private set; }

    public ProductEditForm(Product product, IEnumerable<string> categories)
    {
        _original = product;
        Theme.StyleDialog(this, product == null ? "Новый товар" : "Редактирование товара", new Size(480, 500));

        var title = Theme.Label(product == null ? "Новый товар" : "Редактирование", Theme.H2);
        title.Location = new Point(24, 18);
        Controls.Add(title);

        int y = 62;
        AddField("Название *", _name, 24, y);
        y += 66;
        AddField("Артикул *", _sku, 24, y);
        AddField("Категория", _category, 248, y);
        y += 66;
        AddField("Закупочная цена, сом", _purchase, 24, y);
        AddField("Цена продажи, сом", _price, 248, y);
        y += 66;
        AddField(product == null ? "Начальный остаток, шт." : "Остаток, шт.", _qty, 24, y);
        AddField("Мин. остаток (уведомление)", _min, 248, y);
        y += 66;
        _markup.Location = new Point(24, y);
        Controls.Add(_markup);

        foreach (var n in new[] { _purchase, _price, _qty, _min }) n.Width = 208;
        _category.Items.AddRange(categories.Cast<object>().ToArray());

        if (product != null)
        {
            _name.Text = product.Name;
            _sku.Text = product.Sku;
            _category.Text = product.Category;
            _purchase.Value = product.PurchasePrice;
            _price.Value = product.Price;
            _qty.Value = product.Quantity;
            _min.Value = product.MinQuantity;
        }
        else
        {
            _sku.Text = "PRD-" + DateTime.Now.ToString("yyMMddHHmmss")[4..];
            _min.Value = 5;
        }

        _purchase.ValueChanged += (_, _) => UpdateMarkup();
        _price.ValueChanged += (_, _) => UpdateMarkup();
        UpdateMarkup();

        var save = Theme.Btn("Сохранить", Theme.Accent, 140);
        var cancel = Theme.SecondaryBtn("Отмена", 110);
        save.Location = new Point(ClientSize.Width - 24 - 140, ClientSize.Height - 60);
        cancel.Location = new Point(save.Left - 10 - 110, save.Top);
        save.Click += (_, _) => Save();
        cancel.DialogResult = DialogResult.Cancel;
        Controls.Add(save);
        Controls.Add(cancel);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private void AddField(string label, Control input, int x, int y)
    {
        var l = Theme.Label(label, Theme.Small, Theme.Muted);
        l.Location = new Point(x, y);
        input.Location = new Point(x, y + 22);
        Controls.Add(l);
        Controls.Add(input);
    }

    private void UpdateMarkup()
    {
        var buy = _purchase.Value;
        var sell = _price.Value;
        if (buy <= 0) { _markup.Text = "Укажите закупочную цену, чтобы рассчитать наценку"; _markup.ForeColor = Theme.Muted; return; }
        var markup = (sell - buy) / buy * 100;
        _markup.Text = $"Наценка: {markup:0.#}%   •   Прибыль с единицы: {Theme.Money(sell - buy)}";
        _markup.ForeColor = sell < buy ? Theme.Danger : Theme.Success;
    }

    private void Save()
    {
        var name = _name.Text.Trim();
        var sku = _sku.Text.Trim();

        if (name.Length == 0) { Theme.Warn(this, "Введите название товара."); _name.Focus(); return; }
        if (sku.Length == 0) { Theme.Warn(this, "Введите артикул."); _sku.Focus(); return; }
        if (_price.Value <= 0) { Theme.Warn(this, "Цена продажи должна быть больше нуля."); _price.Focus(); return; }

        try
        {
            if (new ProductRepository().SkuExists(sku, _original?.Id ?? 0))
            {
                Theme.Warn(this, $"Товар с артикулом «{sku}» уже существует.");
                _sku.Focus();
                return;
            }
        }
        catch (Exception ex) { Theme.Warn(this, ex.Message); return; }

        if (_price.Value < _purchase.Value &&
            !Theme.Confirm(this, "Цена продажи ниже закупочной. Сохранить всё равно?"))
            return;

        Result = new Product
        {
            Id = _original?.Id ?? 0,
            Name = name,
            Sku = sku,
            Category = _category.Text.Trim(),
            PurchasePrice = _purchase.Value,
            Price = _price.Value,
            Quantity = (int)_qty.Value,
            MinQuantity = (int)_min.Value
        };
        DialogResult = DialogResult.OK;
    }
}
