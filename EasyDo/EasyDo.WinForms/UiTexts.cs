namespace EasyDo.WinForms;

public sealed record UiTexts
{
    public string ApplicationTitle { get; init; } = "Application";
    public string ErrorTitle { get; init; } = "Error";
    public string WarningTitle { get; init; } = "Warning";
    public string ConfirmTitle { get; init; } = "Confirm";
    public string NoData { get; init; } = "No data";
}
