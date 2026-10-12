using InventoryApp.UI.Views;

namespace InventoryApp.UI;

public class MainForm : Form
{
    private readonly Panel _content;
    private readonly List<Button> _navButtons = new();

    private readonly ViewDeck _pages;

    public MainForm()
    {
        Text = "StockFlow — система учёта товаров";
        Size = new Size(1360, 840);
        MinimumSize = new Size(1150, 720);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = AppStyle.Ui.Bg;
        ForeColor = AppStyle.Ui.Text;
        Font = AppStyle.Ui.Body;

        _content = new Panel { Dock = DockStyle.Fill, BackColor = AppStyle.Ui.Bg, Padding = new Padding(30, 26, 30, 26) };
        _pages = new ViewDeck(_content, page => AppStyle.Ui.Safe(page.RefreshData));
        var sidebar = BuildSidebar();

        Controls.Add(_content);
        Controls.Add(sidebar);
        _content.BringToFront();

        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.F5 && _content.Controls.Count > 0 && _content.Controls[0] is IRefreshable r)
                AppStyle.Ui.Safe(r.RefreshData);
        };

        Shown += (_, _) => _navButtons[0].PerformClick();
    }

    private Panel BuildSidebar()
    {
        var sidebar = new Panel { Dock = DockStyle.Left, Width = 236, BackColor = AppStyle.Ui.Sidebar, Padding = new Padding(16, 24, 16, 18) };

        // Логотип
        var logo = new Panel { Dock = DockStyle.Top, Height = 86, BackColor = AppStyle.Ui.Sidebar };
        logo.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var r = new Rectangle(4, 4, 38, 38);
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(r, AppStyle.Ui.Accent, AppStyle.Ui.Pink, 45f);
            using var path = AppStyle.Ui.RoundedRect(r, 10);
            g.FillPath(brush, path);
            using var logoFont = new Font("Segoe UI Black", 15f);
            TextRenderer.DrawText(g, "S", logoFont, r, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };
        var name = AppStyle.Ui.Label("StockFlow", new Font("Segoe UI Semibold", 14f));
        name.Location = new Point(50, 2);
        var sub = AppStyle.Ui.Label("Учёт товаров", AppStyle.Ui.Small, AppStyle.Ui.Muted);
        sub.Location = new Point(52, 26);
        logo.Controls.Add(name);
        logo.Controls.Add(sub);

        // Навигация
        var nav = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = AppStyle.Ui.Sidebar
        };
        var menuLabel = AppStyle.Ui.Label("МЕНЮ", new Font("Segoe UI Semibold", 8f), AppStyle.Ui.Muted);
        menuLabel.Margin = new Padding(6, 0, 0, 10);
        nav.Controls.Add(menuLabel);

        AddNav(nav, "▣   Обзор", () => new DashboardView());
        AddNav(nav, "▤   Товары", () => new ProductsView());
        AddNav(nav, "◈   Продажи", () => new SalesView());
        AddNav(nav, "▥   Склад", () => new StockView());

        // Статус подключения
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = AppStyle.Ui.Sidebar };
        footer.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var b = new SolidBrush(AppStyle.Ui.Success);
            e.Graphics.FillEllipse(b, 8, 21, 9, 9);
        };
        var status = AppStyle.Ui.Label("MySQL подключена", AppStyle.Ui.Small, AppStyle.Ui.Muted);
        status.Location = new Point(24, 16);
        var hint = AppStyle.Ui.Label("F5 — обновить", new Font("Segoe UI", 8f), Color.FromArgb(90, 96, 120));
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
            BackColor = AppStyle.Ui.Sidebar,
            ForeColor = AppStyle.Ui.Muted,
            Font = new Font("Segoe UI Semibold", 10.5f),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = AppStyle.Ui.Input;
        _pages.Register(text, factory);
        btn.OnClick(() => Navigate(text, btn));
        _navButtons.Add(btn);
        nav.Controls.Add(btn);
    }

    private void Navigate(string key, Button active)
    {
        foreach (var b in _navButtons)
        {
            bool isActive = b == active;
            b.BackColor = isActive ? AppStyle.Ui.Accent : AppStyle.Ui.Sidebar;
            b.ForeColor = isActive ? Color.White : AppStyle.Ui.Muted;
            b.FlatAppearance.MouseOverBackColor = isActive ? AppStyle.Ui.Accent : AppStyle.Ui.Input;
        }

        _pages.Show(key);
    }
}
