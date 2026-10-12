using System.Drawing.Drawing2D;

namespace EasyDo.WinForms;

/// <summary>Тёмная тема и фабрика стилизованных контролов.</summary>
public sealed partial class UiKit : IDisposable
{
    public UiSkin Skin { get; }
    public UiTexts Texts { get; }
    public Func<decimal, string> FormatMoney { get; }
    internal Font InputFont { get; }
    internal Font DateFont { get; }
    internal Font HeaderFont { get; }
    internal Font CaptionFont { get; }
    private bool _disposed;

    public UiKit(UiSkin skin = null, UiTexts texts = null, Func<decimal, string> money = null)
    {
        Skin = skin ?? new UiSkin();
        Texts = texts ?? new UiTexts();
        FormatMoney = money ?? (value => value.ToString("N2"));
        Body = new(Skin.FontFamily, 10f);
        Small = new(Skin.FontFamily, 9f);
        Bold = new(Skin.StrongFontFamily, 10f);
        H1 = new(Skin.StrongFontFamily, 20f);
        H2 = new(Skin.StrongFontFamily, 12f);
        StatValue = new(Skin.StrongFontFamily, 20f);
        Total = new(Skin.StrongFontFamily, 18f);
        InputFont = new Font(Skin.FontFamily, 11f);
        DateFont = new Font(Skin.FontFamily, 10.5f);
        HeaderFont = new Font(Skin.StrongFontFamily, 9f);
        CaptionFont = new Font(Skin.StrongFontFamily, 8.5f);
    }

