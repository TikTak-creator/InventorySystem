namespace InventoryApp.UI.Dialogs;

/// <summary>Небольшой диалог ввода количества (приход товара).</summary>
public class QuantityDialog : Form
{
    private readonly NumericUpDown _qty = AppStyle.Ui.Number(1_000_000);

    private QuantityDialog(string title, string subject, string hint)
    {
        AppStyle.Ui.StyleDialog(this, title, new Size(400, 230));

        var t = AppStyle.Ui.Label(title, AppStyle.Ui.H2);
        t.Location = new Point(24, 18);
        var s = new Label
        {
            Text = subject, Font = AppStyle.Ui.Bold, ForeColor = AppStyle.Ui.Text, Location = new Point(24, 50),
            Size = new Size(352, 22), AutoEllipsis = true
        };
        var h = AppStyle.Ui.Label(hint, AppStyle.Ui.Small, AppStyle.Ui.Muted);
        h.Location = new Point(24, 74);
        var l = AppStyle.Ui.Label("Количество, шт.", AppStyle.Ui.Small, AppStyle.Ui.Muted);
        l.Location = new Point(24, 104);
        _qty.Location = new Point(24, 126);
        _qty.Width = 352;
        _qty.Minimum = 1;
        _qty.Value = 10;

        var ok = AppStyle.Ui.Btn("Принять", AppStyle.Ui.Success, 120);
        var cancel = AppStyle.Ui.SecondaryBtn("Отмена", 100);
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
