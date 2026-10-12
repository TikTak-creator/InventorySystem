namespace InventoryApp.UI;

/// <summary>StockFlow's presentation policy; EasyDo does not know about shops or currencies.</summary>
internal static class AppStyle
{
    public static UiKit Ui { get; } = new(new UiSkin(), new UiTexts
    {
        ApplicationTitle = "StockFlow", ErrorTitle = "Ошибка", WarningTitle = "Внимание",
        ConfirmTitle = "Подтверждение", NoData = "Нет данных"
    }, value => value.ToString("N0") + " сом");
}
