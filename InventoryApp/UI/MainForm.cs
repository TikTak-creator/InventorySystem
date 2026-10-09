using InventoryApp.UI.Views;

namespace InventoryApp.UI;

public class MainForm : Form
{
    private readonly Panel _content;
    private readonly List<Button> _navButtons = new();

    private DashboardView _dashboard;
    private ProductsView _products;
    private SalesView _sales;
    private StockView _stock;

    public MainForm()
    {
        Text = "StockFlow — система учёта товаров";
        Size = new Size(1360, 840);
        MinimumSize = new Size(1150, 720);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = Theme.Body;

        _content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(30, 26, 30, 26) };
        var sidebar = BuildSidebar();

        Controls.Add(_content);
        Controls.Add(sidebar);
        _content.BringToFront();

        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.F5 && _content.Controls.Count > 0 && _content.Controls[0] is IRefreshable r)
                Theme.Safe(r.RefreshData);
        };

        Shown += (_, _) => _navButtons[0].PerformClick();
    }

    private Panel BuildSidebar()
    {
        var sidebar = new Panel { Dock = DockStyle.Left, Width = 236, BackColor = Theme.Sidebar, Padding = new Padding(16, 24, 16, 18) };

        // Логотип
        var logo = new Panel { Dock = DockStyle.Top, Height = 86, BackColor = Theme.Sidebar };
        logo.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var r = new Rectangle(4, 4, 38, 38);
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(r, Theme.Accent, Theme.Pink, 45f);
            using var path = Theme.RoundedRect(r, 10);
            g.FillPath(brush, path);
            TextRenderer.DrawText(g, "S", new Font("Segoe UI Black", 15f), r, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };
        var name = Theme.Label("StockFlow", new Font("Segoe UI Semibold", 14f));
        name.Location = new Point(50, 2);
        var sub = Theme.Label("Учёт товаров", Theme.Small, Theme.Muted);
        sub.Location = new Point(52, 26);
        logo.Controls.Add(name);
        logo.Controls.Add(sub);

        // Навигация
        var nav = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Theme.Sidebar
        };
        var menuLabel = Theme.Label("МЕНЮ", new Font("Segoe UI Semibold", 8f), Theme.Muted);
        menuLabel.Margin = new Padding(6, 0, 0, 10);
        nav.Controls.Add(menuLabel);

        AddNav(nav, "▣   Обзор", () => _dashboard ??= new DashboardView());
        AddNav(nav, "▤   Товары", () => _products ??= new ProductsView());
        AddNav(nav, "◈   Продажи", () => _sales ??= new SalesView());
        AddNav(nav, "▥   Склад", () => _stock ??= new StockView());

        // Статус подключения
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = Theme.Sidebar };
        footer.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var b = new SolidBrush(Theme.Success);
            e.Graphics.FillEllipse(b, 8, 21, 9, 9);
        };
        var status = Theme.Label("MySQL подключена", Theme.Small, Theme.Muted);
        status.Location = new Point(24, 16);
        var hint = Theme.Label("F5 — обновить", new Font("Segoe UI", 8f), Color.FromArgb(90, 96, 120));
        hint.Location = new Point(24, 34);
        footer.Controls.Add(status);
        footer.Controls.Add(hint);

        sidebar.Controls.Add(nav);
        sidebar.Controls.Add(footer);
        sidebar.Controls.Add(logo);
        nav.BringToFront();
        return sidebar;
    }

    private void AddNav(FlowLayoutPanel nav, string text, Func<UserControl> factory)
    {
        var btn = new Button
        {
            Text = text,
            Width = 204,
            Height = 44,
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0),
            Margin = new Padding(0, 0, 0, 6),
            BackColor = Theme.Sidebar,
            ForeColor = Theme.Muted,
            Font = new Font("Segoe UI Semibold", 10.5f),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = Theme.Input;
        btn.Click += (_, _) => Navigate(factory(), btn);
        _navButtons.Add(btn);
        nav.Controls.Add(btn);
    }

    private void Navigate(UserControl view, Button active)
    {
        foreach (var b in _navButtons)
        {
            bool isActive = b == active;
            b.BackColor = isActive ? Theme.Accent : Theme.Sidebar;
            b.ForeColor = isActive ? Color.White : Theme.Muted;
            b.FlatAppearance.MouseOverBackColor = isActive ? Theme.Accent : Theme.Input;
        }

        _content.SuspendLayout();
        _content.Controls.Clear();
        view.Dock = DockStyle.Fill;
        _content.Controls.Add(view);
        _content.ResumeLayout();

        if (view is IRefreshable r) Theme.Safe(r.RefreshData);
    }
}
