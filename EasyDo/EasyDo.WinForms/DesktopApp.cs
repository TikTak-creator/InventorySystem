using System.Globalization;

namespace EasyDo.WinForms;

public static class DesktopApp
{
    /// <summary>Call on an STA thread after ApplicationConfiguration.Initialize.</summary>
    public static void Run(UiKit ui, string culture, Action initialize, Func<Form> mainWindow, Action<Exception> initializationError)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(initialize);
        ArgumentNullException.ThrowIfNull(mainWindow);
        ArgumentNullException.ThrowIfNull(initializationError);
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            throw new InvalidOperationException("DesktopApp requires an STA entry point.");
        var info = new CultureInfo(culture);
        CultureInfo.DefaultThreadCurrentCulture = info;
        CultureInfo.DefaultThreadCurrentUICulture = info;
        Thread.CurrentThread.CurrentCulture = info;
        Thread.CurrentThread.CurrentUICulture = info;
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        ThreadExceptionEventHandler handler = (_, e) => ui.Safe(() => throw e.Exception);
        Application.ThreadException += handler;
        try
        {
            try { initialize(); }
            catch (Exception error) { initializationError(error); return; }
            using var form = mainWindow();
            Application.Run(form);
        }
        finally { Application.ThreadException -= handler; }
    }
}