    // Dispose the kit after every window using its fonts has closed.
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Body.Dispose();
        Small.Dispose();
        Bold.Dispose();
        H1.Dispose();
        H2.Dispose();
        StatValue.Dispose();
        Total.Dispose();
        InputFont.Dispose(); DateFont.Dispose(); HeaderFont.Dispose(); CaptionFont.Dispose();
    }

    public Color Bg => Skin.Bg;
    public Color Sidebar => Skin.Sidebar;
    public Color Card => Skin.Card;
    public Color CardBorder => Skin.CardBorder;
    public Color Input => Skin.Input;
    public Color Selection => Skin.Selection;
    public Color Accent => Skin.Accent;
    public Color Text => Skin.Text;
    public Color Muted => Skin.Muted;
    public Color Success => Skin.Success;
    public Color Danger => Skin.Danger;
    public Color Warning => Skin.Warning;
    public Color Info => Skin.Info;
    public Color Pink => Skin.Pink;

    public Font Body { get; }
    public Font Small { get; }
    public Font Bold { get; }
    public Font H1 { get; }
    public Font H2 { get; }
    public Font StatValue { get; }
    public Font Total { get; }

    public string Money(decimal v) => FormatMoney(v);

    public Color Blend(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    public GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        radius = Math.Min(radius, Math.Min(r.Width, r.Height) / 2);
        if (radius <= 0) { path.AddRectangle(r); return path; }
        int d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    // ---------- Фабрика контролов ----------

    public Button Btn(string text, Color? color = null, int width = 130)
    {
        var c = color ?? Accent;
        var b = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = c,
            ForeColor = Color.White,
            Font = Bold,
            Height = 36,
            Width = width,
            Cursor = Cursors.Hand,
            Margin = new Padding(8, 0, 0, 0),
            UseVisualStyleBackColor = false
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = Blend(c, Color.White, 0.12f);
        b.FlatAppearance.MouseDownBackColor = Blend(c, Color.Black, 0.15f);
        return b;
    }

    public Button SecondaryBtn(string text, int width = 120)
    {
        var b = Btn(text, Input, width);
        b.ForeColor = Text;
        return b;
    }

    public Label Label(string text, Font font = null, Color? color = null) => new()
    {
        Text = text,
        Font = font ?? Body,
        ForeColor = color ?? Text,
        AutoSize = true,
        BackColor = Color.Transparent
    };

    public TextBox TextBox(string placeholder = "", int width = 240) => new()
    {
        PlaceholderText = placeholder,
        BackColor = Input,
        ForeColor = Text,
        BorderStyle = BorderStyle.FixedSingle,
        Font = InputFont,
        Width = width
    };

    public ComboBox Combo(int width = 200, bool editable = false) => new()
    {
        DropDownStyle = editable ? ComboBoxStyle.DropDown : ComboBoxStyle.DropDownList,
        FlatStyle = FlatStyle.Flat,
        BackColor = Input,
        ForeColor = Text,
        Font = InputFont,
        Width = width
    };

    public NumericUpDown Number(decimal max = 10_000_000, int decimals = 0) => new()
    {
        Minimum = 0,
        Maximum = max,
        DecimalPlaces = decimals,
        ThousandsSeparator = true,
        BackColor = Input,
        ForeColor = Text,
        BorderStyle = BorderStyle.FixedSingle,
        Font = InputFont,
        Width = 200
    };

    public DateTimePicker DatePicker(DateTime value) => new()
    {
        Format = DateTimePickerFormat.Short,
        Value = value,
        Width = 130,
        Font = DateFont,
        CalendarMonthBackground = Input,
        CalendarForeColor = Text
    };

    public DataGridView Grid()
    {
        var g = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Card,
            BorderStyle = BorderStyle.None,
            EnableHeadersVisualStyles = false,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            ColumnHeadersHeight = 40,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = CardBorder,
            RowHeadersVisible = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AutoGenerateColumns = false,
            Font = Body
        };
        g.RowTemplate.Height = 38;
        g.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Card,
            ForeColor = Muted,
            Font = HeaderFont,
            SelectionBackColor = Card,
            SelectionForeColor = Muted,
            Padding = new Padding(6, 0, 6, 0)
        };
        g.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Card,
            ForeColor = Text,
            SelectionBackColor = Selection,
            SelectionForeColor = Color.White,
            Padding = new Padding(6, 0, 6, 0)
        };
        return g;
    }

    public DataGridViewTextBoxColumn Col(string prop, string header, float weight = 100,
        string format = null, bool right = false)
    {
        var c = new DataGridViewTextBoxColumn
        {
            Name = prop,
            DataPropertyName = prop,
            HeaderText = header,
            FillWeight = weight,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        if (format != null) c.DefaultCellStyle.Format = format;
        if (right)
        {
            c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            c.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
        return c;
    }

    /// <summary>Заголовок страницы: крупный заголовок + подзаголовок.</summary>
    public Panel PageHeader(string title, string subtitle)
    {
        var p = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = Bg };
        var t = Label(title, H1);
        t.Location = new Point(0, 0);
        var s = Label(subtitle, Body, Muted);
        s.Location = new Point(3, 44);
        p.Controls.Add(t);
        p.Controls.Add(s);
        return p;
    }

    /// <summary>Карточка с заголовком и содержимым.</summary>
    public Card TitledCard(string title, Control body, Control headerRight = null)
    {
        var card = new Card(this) { Dock = DockStyle.Fill, Padding = new Padding(18, 14, 18, 14) };
        var head = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Card };
        var lbl = Label(title, H2);
        lbl.Location = new Point(0, 4);
        head.Controls.Add(lbl);
        if (headerRight != null)
        {
            headerRight.Dock = DockStyle.Right;
            head.Controls.Add(headerRight);
        }
        body.Dock = DockStyle.Fill;
        card.Controls.Add(body);
        card.Controls.Add(head);
        body.BringToFront();
        return card;
    }

    public void StyleDialog(Form f, string title, Size size)
    {
        f.Text = title;
        f.ClientSize = size;
        f.FormBorderStyle = FormBorderStyle.FixedDialog;
        f.StartPosition = FormStartPosition.CenterParent;
        f.MaximizeBox = false;
        f.MinimizeBox = false;
        f.ShowInTaskbar = false;
        f.BackColor = Bg;
        f.ForeColor = Text;
        f.Font = Body;
    }

    /// <summary>Выполнить действие и показать ошибку, если что-то пошло не так.</summary>
    public void Safe(Action action)
    {
        try { action(); }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, Texts.ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public bool Confirm(IWin32Window owner, string text) =>
        MessageBox.Show(owner, text, Texts.ConfirmTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

    public void Notify(IWin32Window owner, string text) =>
        MessageBox.Show(owner, text, Texts.ApplicationTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);

    public void Warn(IWin32Window owner, string text) =>
        MessageBox.Show(owner, text, Texts.WarningTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
}

public interface IRefreshable
{
    void RefreshData();
}
