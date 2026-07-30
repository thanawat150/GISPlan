using System.Drawing.Drawing2D;

namespace GISPlan.Desktop;

public enum ModernButtonKind
{
    Primary,
    Secondary,
    Ghost,
    Danger,
    Success
}

public static class UiTheme
{
    public static readonly Color Background = Color.FromArgb(244, 247, 251);
    public static readonly Color Surface = Color.White;
    public static readonly Color SurfaceMuted = Color.FromArgb(248, 250, 252);
    public static readonly Color Navy = Color.FromArgb(15, 23, 42);
    public static readonly Color Primary = Color.FromArgb(37, 99, 235);
    public static readonly Color PrimaryHover = Color.FromArgb(29, 78, 216);
    public static readonly Color Cyan = Color.FromArgb(6, 182, 212);
    public static readonly Color Success = Color.FromArgb(22, 163, 74);
    public static readonly Color Warning = Color.FromArgb(217, 119, 6);
    public static readonly Color Danger = Color.FromArgb(220, 38, 38);
    public static readonly Color Text = Color.FromArgb(15, 23, 42);
    public static readonly Color MutedText = Color.FromArgb(100, 116, 139);
    public static readonly Color Border = Color.FromArgb(226, 232, 240);

    public static void ApplyForm(Form form)
    {
        form.BackColor = Background;
        form.ForeColor = Text;
        form.Font = new Font("Segoe UI", 10F);
    }

    public static void StyleInput(Control control)
    {
        control.Font = new Font("Segoe UI", 10F);
        control.BackColor = Surface;
        control.ForeColor = Text;
        control.Margin = new Padding(4, 6, 4, 6);
        control.MinimumSize = new Size(0, 36);

        if (control is TextBox textBox)
            textBox.BorderStyle = BorderStyle.FixedSingle;
        else if (control is ComboBox comboBox)
            comboBox.FlatStyle = FlatStyle.Flat;
        else if (control is NumericUpDown number)
            number.BorderStyle = BorderStyle.FixedSingle;
    }

    public static Label Heading(string text, float size = 18F) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", size, FontStyle.Bold),
        ForeColor = Text,
        Margin = new Padding(0)
    };

    public static Label Caption(string text, int maxWidth = 720) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(maxWidth, 0),
        Font = new Font("Segoe UI", 9.5F),
        ForeColor = MutedText,
        Margin = new Padding(0, 4, 0, 0)
    };

    public static Label FieldLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
        ForeColor = Text,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(3, 13, 8, 3)
    };
}

public sealed class ModernCard : Panel
{
    public int CornerRadius { get; set; } = 16;
    public Color BorderColor { get; set; } = UiTheme.Border;
    public int BorderThickness { get; set; } = 1;

    public ModernCard()
    {
        BackColor = UiTheme.Surface;
        Padding = new Padding(18);
        DoubleBuffered = true;
        Resize += (_, _) => UpdateRegion();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UpdateRegion();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), CornerRadius);
        using var pen = new Pen(BorderColor, BorderThickness);
        e.Graphics.DrawPath(pen, path);
    }

    private void UpdateRegion()
    {
        if (Width <= 0 || Height <= 0) return;
        using var path = RoundedRectangle(new Rectangle(0, 0, Width, Height), CornerRadius);
        Region = new Region(path);
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var diameter = Math.Max(2, radius * 2);
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
        var path = new GraphicsPath();
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}

public sealed class ModernButton : Button
{
    private bool _hovered;
    private ModernButtonKind _kind = ModernButtonKind.Primary;

    public ModernButtonKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            Invalidate();
        }
    }

    public int CornerRadius { get; set; } = 10;

    public ModernButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Height = 40;
        Padding = new Padding(14, 0, 14, 0);
        Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
        Cursor = Cursors.Hand;
        UseVisualStyleBackColor = false;
        DoubleBuffered = true;
        MouseEnter += (_, _) => { _hovered = true; Invalidate(); };
        MouseLeave += (_, _) => { _hovered = false; Invalidate(); };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var (background, foreground, border) = ResolveColors();
        using var path = RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), CornerRadius);
        using var brush = new SolidBrush(background);
        e.Graphics.FillPath(brush, path);
        if (border != Color.Transparent)
        {
            using var pen = new Pen(border);
            e.Graphics.DrawPath(pen, path);
        }

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            ClientRectangle,
            foreground,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private (Color Background, Color Foreground, Color Border) ResolveColors()
    {
        if (!Enabled)
            return (Color.FromArgb(226, 232, 240), Color.FromArgb(148, 163, 184), Color.Transparent);

        return Kind switch
        {
            ModernButtonKind.Primary => (_hovered ? UiTheme.PrimaryHover : UiTheme.Primary, Color.White, Color.Transparent),
            ModernButtonKind.Success => (_hovered ? Color.FromArgb(21, 128, 61) : UiTheme.Success, Color.White, Color.Transparent),
            ModernButtonKind.Danger => (_hovered ? Color.FromArgb(185, 28, 28) : UiTheme.Danger, Color.White, Color.Transparent),
            ModernButtonKind.Secondary => (_hovered ? Color.FromArgb(239, 246, 255) : Color.White, UiTheme.Primary, UiTheme.Primary),
            _ => (_hovered ? Color.FromArgb(241, 245, 249) : UiTheme.SurfaceMuted, UiTheme.Text, UiTheme.Border)
        };
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var diameter = Math.Max(2, radius * 2);
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
        var path = new GraphicsPath();
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}

public sealed class StatusPill : Label
{
    public StatusPill()
    {
        AutoSize = true;
        Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        Padding = new Padding(11, 6, 11, 6);
        Margin = new Padding(0);
        SetNeutral("Ready");
    }

    public void SetNeutral(string text)
    {
        Text = text;
        ForeColor = UiTheme.MutedText;
        BackColor = Color.FromArgb(241, 245, 249);
    }

    public void SetBusy(string text)
    {
        Text = text;
        ForeColor = Color.FromArgb(30, 64, 175);
        BackColor = Color.FromArgb(219, 234, 254);
    }

    public void SetSuccess(string text)
    {
        Text = text;
        ForeColor = Color.FromArgb(22, 101, 52);
        BackColor = Color.FromArgb(220, 252, 231);
    }

    public void SetWarning(string text)
    {
        Text = text;
        ForeColor = Color.FromArgb(146, 64, 14);
        BackColor = Color.FromArgb(254, 243, 199);
    }

    public void SetError(string text)
    {
        Text = text;
        ForeColor = Color.FromArgb(153, 27, 27);
        BackColor = Color.FromArgb(254, 226, 226);
    }
}
