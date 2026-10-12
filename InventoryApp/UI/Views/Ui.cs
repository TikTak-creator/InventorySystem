namespace InventoryApp.UI.Views;

internal static class Ui
{
    public static void ColorStatus(DataGridView grid, DataGridViewCellFormattingEventArgs e, string column = "Status")
    {
        if (e.RowIndex < 0 || grid.Columns[e.ColumnIndex].Name != column || e.Value is not string s) return;
        var c = s switch { "Нет в наличии" => AppStyle.Ui.Danger, "Мало" => AppStyle.Ui.Warning, _ => AppStyle.Ui.Success };
        e.CellStyle.ForeColor = c;
        e.CellStyle.SelectionForeColor = c;
        e.CellStyle.Font = AppStyle.Ui.Bold;
    }
}
