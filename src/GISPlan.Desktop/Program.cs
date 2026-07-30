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
        Application.Run(new StartupForm());
    }
}
