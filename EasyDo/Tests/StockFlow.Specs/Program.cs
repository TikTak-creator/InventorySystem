using System.Globalization;
using EasyDo;
using EasyDo.WinForms;
using Inventory.Contracts;
using InventoryApp.UI;
using InventoryApp.UI.Dialogs;

namespace StockFlow.Specs;

internal static class Program
{
    internal static int Count;
    internal static void Test(string name, Action run) { run(); Console.WriteLine("PASS " + name); Count++; }
    internal static void Assert(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
    internal static T Throws<T>(Action run) where T : Exception
    {
        try { run(); } catch (T error) { return error; }
        throw new Exception("Expected " + typeof(T).Name);
    }
    [STAThread]
    private static void Main(string[] args)
    {
        var culture = new CultureInfo("ru-RU");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        ApplicationConfiguration.Initialize();
        using var ui = new UiKit();

        Test("typed table binding, formatting and selection", () =>
        {
            using var form = new Form(); using var grid = ui.Grid(); form.Controls.Add(grid);
            grid.Define<Product>().Column(p => p.Id, "ID", 50).Column(p => p.Price, "Price", 80, "N2", true).Apply();
            _ = form.Handle; _ = grid.Handle;
            grid.BindRows(new[] { new Product { Id = 1 }, new Product { Id = 2 } }, p => p.Id);
            grid.CurrentCell = grid.Rows[1].Cells[0];
            grid.BindRows(new[] { new Product { Id = 2, Price = 10.50m }, new Product { Id = 1 } }, p => p.Id);
            Assert(grid.Selected<Product>().Id == 2 && grid.Columns[1].DefaultCellStyle.Format == "N2");
        });
        Test("typed grid rejects computed and duplicate properties", () =>
        {
            using var grid = ui.Grid(); var recipe = grid.Define<Product>().Column(p => p.Name, "Name");
            Throws<ArgumentException>(() => recipe.Column(p => p.Name, "Again"));
            Throws<ArgumentException>(() => recipe.Column(p => p.Name.Length, "Length"));
            recipe.Apply(); Throws<InvalidOperationException>(() => recipe.Apply());
        });
        Test("surface docking order and single-use guard", () =>
        {
            using var host = new Panel { Size = new Size(600, 400) };
            var body = new Panel(); var header = new Panel { Height = 40 };
            var recipe = ui.Page(body).Above(header); recipe.Into(host); host.PerformLayout();
            Assert(body.Dock == DockStyle.Fill && header.Dock == DockStyle.Top && body.Top == 40 && body.Height == 360);
            Throws<InvalidOperationException>(() => recipe.Build());
        });
        Test("column recipe rejects mismatched input before mutation", () =>
        {
            using var child = new Panel();
            Throws<ArgumentException>(() => ui.Columns([50, 50], [child])); Assert(child.Parent == null);
            Throws<ArgumentException>(() => ui.Columns([100], [null]));
        });
        Test("form binding loads without mutating model until Save", () =>
        {
            using var name = new TextBox(); using var price = new NumericUpDown { Maximum = 1000 };
            var model = new Product { Name = "First", Price = 12.5m };
            var binding = new FormBinding<Product>().Text(name, p => p.Name, (p, value) => p.Name = value.Trim())
                .Number(price, p => p.Price, (p, value) => p.Price = value);
            binding.Load(model); name.Text = " Second "; price.Value = 25;
            Assert(model.Name == "First" && model.Price == 12.5m);
            var saved = binding.Save(new Product()); Assert(saved.Name == "Second" && saved.Price == 25);
        });
        Test("matrix validates spans and builds the declared cells", () =>
        {
            using var one = new Panel(); using var two = new Panel();
            var recipe = ui.Matrix(new[] { 60f, 40f }, new[] { 50f, 50f }).Cell(one, 0, 0, rowSpan: 2);
            Throws<ArgumentException>(() => recipe.Cell(two, 0, 1));
            Throws<ArgumentOutOfRangeException>(() => recipe.Cell(two, 2, 0));
            using var matrix = recipe.Cell(two, 1, 0).Build();
            Assert(matrix.GetRowSpan(one) == 2 && matrix.Controls.Count == 2);
            Throws<InvalidOperationException>(() => recipe.Build());
        });
        Test("navigation creates lazily, caches pages, refreshes and disposes detached pages", () =>
        {
            using var host = new Panel(); var created = 0;
            var deck = new ViewDeck(host); deck.Register("a", () => { created++; return new TestPage(); });
            deck.Register("b", () => new TestPage()); Assert(created == 0);
            var first = (TestPage)deck.Show("a"); deck.Show("b"); var again = deck.Show("a");
            Assert(created == 1 && first == again && first.Refreshes == 2);
            host.Dispose(); Assert(first.IsDisposed); Throws<ObjectDisposedException>(() => deck.Show("a"));
        });
        Test("navigation rejects duplicate and unknown routes", () =>
        {
            using var host = new Panel(); using var deck = new ViewDeck(host);
            deck.Register("a", () => new TestPage()); Throws<ArgumentException>(() => deck.Register("a", () => new TestPage()));
            Throws<KeyNotFoundException>(() => deck.Show("missing"));
        });
        Test("debounce is disposed with owner", () =>
        {
            var owner = new Panel(); using var action = new DebouncedAction(owner, () => { });
            action.Trigger(); owner.Dispose(); Throws<ObjectDisposedException>(action.Trigger);
        });
        Test("button action fires once per click", () =>
        {
            var calls = 0; using var button = ui.Btn("Go").OnClick(() => calls++); button.PerformClick(); Assert(calls == 1);
        });
        Test("chart rejects unsupported negative values and handles a tiny viewport", () =>
        {
            using var chart = new BarChart(ui) { Size = new Size(20, 20) };
            Throws<ArgumentOutOfRangeException>(() => chart.SetData(new[] { ("loss", -1m) }));
            chart.SetData(new[] { ("zero", 0m) });
            using var bitmap = new Bitmap(20, 20); chart.DrawToBitmap(bitmap, new Rectangle(0, 0, 20, 20));
            using var card = new StatCard(ui, "tiny", ui.Accent) { Size = new Size(10, 10) };
            card.DrawToBitmap(bitmap, new Rectangle(0, 0, 10, 10));
        });
        Test("domain rules reject invalid stock before persistence", () =>
        {
            var p = new Product { Name = "x", Sku = "x", Price = 1, Quantity = -1 };
            var error = Throws<RuleException>(() => ProductRules.Valid.Ensure(p)); Assert(error.Failures.Any(f => f.Key == nameof(Product.Quantity)));
        });
        Test("desktop assembly has no MySQL or storage reference; login masks password", () =>
        {
            var references = typeof(MainForm).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            Assert(!references.Contains("MySqlConnector") && !references.Contains("Inventory.Storage"));
            using var login = new LoginForm();
            var field = (TextBox)typeof(LoginForm).GetField("_password", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(login);
            Assert(field.UseSystemPasswordChar && login.Controls.OfType<TextBox>().Any(t => t.Text.StartsWith("https://")));
        });

        var baseline = Argument(args, "--baseline");
        if (baseline != null) VisualParity.Run(baseline, Argument(args, "--output") ?? "visual-parity");
        if (args.Contains("--mysql")) MySqlSpecs.Run(Argument(args, "--settings") ?? "InventoryApp/appsettings.json");
        Console.WriteLine($"{Count} StockFlow/WinForms checks passed.");
    }
    private static string Argument(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
    private sealed class TestPage : UserControl, IRefreshable
    {
        public int Refreshes;
        public void RefreshData() => Refreshes++;
    }
}
