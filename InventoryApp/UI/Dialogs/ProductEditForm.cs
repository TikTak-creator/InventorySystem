using InventoryApp.Data;
using Inventory.Contracts;

namespace InventoryApp.UI.Dialogs;

/// <summary>Добавление / редактирование товара.</summary>
public class ProductEditForm : Form
{
    private readonly Product _original;
    private readonly TextBox _name = AppStyle.Ui.TextBox("", 432);
    private readonly TextBox _sku = AppStyle.Ui.TextBox("", 208);
    private readonly ComboBox _category = AppStyle.Ui.Combo(208, editable: true);
    private readonly NumericUpDown _purchase = AppStyle.Ui.Number(10_000_000, 2);
    private readonly NumericUpDown _price = AppStyle.Ui.Number(10_000_000, 2);
    private readonly NumericUpDown _qty = AppStyle.Ui.Number(1_000_000);
    private readonly NumericUpDown _min = AppStyle.Ui.Number(1_000_000);
    private readonly Label _markup = AppStyle.Ui.Label("", AppStyle.Ui.Small, AppStyle.Ui.Muted);

    private readonly FormBinding<Product> _binding;

    public Product Result { get; private set; }

    public ProductEditForm(Product product, IEnumerable<string> categories)
    {
        _original = product;
        AppStyle.Ui.StyleDialog(this, product == null ? "Новый товар" : "Редактирование товара", new Size(480, 500));

        var title = AppStyle.Ui.Label(product == null ? "Новый товар" : "Редактирование", AppStyle.Ui.H2);
        title.Location = new Point(24, 18);
        Controls.Add(title);

        int y = 62;
        AppStyle.Ui.Field(this, "Название *", _name, 24, y);
        y += 66;
        AppStyle.Ui.Field(this, "Артикул *", _sku, 24, y);
        AppStyle.Ui.Field(this, "Категория", _category, 248, y);
        y += 66;
        AppStyle.Ui.Field(this, "Закупочная цена, сом", _purchase, 24, y);
        AppStyle.Ui.Field(this, "Цена продажи, сом", _price, 248, y);
        y += 66;
        AppStyle.Ui.Field(this, product == null ? "Начальный остаток, шт." : "Остаток, шт.", _qty, 24, y);
        AppStyle.Ui.Field(this, "Мин. остаток (уведомление)", _min, 248, y);
        y += 66;
        _markup.Location = new Point(24, y);
        Controls.Add(_markup);

        foreach (var n in new[] { _purchase, _price, _qty, _min }) n.Width = 208;
        _category.Items.AddRange(categories.Cast<object>().ToArray());

        _binding = new FormBinding<Product>()
            .Text(_name, p => p.Name, (p, value) => p.Name = value.Trim())
            .Text(_sku, p => p.Sku, (p, value) => p.Sku = value.Trim())
            .Choice(_category, p => p.Category, (p, value) => p.Category = value.Trim())
            .Number(_purchase, p => p.PurchasePrice, (p, value) => p.PurchasePrice = value)
            .Number(_price, p => p.Price, (p, value) => p.Price = value)
            .Number(_qty, p => p.Quantity, (p, value) => p.Quantity = (int)value)
            .Number(_min, p => p.MinQuantity, (p, value) => p.MinQuantity = (int)value);

        if (product != null)
        {
            _binding.Load(product);
        }
        else
        {
            _sku.Text = "PRD-" + DateTime.Now.ToString("yyMMddHHmmss")[4..];
            _min.Value = 5;
        }

        _purchase.ValueChanged += (_, _) => UpdateMarkup();
        _price.ValueChanged += (_, _) => UpdateMarkup();
        UpdateMarkup();

        var save = AppStyle.Ui.Btn("Сохранить", AppStyle.Ui.Accent, 140);
        var cancel = AppStyle.Ui.SecondaryBtn("Отмена", 110);
        save.Location = new Point(ClientSize.Width - 24 - 140, ClientSize.Height - 60);
        cancel.Location = new Point(save.Left - 10 - 110, save.Top);
        save.OnClick(Save);
        cancel.DialogResult = DialogResult.Cancel;
        Controls.Add(save);
        Controls.Add(cancel);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private void UpdateMarkup()
    {
        var buy = _purchase.Value;
        var sell = _price.Value;
        if (buy <= 0) { _markup.Text = "Укажите закупочную цену, чтобы рассчитать наценку"; _markup.ForeColor = AppStyle.Ui.Muted; return; }
        var markup = (sell - buy) / buy * 100;
        _markup.Text = $"Наценка: {markup:0.#}%   •   Прибыль с единицы: {AppStyle.Ui.Money(sell - buy)}";
        _markup.ForeColor = sell < buy ? AppStyle.Ui.Danger : AppStyle.Ui.Success;
    }

    private void Save()
    {
        var name = _name.Text.Trim();
        var sku = _sku.Text.Trim();

        if (name.Length == 0) { AppStyle.Ui.Warn(this, "Введите название товара."); _name.Focus(); return; }
        if (sku.Length == 0) { AppStyle.Ui.Warn(this, "Введите артикул."); _sku.Focus(); return; }
        if (_price.Value <= 0) { AppStyle.Ui.Warn(this, "Цена продажи должна быть больше нуля."); _price.Focus(); return; }

        try
        {
            if (new ProductRepository().SkuExists(sku, _original?.Id ?? 0))
            {
                AppStyle.Ui.Warn(this, $"Товар с артикулом «{sku}» уже существует.");
                _sku.Focus();
                return;
            }
        }
        catch (Exception ex) { AppStyle.Ui.Warn(this, ex.Message); return; }

        if (_price.Value < _purchase.Value &&
            !AppStyle.Ui.Confirm(this, "Цена продажи ниже закупочной. Сохранить всё равно?"))
            return;

        Result = _binding.Save(new Product { Id = _original?.Id ?? 0, UpdatedAt = _original?.UpdatedAt ?? default });
        var failures = ProductRules.Valid.Validate(Result);
        if (failures.Count > 0) { AppStyle.Ui.Warn(this, failures[0].Message); return; }
        DialogResult = DialogResult.OK;
    }
}
