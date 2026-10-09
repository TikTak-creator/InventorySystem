namespace InventoryApp.UI.Dialogs;

/// <summary>Небольшой диалог ввода количества (приход товара).</summary>
public class QuantityDialog : Form
{
    private readonly NumericUpDown _qty = Theme.Number(1_000_000);

    private QuantityDialog(string title, string subject, string hint)
    {
        Theme.StyleDialog(this, title, new Size(400, 230));

        var t = Theme.Label(title, Theme.H2);
        t.Location = new Point(24, 18);
        var s = new Label
        {
            Text = subject, Font = Theme.Bold, ForeColor = Theme.Text, Location = new Point(24, 50),
            Size = new Size(352, 22), AutoEllipsis = true
        };
        var h = Theme.Label(hint, Theme.Small, Theme.Muted);
        h.Location = new Point(24, 74);
        var l = Theme.Label("Количество, шт.", Theme.Small, Theme.Muted);
        l.Location = new Point(24, 104);
        _qty.Location = new Point(24, 126);
        _qty.Width = 352;
        _qty.Minimum = 1;
        _qty.Value = 10;

        var ok = Theme.Btn("Принять", Theme.Success, 120);
        var cancel = Theme.SecondaryBtn("Отмена", 100);
        ok.Location = new Point(400 - 24 - 120, 176);
        cancel.Location = new Point(ok.Left - 110, 176);
        ok.DialogResult = DialogResult.OK;
        cancel.DialogResult = DialogResult.Cancel;
        AcceptButton = ok;
        CancelButton = cancel;

        Controls.AddRange(new Control[] { t, s, h, l, _qty, ok, cancel });
        Shown += (_, _) => { _qty.Select(0, _qty.Text.Length); _qty.Focus(); };
    }

    public static int? Ask(IWin32Window owner, string title, string subject, string hint)
    {
        using var d = new QuantityDialog(title, subject, hint);
        return d.ShowDialog(owner) == DialogResult.OK ? (int)d._qty.Value : null;
    }
}
