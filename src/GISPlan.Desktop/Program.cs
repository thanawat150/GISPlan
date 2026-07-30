using GISPlan.Core;

namespace GISPlan.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        AppPaths.EnsureCreated();
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            MessageBox.Show(e.Exception.Message, "GISPlan Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };

        var startup = new StartupForm();
        AppNavigation.Attach(startup);
        Application.Run(startup);
    }
}
