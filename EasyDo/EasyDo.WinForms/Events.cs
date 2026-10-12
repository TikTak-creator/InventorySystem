namespace EasyDo.WinForms;

public static class EventBlocks
{
    public static T OnClick<T>(this T control, Action action) where T : Control
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(action);
        control.Click += (_, _) => action();
        return control;
    }
}
