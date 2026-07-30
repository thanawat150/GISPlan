using GISPlan.Core;

namespace GISPlan.Desktop;

public static class AppNavigation
{
    public static void Attach(Form form)
    {
        var preferences = UserPreferences.Load();
        var localizer = new LocalizationService(preferences.LanguageCode);
        var thai = localizer.LanguageCode.StartsWith("th", StringComparison.OrdinalIgnoreCase);

        var navigation = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            BackColor = UiTheme.Navy,
            ForeColor = Color.White,
            Padding = new Padding(18, 3, 18, 3),
            Height = 38,
            RenderMode = ToolStripRenderMode.System,
            ImageScalingSize = new Size(20, 20)
        };

        var home = MakeButton(thai ? "หน้าหลัก" : "Home");
        home.Enabled = false;
        var data = MakeButton(thai ? "คลังข้อมูลภายนอก" : "External data");
        data.Click += (_, _) =>
        {
            using var dataForm = new DataSourcesForm(new LocalizationService(UserPreferences.Load().LanguageCode));
            dataForm.ShowDialog(form);
        };
        var separator = new ToolStripSeparator();
        var safety = new ToolStripLabel(thai
            ? "ดาวน์โหลดเฉพาะเมื่อผู้ใช้ยืนยัน"
            : "Downloads require confirmation")
        {
            ForeColor = Color.FromArgb(148, 163, 184),
            Alignment = ToolStripItemAlignment.Right,
            Font = new Font("Segoe UI", 9F)
        };

        navigation.Items.AddRange([home, data, separator, safety]);
        form.Controls.Add(navigation);
        navigation.BringToFront();
    }

    private static ToolStripButton MakeButton(string text) => new()
    {
        Text = text,
        DisplayStyle = ToolStripItemDisplayStyle.Text,
        ForeColor = Color.White,
        BackColor = UiTheme.Navy,
        Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
        AutoSize = true,
        Margin = new Padding(0, 0, 12, 0),
        Padding = new Padding(10, 4, 10, 4)
    };
}
