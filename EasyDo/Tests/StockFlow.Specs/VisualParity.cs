using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using InventoryApp.UI;

namespace StockFlow.Specs;

internal static class VisualParity
{
    internal static void Run(string baselinePath, string output)
    {
        Directory.CreateDirectory(output);
        var context = new AssemblyLoadContext("original-stockflow", isCollectible: true);
        var original = context.LoadFromAssemblyPath(Path.GetFullPath(baselinePath));
        var current = typeof(MainForm).Assembly;
        Program.Test("original business tables retain their schema", () =>
        {
            var oldSchema = (string[])original.GetType("InventoryApp.Data.Database", true).GetField("Schema").GetValue(null);
            Program.Assert(oldSchema.Select(sql => sql.Replace("\r\n", "\n"))
                .SequenceEqual(Inventory.Storage.Database.Schema.Take(oldSchema.Length).Select(sql => sql.Replace("\r\n", "\n"))));
        });
        // MainForm intentionally gains API status/logout; LoginForm is a new screen.
        var screens = new[] { "UI.Views.DashboardView", "UI.Views.ProductsView", "UI.Views.SalesView", "UI.Views.StockView",
            "UI.Dialogs.ProductEditForm", "UI.Dialogs.SaleForm", "UI.Dialogs.QuantityDialog" };
        foreach (var name in screens)
        {
            Program.Test("visual and control-tree parity: " + name, () =>
            {
                using var before = Create(original, name);
                using var after = Create(current, name);
                Prepare(before, name); Prepare(after, name);
                var beforeTree = Describe(before); var afterTree = Describe(after);
                var file = name.Replace('.', '-');
                File.WriteAllText(Path.Combine(output, file + "-before.txt"), beforeTree);
                File.WriteAllText(Path.Combine(output, file + "-after.txt"), afterTree);
                if (beforeTree != afterTree) throw new Exception("Control tree changed: " + name + "; inspect " + output);
                using var beforeImage = new Bitmap(before.Width, before.Height);
                using var afterImage = new Bitmap(after.Width, after.Height);
                before.DrawToBitmap(beforeImage, new Rectangle(Point.Empty, before.Size));
                after.DrawToBitmap(afterImage, new Rectangle(Point.Empty, after.Size));
                var beforeBytes = ImageBytes(beforeImage); var afterBytes = ImageBytes(afterImage);
                beforeImage.Save(Path.Combine(output, file + "-before.png"));
                afterImage.Save(Path.Combine(output, file + "-after.png"));
                if (!beforeBytes.SequenceEqual(afterBytes)) throw new Exception("Rendered pixels changed: " + name + "; inspect " + output);
            });
        }
        context.Unload();
    }
    private static Control Create(Assembly assembly, string name)
    {
        var type = assembly.GetType("InventoryApp." + name, throwOnError: true);
        if (name.EndsWith("ProductEditForm"))
        {
            var product = Activator.CreateInstance(type.GetConstructors().Single().GetParameters()[0].ParameterType);
            foreach (var (key, value) in new (string, object)[] { ("Id", 1), ("Sku", "PRD-001"), ("Name", "Молоко «Ак-Сүт» 3,2% 1 л"),
                ("Category", "Молочные"), ("PurchasePrice", 62m), ("Price", 79m), ("Quantity", 48), ("MinQuantity", 10) })
                product.GetType().GetProperty(key).SetValue(product, value);
            return (Control)Activator.CreateInstance(type, product, new[] { "Молочные", "Бакалея" });
        }
        if (name.EndsWith("QuantityDialog")) return (Control)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, args: ["Приход товара", "Молоко «Ак-Сүт» 3,2% 1 л", "Сейчас на складе: 48 шт."], culture: null);
        return (Control)Activator.CreateInstance(type);
    }
    private static void Prepare(Control control, string name)
    {
        // Construct and render without opening the application or raising Load/Shown (which query MySQL).
        if (control is UserControl) control.Size = new Size(1064, 720);
        if (control is Form form) { form.StartPosition = FormStartPosition.Manual; form.Location = new Point(100, 100); }
        foreach (var field in control.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
        {
            var value = field.GetValue(control);
            if (value?.GetType().Name == "StatCard")
            { value.GetType().GetProperty("Value").SetValue(value, "16"); value.GetType().GetProperty("Hint").SetValue(value, "Тестовые данные"); }
            if (value?.GetType().Name == "BarChart") value.GetType().GetMethod("SetData").Invoke(value,
                [new (string Label, decimal Value)[] { ("Пн", 2500), ("Вт", 1800), ("Ср", 3100), ("Чт", 2250), ("Пт", 4200), ("Сб", 3650), ("Сегодня", 4500) }]);
        }
        control.PerformLayout();
        // Form.Handle creates child handles without showing the form. UserControls need a host for layout.
        _ = control.Handle;
        LayoutChildren(control);
    }
    private static void LayoutChildren(Control control)
    {
        foreach (Control child in control.Controls) { _ = child.Handle; child.PerformLayout(); LayoutChildren(child); }
        control.PerformLayout();
    }
    private static byte[] ImageBytes(Bitmap image)
    {
        using var stream = new MemoryStream(); image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        return SHA256.HashData(stream.ToArray());
    }
    private static string Describe(Control control)
    {
        var result = new StringBuilder();
        void Visit(Control c, string path)
        {
            result.AppendLine($"{path}|{c.GetType().Name}|{c.Text}|{c.Bounds}|{c.Dock}|{c.Padding}|{c.Margin}|{c.BackColor.ToArgb()}|{c.ForeColor.ToArgb()}|{c.Font.Name}:{c.Font.Size}:{c.Font.Style}");
            if (c is DataGridView grid)
                foreach (DataGridViewColumn column in grid.Columns)
                    result.AppendLine($"COLUMN|{column.Name}|{column.DataPropertyName}|{column.HeaderText}|{column.FillWeight}|{column.DefaultCellStyle.Format}|{column.DefaultCellStyle.Alignment}|{column.HeaderCell.Style.Alignment}");
            if (c is NumericUpDown number) result.AppendLine($"NUMBER|{number.Value}|{number.Minimum}|{number.Maximum}|{number.DecimalPlaces}");
            if (c is TableLayoutPanel layout)
            {
                foreach (ColumnStyle column in layout.ColumnStyles) result.AppendLine($"COLSTYLE|{column.SizeType}|{column.Width}");
                foreach (RowStyle row in layout.RowStyles) result.AppendLine($"ROWSTYLE|{row.SizeType}|{row.Height}");
            }
            for (var i = 0; i < c.Controls.Count; i++) Visit(c.Controls[i], path + "/" + i);
        }
        Visit(control, "root"); return result.ToString();
    }
}
